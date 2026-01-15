using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Serviço de cache para análises (economiza créditos da Anthropic)
/// </summary>
public interface IAnalysisCacheService
{
    /// <summary>
    /// Verifica se existe análise em cache para um ticket
    /// </summary>
    Task<AnalysisResult?> GetCachedAnalysisAsync(string ticketId, string ticketHash);

    /// <summary>
    /// Armazena análise em cache
    /// </summary>
    Task CacheAnalysisAsync(string ticketId, string ticketHash, AnalysisResult analysis);

    /// <summary>
    /// Busca análises similares que podem ser reutilizadas
    /// </summary>
    Task<List<AnalysisResult>> FindSimilarAnalysesAsync(string ticketTitle, string ticketDescription, int limit = 3);
}
