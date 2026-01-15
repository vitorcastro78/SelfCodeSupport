using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SelfCodeSupport.Infrastructure.Data.Entities;

/// <summary>
/// Entity for saved analyses in database
/// </summary>
[Table("SavedAnalyses")]
public class SavedAnalysisEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(50)]
    public string TicketId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string TicketTitle { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "TEXT")]
    public string AnalysisJson { get; set; } = string.Empty;

    [Required]
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "TEXT")]
    public string? Notes { get; set; }

    public bool SentToJira { get; set; } = false;

    public bool UsedForImplementation { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
