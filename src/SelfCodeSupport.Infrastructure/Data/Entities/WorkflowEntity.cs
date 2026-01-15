using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SelfCodeSupport.Infrastructure.Data.Entities;

/// <summary>
/// Entity for workflows in database
/// </summary>
[Table("Workflows")]
public class WorkflowEntity
{
    [Key]
    [MaxLength(50)]
    public string TicketId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string TicketTitle { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CurrentPhase { get; set; } = "NotStarted";

    [Required]
    [MaxLength(50)]
    public string State { get; set; } = "Running";

    [Column(TypeName = "TEXT")]
    public string? AnalysisJson { get; set; }

    [Column(TypeName = "TEXT")]
    public string? ImplementationJson { get; set; }

    [Column(TypeName = "TEXT")]
    public string? PullRequestJson { get; set; }

    [Column(TypeName = "TEXT")]
    public string? ErrorsJson { get; set; }

    public bool IsSuccess { get; set; } = false;

    [Required]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
