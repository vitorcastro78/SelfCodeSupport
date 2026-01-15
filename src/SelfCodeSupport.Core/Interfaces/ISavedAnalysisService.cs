using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Service for saving and retrieving saved analyses (user-initiated saves)
/// </summary>
public interface ISavedAnalysisService
{
    /// <summary>
    /// Saves an analysis for a ticket (user-initiated save)
    /// </summary>
    /// <param name="ticketId">JIRA ticket ID</param>
    /// <param name="analysis">Analysis result to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Saved analysis with save metadata</returns>
    Task<SavedAnalysis> SaveAnalysisAsync(string ticketId, AnalysisResult analysis, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved analyses for a ticket
    /// </summary>
    /// <param name="ticketId">JIRA ticket ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of saved analyses</returns>
    Task<List<SavedAnalysis>> GetSavedAnalysesAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific saved analysis by ID
    /// </summary>
    /// <param name="analysisId">Saved analysis ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Saved analysis or null if not found</returns>
    Task<SavedAnalysis?> GetSavedAnalysisAsync(string analysisId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved analyses across all tickets
    /// </summary>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of saved analyses</returns>
    Task<List<SavedAnalysis>> GetAllSavedAnalysesAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saved analysis
    /// </summary>
    /// <param name="analysisId">Saved analysis ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSavedAnalysisAsync(string analysisId, CancellationToken cancellationToken = default);
}
