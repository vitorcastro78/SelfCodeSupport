using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;
using SelfCodeSupport.Infrastructure.Data;
using SelfCodeSupport.Infrastructure.Data.Entities;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Service for saving and retrieving saved analyses using database
/// </summary>
public class SavedAnalysisService : ISavedAnalysisService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<SavedAnalysisService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SavedAnalysisService(
        ApplicationDbContext dbContext,
        ILogger<SavedAnalysisService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<SavedAnalysis> SaveAnalysisAsync(string ticketId, AnalysisResult analysis, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new SavedAnalysisEntity
            {
                Id = Guid.NewGuid().ToString(),
                TicketId = ticketId,
                TicketTitle = analysis.TicketId, // Will be updated from ticket if available
                AnalysisJson = JsonSerializer.Serialize(analysis, _jsonOptions),
                SavedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SavedAnalyses.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Analysis saved for ticket {TicketId} with ID {AnalysisId}", ticketId, entity.Id);
            
            return MapToSavedAnalysis(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving analysis for ticket {TicketId}", ticketId);
            throw;
        }
    }

    public async Task<List<SavedAnalysis>> GetSavedAnalysesAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _dbContext.SavedAnalyses
                .Where(a => a.TicketId == ticketId)
                .OrderByDescending(a => a.SavedAt)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToSavedAnalysis).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved analyses for ticket {TicketId}", ticketId);
            return new List<SavedAnalysis>();
        }
    }

    public async Task<SavedAnalysis?> GetSavedAnalysisAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbContext.SavedAnalyses
                .FirstOrDefaultAsync(a => a.Id == analysisId, cancellationToken);

            if (entity == null)
            {
                return null;
            }

            return MapToSavedAnalysis(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved analysis {AnalysisId}", analysisId);
            return null;
        }
    }

    public async Task<List<SavedAnalysis>> GetAllSavedAnalysesAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _dbContext.SavedAnalyses
                .OrderByDescending(a => a.SavedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToSavedAnalysis).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all saved analyses");
            return new List<SavedAnalysis>();
        }
    }

    public async Task DeleteSavedAnalysisAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbContext.SavedAnalyses
                .FirstOrDefaultAsync(a => a.Id == analysisId, cancellationToken);

            if (entity != null)
            {
                _dbContext.SavedAnalyses.Remove(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Saved analysis {AnalysisId} deleted", analysisId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting saved analysis {AnalysisId}", analysisId);
            throw;
        }
    }

    private SavedAnalysis MapToSavedAnalysis(SavedAnalysisEntity entity)
    {
        var analysis = JsonSerializer.Deserialize<AnalysisResult>(entity.AnalysisJson, _jsonOptions) 
            ?? new AnalysisResult();

        return new SavedAnalysis
        {
            Id = entity.Id,
            TicketId = entity.TicketId,
            TicketTitle = entity.TicketTitle,
            Analysis = analysis,
            SavedAt = entity.SavedAt,
            Notes = entity.Notes,
            SentToJira = entity.SentToJira,
            UsedForImplementation = entity.UsedForImplementation
        };
    }
}
