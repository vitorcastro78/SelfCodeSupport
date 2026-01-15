using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;
using SelfCodeSupport.Infrastructure.Data;
using SelfCodeSupport.Infrastructure.Data.Entities;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Serviço de cache para análises usando banco de dados
/// </summary>
public class AnalysisCacheService : IAnalysisCacheService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AnalysisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnalysisCacheService(
        ApplicationDbContext dbContext,
        ILogger<AnalysisCacheService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<AnalysisResult?> GetCachedAnalysisAsync(string ticketId, string ticketHash)
    {
        try
        {
            var cacheKey = $"{ticketId}_{ticketHash}";
            var entity = await _dbContext.AnalysisCache
                .FirstOrDefaultAsync(c => c.CacheKey == cacheKey);

            if (entity == null)
            {
                return null;
            }

            // Verificar se expirou
            if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value < DateTime.UtcNow)
            {
                _dbContext.AnalysisCache.Remove(entity);
                await _dbContext.SaveChangesAsync();
                return null;
            }

            // Atualizar último acesso
            entity.LastAccessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var analysis = JsonSerializer.Deserialize<AnalysisResult>(entity.AnalysisJson, _jsonOptions);
            
            if (analysis != null)
            {
                _logger.LogInformation("Analysis found in cache for ticket {TicketId}", ticketId);
            }
            
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading analysis from cache for ticket {TicketId}", ticketId);
            return null;
        }
    }

    public async Task CacheAnalysisAsync(string ticketId, string ticketHash, AnalysisResult analysis)
    {
        try
        {
            var cacheKey = $"{ticketId}_{ticketHash}";
            var json = JsonSerializer.Serialize(analysis, _jsonOptions);

            var existing = await _dbContext.AnalysisCache
                .FirstOrDefaultAsync(c => c.CacheKey == cacheKey);

            if (existing != null)
            {
                // Atualizar cache existente
                existing.AnalysisJson = json;
                existing.CachedAt = DateTime.UtcNow;
                existing.LastAccessedAt = DateTime.UtcNow;
                // Cache não expira por padrão, mas pode ser configurado
            }
            else
            {
                // Criar novo cache
                var entity = new AnalysisCacheEntity
                {
                    CacheKey = cacheKey,
                    TicketId = ticketId,
                    TicketHash = ticketHash,
                    AnalysisJson = json,
                    CachedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow
                };
                _dbContext.AnalysisCache.Add(entity);
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Analysis cached for ticket {TicketId}", ticketId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching analysis for ticket {TicketId}", ticketId);
        }
    }

    public async Task<List<AnalysisResult>> FindSimilarAnalysesAsync(string ticketTitle, string ticketDescription, int limit = 3)
    {
        var similarAnalyses = new List<AnalysisResult>();
        
        try
        {
            var searchTerms = ExtractSearchTerms(ticketTitle, ticketDescription);
            
            // Buscar todos os caches (limitado para performance)
            var cacheEntities = await _dbContext.AnalysisCache
                .OrderByDescending(c => c.LastAccessedAt)
                .Take(100) // Limitar busca para performance
                .ToListAsync();

            foreach (var entity in cacheEntities)
            {
                try
                {
                    var analysis = JsonSerializer.Deserialize<AnalysisResult>(entity.AnalysisJson, _jsonOptions);
                    
                    if (analysis != null && IsSimilar(analysis, searchTerms))
                    {
                        similarAnalyses.Add(analysis);
                    }
                }
                catch
                {
                    // Ignorar caches corrompidos
                }
            }

            return similarAnalyses
                .OrderByDescending(a => CalculateSimilarityScore(a, searchTerms))
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for similar analyses");
            return similarAnalyses;
        }
    }

    private static List<string> ExtractSearchTerms(string title, string description)
    {
        var text = $"{title} {description}".ToLowerInvariant();
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Distinct()
            .ToList();
    }

    private static bool IsSimilar(AnalysisResult analysis, List<string> searchTerms)
    {
        // Verificar se há termos em comum com os arquivos afetados
        var affectedFileNames = analysis.AffectedFiles
            .Select(f => Path.GetFileNameWithoutExtension(f.Path).ToLowerInvariant())
            .ToList();

        return searchTerms.Any(term => 
            affectedFileNames.Any(f => f.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static double CalculateSimilarityScore(AnalysisResult analysis, List<string> searchTerms)
    {
        var score = 0.0;
        var affectedFileNames = analysis.AffectedFiles
            .Select(f => Path.GetFileNameWithoutExtension(f.Path).ToLowerInvariant())
            .ToList();

        foreach (var term in searchTerms)
        {
            if (affectedFileNames.Any(f => f.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                score += 1.0;
            }
        }

        return score / Math.Max(searchTerms.Count, 1);
    }
}
