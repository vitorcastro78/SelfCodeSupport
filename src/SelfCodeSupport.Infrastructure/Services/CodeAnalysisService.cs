using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;
using ModelsSymbolKind = SelfCodeSupport.Core.Models.SymbolKind;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Serviço de análise semântica de código usando Roslyn (similar ao Cursor IDE)
/// </summary>
public class CodeAnalysisService : ICodeAnalysisService
{
    private readonly GitSettings _gitSettings;
    private readonly WorkflowSettings _workflowSettings;
    private readonly ILogger<CodeAnalysisService> _logger;
    private readonly IGitService _gitService;
    private CodeIndex? _codeIndex;
    private Compilation? _compilation;
    private readonly object _indexLock = new();

    public CodeAnalysisService(
        IOptions<GitSettings> gitSettings,
        IOptions<WorkflowSettings> workflowSettings,
        ILogger<CodeAnalysisService> logger,
        IGitService gitService)
    {
        _gitSettings = gitSettings.Value;
        _workflowSettings = workflowSettings.Value;
        _logger = logger;
        _gitService = gitService;
    }

    public async Task<CodeIndex> IndexProjectAsync(CancellationToken cancellationToken = default)
    {
        if (_codeIndex != null)
        {
            return _codeIndex;
        }

        lock (_indexLock)
        {
            if (_codeIndex != null)
            {
                return _codeIndex;
            }

            _logger.LogInformation("Starting semantic indexing of project...");
        }

        var index = new CodeIndex
        {
            IndexedAt = DateTime.UtcNow
        };

        try
        {
            var files = _gitService.ListFiles(pattern: "*.cs")
                .Where(f => !ShouldIgnoreFile(f))
                .ToList();

            _logger.LogInformation("Analyzing {Count} C# files", files.Count);

            var syntaxTrees = new List<SyntaxTree>();
            var fileStructures = new Dictionary<string, FileStructure>();
            var allSymbols = new List<CodeSymbol>();

            // Analisar arquivos em paralelo para melhor performance
            var filesToProcess = files.Take(500).ToList(); // Limitar para performance
            _logger.LogInformation("Processing {Count} files in parallel", filesToProcess.Count);

            var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2); // Limitar concorrência
            var tasks = filesToProcess.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var content = await _gitService.GetFileContentAsync(file, cancellationToken);
                    var syntaxTree = CSharpSyntaxTree.ParseText(content, path: file);
                    
                    lock (syntaxTrees)
                    {
                        syntaxTrees.Add(syntaxTree);
                    }

                    var structure = AnalyzeFileStructure(syntaxTree, file);
                    var symbols = ExtractSymbols(syntaxTree, file);
                    
                    lock (fileStructures)
                    {
                        fileStructures[file] = structure;
                        index.Files[file] = structure;
                    }
                    
                    lock (allSymbols)
                    {
                        allSymbols.AddRange(symbols);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error analyzing file {FilePath}", file);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            index.Symbols = allSymbols;

            // Criar compilação para análise semântica
            var references = GetMetadataReferences();
            _compilation = CSharpCompilation.Create(
                "ProjectAnalysis",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Encontrar relacionamentos
            index.Relationships = FindAllRelationships(index);

            _codeIndex = index;
            _logger.LogInformation("Indexing completed: {SymbolCount} symbols, {FileCount} files", 
                index.Symbols.Count, index.Files.Count);

            return index;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing project");
            throw;
        }
    }

    public async Task<IEnumerable<CodeSymbol>> FindSymbolsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var index = await IndexProjectAsync(cancellationToken);
        
        var term = searchTerm.ToLowerInvariant();
        return index.Symbols
            .Where(s => 
                s.Name.ToLowerInvariant().Contains(term) ||
                s.Namespace.ToLowerInvariant().Contains(term) ||
                s.Methods.Any(m => m.ToLowerInvariant().Contains(term)) ||
                s.Properties.Any(p => p.ToLowerInvariant().Contains(term)))
            .OrderByDescending(s => 
                s.Name.Equals(term, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .Take(20);
    }

    public async Task<CodeContext> BuildSemanticContextAsync(JiraTicket ticket, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building semantic context for ticket {TicketId}", ticket.Id);

        var index = await IndexProjectAsync(cancellationToken);
        var context = new CodeContext();

        // Extrair termos relevantes do ticket
        var searchTerms = ExtractSearchTerms(ticket);
        
        // Encontrar símbolos relevantes em paralelo
        var relevantSymbols = new List<CodeSymbol>();
        var symbolTasks = searchTerms.Select(term => FindSymbolsAsync(term, cancellationToken));
        var symbolResults = await Task.WhenAll(symbolTasks);
        relevantSymbols.AddRange(symbolResults.SelectMany(s => s));

        // Remover duplicatas e ordenar por relevância
        relevantSymbols = relevantSymbols
            .GroupBy(s => s.FilePath + s.Name)
            .Select(g => g.First())
            .OrderByDescending(s => CalculateRelevanceScore(s, ticket, searchTerms))
            .Take(15)
            .ToList();

        context.RelevantSymbols = relevantSymbols;

        // Encontrar arquivos relevantes
        var relevantFiles = new Dictionary<string, RelevantFile>();
        
        foreach (var symbol in relevantSymbols)
        {
            if (!relevantFiles.ContainsKey(symbol.FilePath))
            {
                var fileStructure = index.Files.GetValueOrDefault(symbol.FilePath);
                var reasons = new List<string> 
                { 
                    $"Contém símbolo '{symbol.Name}' ({symbol.Kind})" 
                };

                relevantFiles[symbol.FilePath] = new RelevantFile
                {
                    FilePath = symbol.FilePath,
                    RelevanceScore = CalculateRelevanceScore(symbol, ticket, searchTerms),
                    Reasons = reasons,
                    Structure = fileStructure
                };
            }
        }

        // Encontrar relacionamentos em paralelo
        var relationships = new List<SymbolRelationship>();
        var relationshipTasks = relevantSymbols.Take(10)
            .Select(symbol => FindRelationshipsAsync(symbol.Name, cancellationToken));
        var relationshipResults = await Task.WhenAll(relationshipTasks);
        relationships.AddRange(relationshipResults.SelectMany(r => r));

        context.Relationships = relationships
            .GroupBy(r => r.FromSymbol + r.ToSymbol + r.Type)
            .Select(g => g.First())
            .ToList();

        // Adicionar arquivos relacionados através de relacionamentos
        foreach (var rel in context.Relationships)
        {
            if (!relevantFiles.ContainsKey(rel.FilePath))
            {
                var fileStructure = index.Files.GetValueOrDefault(rel.FilePath);
                relevantFiles[rel.FilePath] = new RelevantFile
                {
                    FilePath = rel.FilePath,
                    RelevanceScore = 0.5,
                    Reasons = new List<string> { $"Relacionado via {rel.Type}" },
                    Structure = fileStructure
                };
            }
        }

        context.RelevantFiles = relevantFiles.Values
            .OrderByDescending(f => f.RelevanceScore)
            .Take(10)
            .ToList();

        // Construir contexto estruturado
        context.StructuredContext = BuildStructuredContext(context, index);

        // Carregar conteúdos dos arquivos mais relevantes em paralelo
        var fileContentTasks = context.RelevantFiles.Take(5).Select(async file =>
        {
            try
            {
                var content = await _gitService.GetFileContentAsync(file.FilePath, cancellationToken);
                // Limitar tamanho do arquivo
                if (content.Length > 10000)
                {
                    content = content.Substring(0, 10000) + "\n// ... (arquivo truncado)";
                }
                return new { file.FilePath, content };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading file {FilePath}", file.FilePath);
                return null;
            }
        });

        var fileContents = await Task.WhenAll(fileContentTasks);
        foreach (var fileContent in fileContents.Where(f => f != null))
        {
            context.FileContents[fileContent!.FilePath] = fileContent.content;
        }

        return context;
    }

    public async Task<IEnumerable<SymbolRelationship>> FindRelationshipsAsync(string symbolName, CancellationToken cancellationToken = default)
    {
        var index = await IndexProjectAsync(cancellationToken);
        return index.Relationships.GetValueOrDefault(symbolName, new List<SymbolRelationship>());
    }

    public async Task<FileStructure> GetFileStructureAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var index = await IndexProjectAsync(cancellationToken);
        return index.Files.GetValueOrDefault(filePath, new FileStructure { FilePath = filePath });
    }

    #region Private Methods

    private FileStructure AnalyzeFileStructure(SyntaxTree syntaxTree, string filePath)
    {
        var root = syntaxTree.GetRoot();
        var structure = new FileStructure
        {
            FilePath = filePath
        };

        // Extrair usings
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
        structure.Usings = usings.Select(u => u.Name?.ToString() ?? string.Empty).ToList();

        // Extrair namespace
        var namespaceDecl = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
        {
            structure.Namespace = namespaceDecl.Name.ToString();
        }

        // Extrair classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classes)
        {
            var symbol = new CodeSymbol
            {
                Name = classDecl.Identifier.ValueText,
                Kind = ModelsSymbolKind.Class,
                FilePath = filePath,
                LineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Namespace = structure.Namespace
            };

            // Base type
            if (classDecl.BaseList != null)
            {
                var baseType = classDecl.BaseList.Types.FirstOrDefault();
                if (baseType != null)
                {
                    symbol.BaseType = baseType.Type.ToString();
                }
            }

            // Implemented interfaces
            symbol.ImplementedInterfaces = classDecl.BaseList?.Types
                .Skip(1) // Pular base class
                .Select(t => t.Type.ToString())
                .ToList() ?? new List<string>();

            // Methods
            symbol.Methods = classDecl.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(m => m.Identifier.ValueText)
                .ToList();

            // Properties
            symbol.Properties = classDecl.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Select(p => p.Identifier.ValueText)
                .ToList();

            structure.Classes.Add(symbol);
        }

        // Extrair interfaces
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var interfaceDecl in interfaces)
        {
            var symbol = new CodeSymbol
            {
                Name = interfaceDecl.Identifier.ValueText,
                Kind = ModelsSymbolKind.Interface,
                FilePath = filePath,
                LineNumber = interfaceDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Namespace = structure.Namespace
            };

            structure.Interfaces.Add(symbol);
        }

        return structure;
    }

    private List<CodeSymbol> ExtractSymbols(SyntaxTree syntaxTree, string filePath)
    {
        var symbols = new List<CodeSymbol>();
        var root = syntaxTree.GetRoot();

        // Classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classes)
        {
            symbols.Add(new CodeSymbol
            {
                Name = classDecl.Identifier.ValueText,
                Kind = ModelsSymbolKind.Class,
                FilePath = filePath,
                LineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Namespace = GetNamespace(classDecl)
            });
        }

        // Interfaces
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var interfaceDecl in interfaces)
        {
            symbols.Add(new CodeSymbol
            {
                Name = interfaceDecl.Identifier.ValueText,
                Kind = ModelsSymbolKind.Interface,
                FilePath = filePath,
                LineNumber = interfaceDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Namespace = GetNamespace(interfaceDecl)
            });
        }

        return symbols;
    }

    private string GetNamespace(SyntaxNode node)
    {
        var namespaceDecl = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        return namespaceDecl?.Name.ToString() ?? string.Empty;
    }

    private Dictionary<string, List<SymbolRelationship>> FindAllRelationships(CodeIndex index)
    {
        var relationships = new Dictionary<string, List<SymbolRelationship>>();

        foreach (var symbol in index.Symbols)
        {
            var symbolRels = new List<SymbolRelationship>();

            // Buscar herança
            if (!string.IsNullOrEmpty(symbol.BaseType))
            {
                var baseSymbol = index.Symbols.FirstOrDefault(s => s.Name == symbol.BaseType.Split('.').Last());
                if (baseSymbol != null)
                {
                    symbolRels.Add(new SymbolRelationship
                    {
                        FromSymbol = symbol.Name,
                        ToSymbol = baseSymbol.Name,
                        Type = RelationshipType.Inherits,
                        FilePath = symbol.FilePath,
                        LineNumber = symbol.LineNumber
                    });
                }
            }

            // Buscar implementações de interface
            foreach (var iface in symbol.ImplementedInterfaces)
            {
                var ifaceName = iface.Split('.').Last().Split('<').First();
                var ifaceSymbol = index.Symbols.FirstOrDefault(s => s.Name == ifaceName);
                if (ifaceSymbol != null)
                {
                    symbolRels.Add(new SymbolRelationship
                    {
                        FromSymbol = symbol.Name,
                        ToSymbol = ifaceSymbol.Name,
                        Type = RelationshipType.Implements,
                        FilePath = symbol.FilePath,
                        LineNumber = symbol.LineNumber
                    });
                }
            }

            if (symbolRels.Any())
            {
                relationships[symbol.Name] = symbolRels;
            }
        }

        return relationships;
    }

    private List<string> ExtractSearchTerms(JiraTicket ticket)
    {
        var terms = new List<string>();

        // Extrair palavras-chave do título
        var titleWords = ticket.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Take(5);
        terms.AddRange(titleWords);

        // Adicionar labels
        terms.AddRange(ticket.Labels);

        // Adicionar componentes
        terms.AddRange(ticket.Components);

        return terms.Distinct().ToList();
    }

    private double CalculateRelevanceScore(CodeSymbol symbol, JiraTicket ticket, List<string> searchTerms)
    {
        double score = 0;

        // Nome exato
        if (searchTerms.Any(t => symbol.Name.Equals(t, StringComparison.OrdinalIgnoreCase)))
            score += 10;

        // Nome contém termo
        foreach (var term in searchTerms)
        {
            if (symbol.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 5;
            if (symbol.Namespace.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 2;
        }

        // Componentes do ticket
        if (ticket.Components.Any(c => symbol.Name.Contains(c, StringComparison.OrdinalIgnoreCase)))
            score += 3;

        return score;
    }

    private string BuildStructuredContext(CodeContext context, CodeIndex index)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Estrutura do Projeto Relevante\n");

        // Agrupar por namespace
        var byNamespace = context.RelevantSymbols
            .GroupBy(s => s.Namespace)
            .OrderByDescending(g => g.Count());

        foreach (var nsGroup in byNamespace)
        {
            sb.AppendLine($"### Namespace: {nsGroup.Key}\n");

            foreach (var symbol in nsGroup)
            {
                sb.AppendLine($"#### {symbol.Kind}: {symbol.Name}");
                sb.AppendLine($"- Arquivo: {symbol.FilePath}");
                sb.AppendLine($"- Linha: {symbol.LineNumber}");

                if (!string.IsNullOrEmpty(symbol.BaseType))
                    sb.AppendLine($"- Herda de: {symbol.BaseType}");

                if (symbol.ImplementedInterfaces.Any())
                    sb.AppendLine($"- Implementa: {string.Join(", ", symbol.ImplementedInterfaces)}");

                if (symbol.Methods.Any())
                    sb.AppendLine($"- Métodos: {string.Join(", ", symbol.Methods.Take(5))}");

                sb.AppendLine();
            }
        }

        // Relacionamentos
        if (context.Relationships.Any())
        {
            sb.AppendLine("## Relacionamentos\n");
            foreach (var rel in context.Relationships.Take(10))
            {
                sb.AppendLine($"- {rel.FromSymbol} {rel.Type} {rel.ToSymbol} ({rel.FilePath}:{rel.LineNumber})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private bool ShouldIgnoreFile(string filePath)
    {
        return _workflowSettings.IgnorePatterns.Any(pattern =>
            filePath.Contains(pattern.TrimEnd('/', '*'), StringComparison.OrdinalIgnoreCase));
    }

    private ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Referências básicas do .NET
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(System.Console).Assembly,
            typeof(System.Linq.Enumerable).Assembly,
            typeof(System.Collections.Generic.List<>).Assembly
        };

        foreach (var assembly in assemblies)
        {
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        return references.ToImmutableArray();
    }

    #endregion
}
