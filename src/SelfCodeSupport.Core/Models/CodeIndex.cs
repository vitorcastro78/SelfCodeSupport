namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Índice semântico do projeto
/// </summary>
public class CodeIndex
{
    public DateTime IndexedAt { get; set; }
    public List<CodeSymbol> Symbols { get; set; } = [];
    public Dictionary<string, FileStructure> Files { get; set; } = [];
    public Dictionary<string, List<SymbolRelationship>> Relationships { get; set; } = [];
}

/// <summary>
/// Símbolo de código (classe, método, interface, etc.)
/// </summary>
public class CodeSymbol
{
    public string Name { get; set; } = string.Empty;
    public SymbolKind Kind { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Namespace { get; set; } = string.Empty;
    public string? BaseType { get; set; }
    public List<string> ImplementedInterfaces { get; set; } = [];
    public List<string> Methods { get; set; } = [];
    public List<string> Properties { get; set; } = [];
    public string? Summary { get; set; }
    public List<string> Attributes { get; set; } = [];
}

/// <summary>
/// Tipo de símbolo
/// </summary>
public enum SymbolKind
{
    Class,
    Interface,
    Struct,
    Enum,
    Method,
    Property,
    Field,
    Event,
    Namespace
}

/// <summary>
/// Estrutura de um arquivo
/// </summary>
public class FileStructure
{
    public string FilePath { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<CodeSymbol> Classes { get; set; } = [];
    public List<CodeSymbol> Interfaces { get; set; } = [];
    public List<CodeSymbol> Methods { get; set; } = [];
    public List<string> Usings { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
}

/// <summary>
/// Relacionamento entre símbolos
/// </summary>
public class SymbolRelationship
{
    public string FromSymbol { get; set; } = string.Empty;
    public string ToSymbol { get; set; } = string.Empty;
    public RelationshipType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

/// <summary>
/// Tipo de relacionamento
/// </summary>
public enum RelationshipType
{
    Inherits,
    Implements,
    References,
    Calls,
    Uses,
    Contains
}

/// <summary>
/// Contexto semântico de código para análise
/// </summary>
public class CodeContext
{
    public List<RelevantFile> RelevantFiles { get; set; } = [];
    public List<CodeSymbol> RelevantSymbols { get; set; } = [];
    public List<SymbolRelationship> Relationships { get; set; } = [];
    public string StructuredContext { get; set; } = string.Empty;
    public Dictionary<string, string> FileContents { get; set; } = [];
}

/// <summary>
/// Arquivo relevante com contexto
/// </summary>
public class RelevantFile
{
    public string FilePath { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public List<string> Reasons { get; set; } = [];
    public FileStructure? Structure { get; set; }
}
