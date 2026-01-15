using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Serviço de análise semântica de código (similar ao Cursor IDE)
/// </summary>
public interface ICodeAnalysisService
{
    /// <summary>
    /// Analisa o projeto e cria um índice semântico
    /// </summary>
    Task<CodeIndex> IndexProjectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca símbolos (classes, métodos, interfaces) relacionados a um termo
    /// </summary>
    Task<IEnumerable<CodeSymbol>> FindSymbolsAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encontra arquivos relacionados a um ticket baseado em análise semântica
    /// </summary>
    Task<CodeContext> BuildSemanticContextAsync(JiraTicket ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encontra relacionamentos entre símbolos (herança, implementação, referências)
    /// </summary>
    Task<IEnumerable<SymbolRelationship>> FindRelationshipsAsync(string symbolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a estrutura completa de um arquivo (classes, métodos, propriedades)
    /// </summary>
    Task<FileStructure> GetFileStructureAsync(string filePath, CancellationToken cancellationToken = default);
}
