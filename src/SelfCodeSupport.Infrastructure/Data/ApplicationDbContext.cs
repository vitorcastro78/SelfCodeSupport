using Microsoft.EntityFrameworkCore;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Infrastructure.Data;

/// <summary>
/// DbContext para configurações da aplicação
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationSettings> ApplicationSettings { get; set; }
    public DbSet<ProjectSettings> ProjectSettings { get; set; }

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
    }
}
