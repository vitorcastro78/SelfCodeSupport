using Microsoft.EntityFrameworkCore;
using SelfCodeSupport.Core.Models;
using SelfCodeSupport.Infrastructure.Data.Entities;

namespace SelfCodeSupport.Infrastructure.Data;

/// <summary>
/// DbContext para configurações da aplicação e dados persistentes
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Configurações
    public DbSet<ApplicationSettings> ApplicationSettings { get; set; }
    public DbSet<ProjectSettings> ProjectSettings { get; set; }

    // Dados persistentes
    public DbSet<SavedAnalysisEntity> SavedAnalyses { get; set; }
    public DbSet<AnalysisCacheEntity> AnalysisCache { get; set; }
    public DbSet<WorkflowEntity> Workflows { get; set; }
    public DbSet<WorkflowProgressEntity> WorkflowProgress { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationSettings - sempre ID 1
        modelBuilder.Entity<ApplicationSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever(); // Não auto-incrementa
            entity.HasData(new ApplicationSettings
            {
                Id = 1,
                ApplicationName = "SelfCodeSupport",
                Version = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        });

        // ProjectSettings
        modelBuilder.Entity<ProjectSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.JiraProjectKey);
            entity.HasIndex(e => e.IsDefault);
            entity.HasIndex(e => e.IsActive);
        });

        // SavedAnalyses
        modelBuilder.Entity<SavedAnalysisEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.SavedAt);
            entity.HasIndex(e => e.CreatedAt);
        });

        // AnalysisCache
        modelBuilder.Entity<AnalysisCacheEntity>(entity =>
        {
            entity.HasKey(e => e.CacheKey);
            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.TicketHash);
            entity.HasIndex(e => e.LastAccessedAt);
            entity.HasIndex(e => e.ExpiresAt);
        });

        // Workflows
        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.HasKey(e => e.TicketId);
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.LastUpdatedAt);
        });

        // WorkflowProgress
        modelBuilder.Entity<WorkflowProgressEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TicketId, e.Timestamp });
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });
    }
}
