using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Otimiza contexto de código para reduzir tokens enviados à Anthropic
/// </summary>
public class ContextOptimizer
{
    private readonly ILogger<ContextOptimizer> _logger;

    public ContextOptimizer(ILogger<ContextOptimizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Otimiza contexto removendo partes desnecessárias e comprimindo
    /// </summary>
    public string OptimizeContext(string context, int maxSize = 30000)
    {
        if (context.Length <= maxSize)
        {
            return context;
        }

        _logger.LogInformation("Optimizing context: {OriginalSize} -> {MaxSize} characters", 
            context.Length, maxSize);

        var optimized = new StringBuilder();
        var lines = context.Split('\n');
        var currentSize = 0;

        // Remover linhas vazias excessivas
        var cleanedLines = RemoveExcessiveEmptyLines(lines);

        // Remover comentários longos (manter apenas os importantes)
        cleanedLines = RemoveExcessiveComments(cleanedLines);

        // Priorizar código relevante (métodos, classes, propriedades)
        foreach (var line in cleanedLines)
        {
            var lineSize = line.Length + 1; // +1 para newline

            if (currentSize + lineSize > maxSize)
            {
                optimized.AppendLine("\n// ... (contexto truncado para otimização)");
                break;
            }

            optimized.AppendLine(line);
            currentSize += lineSize;
        }

        var result = optimized.ToString();
        _logger.LogInformation("Context optimized: {OriginalSize} -> {OptimizedSize} characters ({Reduction}% reduction)",
            context.Length, result.Length, 
            Math.Round((1 - (double)result.Length / context.Length) * 100, 2));

        return result;
    }

    /// <summary>
    /// Cria resumo de arquivo grande ao invés de enviar código completo
    /// </summary>
    public string CreateFileSummary(string filePath, string content, int maxLines = 100)
    {
        var lines = content.Split('\n');
        
        if (lines.Length <= maxLines)
        {
            return content;
        }

        var summary = new StringBuilder();
        summary.AppendLine($"// File: {filePath} (resumido - {lines.Length} linhas totais)");
        summary.AppendLine("// Estrutura do arquivo:");
        
        // Extrair estrutura (classes, métodos, propriedades)
        var structure = ExtractStructure(lines);
        summary.AppendLine(structure);
        
        summary.AppendLine("\n// Primeiras linhas:");
        summary.AppendLine(string.Join("\n", lines.Take(20)));
        
        summary.AppendLine("\n// ... (código intermediário omitido) ...");
        
        summary.AppendLine("\n// Últimas linhas:");
        summary.AppendLine(string.Join("\n", lines.Skip(lines.Length - 20)));

        return summary.ToString();
    }

    private static string[] RemoveExcessiveEmptyLines(string[] lines)
    {
        var result = new List<string>();
        var emptyLineCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                emptyLineCount++;
                if (emptyLineCount <= 2) // Manter no máximo 2 linhas vazias consecutivas
                {
                    result.Add(line);
                }
            }
            else
            {
                emptyLineCount = 0;
                result.Add(line);
            }
        }

        return result.ToArray();
    }

    private static string[] RemoveExcessiveComments(string[] lines)
    {
        var result = new List<string>();
        var inCommentBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Detectar início/fim de blocos de comentário
            if (trimmed.StartsWith("/*"))
            {
                inCommentBlock = true;
                // Manter apenas primeira linha do bloco
                result.Add("// ... (comentário de bloco)");
            }
            else if (trimmed.EndsWith("*/") && inCommentBlock)
            {
                inCommentBlock = false;
                continue;
            }
            else if (inCommentBlock)
            {
                // Pular linhas dentro do bloco de comentário
                continue;
            }
            else if (trimmed.StartsWith("//") && trimmed.Length > 100)
            {
                // Truncar comentários muito longos
                result.Add(trimmed.Substring(0, 100) + " ...");
            }
            else
            {
                result.Add(line);
            }
        }

        return result.ToArray();
    }

    private static string ExtractStructure(string[] lines)
    {
        var structure = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Detectar declarações importantes
            if (Regex.IsMatch(trimmed, @"^(public|private|protected|internal)\s+(class|interface|struct|enum)"))
            {
                structure.AppendLine($"  {trimmed}");
            }
            else if (Regex.IsMatch(trimmed, @"^(public|private|protected|internal)\s+\w+\s+\w+\s*[\(=]"))
            {
                // Método ou propriedade
                var methodName = ExtractMethodName(trimmed);
                if (!string.IsNullOrEmpty(methodName))
                {
                    structure.AppendLine($"    {methodName}");
                }
            }
        }

        return structure.ToString();
    }

    private static string? ExtractMethodName(string line)
    {
        var match = Regex.Match(line, @"\s+(\w+)\s*\(");
        return match.Success ? match.Groups[1].Value : null;
    }
}
