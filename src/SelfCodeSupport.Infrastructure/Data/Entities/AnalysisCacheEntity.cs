using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SelfCodeSupport.Infrastructure.Data.Entities;

/// <summary>
/// Entity for analysis cache in database
/// </summary>
[Table("AnalysisCache")]
public class AnalysisCacheEntity
{
    [Key]
    [MaxLength(100)]
    public string CacheKey { get; set; } = string.Empty; // Format: {ticketId}_{ticketHash}

    [Required]
    [MaxLength(50)]
    public string TicketId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string TicketHash { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "TEXT")]
    public string AnalysisJson { get; set; } = string.Empty;

    [Required]
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}
