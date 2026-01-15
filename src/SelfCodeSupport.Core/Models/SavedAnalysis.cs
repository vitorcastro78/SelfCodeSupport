namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Represents a saved analysis (user-initiated save)
/// </summary>
public class SavedAnalysis
{
    /// <summary>
    /// Unique ID for the saved analysis
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// JIRA ticket ID
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// Ticket title at the time of save
    /// </summary>
    public string TicketTitle { get; set; } = string.Empty;

    /// <summary>
    /// The analysis result
    /// </summary>
    public AnalysisResult Analysis { get; set; } = new();

    /// <summary>
    /// When the analysis was saved
    /// </summary>
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional notes from the user
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this analysis has been sent to JIRA
    /// </summary>
    public bool SentToJira { get; set; } = false;

    /// <summary>
    /// Whether this analysis has been used for implementation
    /// </summary>
    public bool UsedForImplementation { get; set; } = false;
}
