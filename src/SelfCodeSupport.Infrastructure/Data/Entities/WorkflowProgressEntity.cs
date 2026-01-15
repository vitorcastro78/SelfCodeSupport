using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SelfCodeSupport.Infrastructure.Data.Entities;

/// <summary>
/// Entity for workflow progress tracking in database
/// </summary>
[Table("WorkflowProgress")]
public class WorkflowProgressEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string TicketId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Phase { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string State { get; set; } = string.Empty;

    public int ProgressPercentage { get; set; } = 0;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
