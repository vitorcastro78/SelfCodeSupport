using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;
using SelfCodeSupport.Infrastructure.Data;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Serviço para gerenciamento de configurações
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService(
        ApplicationDbContext context,
        ILogger<SettingsService> logger)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    #region Application Settings

    public async Task<ApplicationSettings> GetApplicationSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.ApplicationSettings.FindAsync([1], cancellationToken);
        
        if (settings == null)
        {
            // Criar configurações padrão se não existirem
            settings = new ApplicationSettings
            {
                Id = 1,
                ApplicationName = "SelfCodeSupport",
                Version = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ApplicationSettings.Add(settings);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    public async Task<ApplicationSettings> UpdateApplicationSettingsAsync(
        ApplicationSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        settings.Id = 1; // Sempre ID 1
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = updatedBy;

        var existing = await _context.ApplicationSettings.FindAsync([1], cancellationToken);
        if (existing == null)
        {
            _context.ApplicationSettings.Add(settings);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(settings);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Configurações globais atualizadas por {UpdatedBy}", updatedBy);

        return settings;
    }

    public async Task<JiraSettings> GetJiraSettingsAsync(CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(appSettings.JiraSettingsJson))
            return new JiraSettings();

        return JsonSerializer.Deserialize<JiraSettings>(appSettings.JiraSettingsJson, _jsonOptions) ?? new JiraSettings();
    }

    public async Task<JiraSettings> UpdateJiraSettingsAsync(
        JiraSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        appSettings.JiraSettingsJson = JsonSerializer.Serialize(settings, _jsonOptions);
        await UpdateApplicationSettingsAsync(appSettings, updatedBy, cancellationToken);
        return settings;
    }

    public async Task<GitSettings> GetGitSettingsAsync(CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(appSettings.GitSettingsJson))
            return new GitSettings();

        return JsonSerializer.Deserialize<GitSettings>(appSettings.GitSettingsJson, _jsonOptions) ?? new GitSettings();
    }

    public async Task<GitSettings> UpdateGitSettingsAsync(
        GitSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        appSettings.GitSettingsJson = JsonSerializer.Serialize(settings, _jsonOptions);
        await UpdateApplicationSettingsAsync(appSettings, updatedBy, cancellationToken);
        return settings;
    }

    public async Task<AnthropicSettings> GetAnthropicSettingsAsync(CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(appSettings.AnthropicSettingsJson))
            return new AnthropicSettings();

        return JsonSerializer.Deserialize<AnthropicSettings>(appSettings.AnthropicSettingsJson, _jsonOptions) ?? new AnthropicSettings();
    }

    public async Task<AnthropicSettings> UpdateAnthropicSettingsAsync(
        AnthropicSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        appSettings.AnthropicSettingsJson = JsonSerializer.Serialize(settings, _jsonOptions);
        await UpdateApplicationSettingsAsync(appSettings, updatedBy, cancellationToken);
        return settings;
    }

    public async Task<WorkflowSettings> GetWorkflowSettingsAsync(CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(appSettings.WorkflowSettingsJson))
            return new WorkflowSettings();

        return JsonSerializer.Deserialize<WorkflowSettings>(appSettings.WorkflowSettingsJson, _jsonOptions) ?? new WorkflowSettings();
    }

    public async Task<WorkflowSettings> UpdateWorkflowSettingsAsync(
        WorkflowSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var appSettings = await GetApplicationSettingsAsync(cancellationToken);
        appSettings.WorkflowSettingsJson = JsonSerializer.Serialize(settings, _jsonOptions);
        await UpdateApplicationSettingsAsync(appSettings, updatedBy, cancellationToken);
        return settings;
    }

    #endregion

    #region Project Settings

    public async Task<IEnumerable<ProjectSettings>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProjectSettings
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectSettings?> GetProjectByIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return await _context.ProjectSettings.FindAsync([projectId], cancellationToken);
    }

    public async Task<ProjectSettings?> GetProjectByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.ProjectSettings
            .FirstOrDefaultAsync(p => p.Name == name && p.IsActive, cancellationToken);
    }

    public async Task<ProjectSettings?> GetDefaultProjectAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProjectSettings
            .FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, cancellationToken);
    }

    public async Task<ProjectSettings> CreateProjectAsync(
        ProjectSettings project,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = createdBy;

        // Se for o primeiro projeto ou marcado como padrão, definir como padrão
        var hasDefault = await _context.ProjectSettings.AnyAsync(p => p.IsDefault, cancellationToken);
        if (!hasDefault || project.IsDefault)
        {
            // Remover padrão de outros projetos se este for marcado como padrão
            if (project.IsDefault)
            {
                await RemoveDefaultFromAllProjectsAsync(cancellationToken);
            }
        }
        else
        {
            project.IsDefault = false;
        }

        _context.ProjectSettings.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Projeto {ProjectName} criado por {CreatedBy}", project.Name, createdBy);
        return project;
    }

    public async Task<ProjectSettings> UpdateProjectAsync(
        ProjectSettings project,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.ProjectSettings.FindAsync([project.Id], cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Projeto com ID {project.Id} não encontrado");
        }

        // Se está marcando como padrão, remover de outros
        if (project.IsDefault && !existing.IsDefault)
        {
            await RemoveDefaultFromAllProjectsAsync(cancellationToken);
        }

        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = updatedBy;

        _context.Entry(existing).CurrentValues.SetValues(project);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Projeto {ProjectName} atualizado por {UpdatedBy}", project.Name, updatedBy);
        return project;
    }

    public async Task SetDefaultProjectAsync(int projectId, string updatedBy, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            throw new InvalidOperationException($"Projeto com ID {projectId} não encontrado");
        }

        await RemoveDefaultFromAllProjectsAsync(cancellationToken);

        project.IsDefault = true;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Projeto {ProjectName} definido como padrão por {UpdatedBy}", project.Name, updatedBy);
    }

    public async Task DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            throw new InvalidOperationException($"Projeto com ID {projectId} não encontrado");
        }

        // Soft delete - apenas marca como inativo
        project.IsActive = false;
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Projeto {ProjectName} deletado (soft delete)", project.Name);
    }

    public async Task<JiraSettings?> GetProjectJiraSettingsAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null || string.IsNullOrEmpty(project.JiraSettingsJson))
            return null;

        return JsonSerializer.Deserialize<JiraSettings>(project.JiraSettingsJson, _jsonOptions);
    }

    public async Task<JiraSettings> UpdateProjectJiraSettingsAsync(
        int projectId,
        JiraSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            throw new InvalidOperationException($"Projeto com ID {projectId} não encontrado");
        }

        project.JiraSettingsJson = JsonSerializer.Serialize(settings, _jsonOptions);
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Configurações JIRA do projeto {ProjectName} atualizadas", project.Name);

        return settings;
    }

    public async Task<GitSettings?> GetProjectGitSettingsAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null || string.IsNullOrEmpty(project.GitSettingsJson))
            return null;

        return JsonSerializer.Deserialize<GitSettings>(project.GitSettingsJson, _jsonOptions);
    }

    public async Task<GitSettings> UpdateProjectGitSettingsAsync(
        int projectId,
        GitSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            throw new InvalidOperationException($"Projeto com ID {projectId} não encontrado");
        }

        project.GitSettingsJson = JsonSerializer.Serialize(settings, _jsonOptions);
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Configurações Git do projeto {ProjectName} atualizadas", project.Name);

        return settings;
    }

    public async Task<WorkflowSettings?> GetProjectWorkflowSettingsAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null || string.IsNullOrEmpty(project.WorkflowSettingsJson))
            return null;

        return JsonSerializer.Deserialize<WorkflowSettings>(project.WorkflowSettingsJson, _jsonOptions);
    }

    public async Task<WorkflowSettings> UpdateProjectWorkflowSettingsAsync(
        int projectId,
        WorkflowSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            throw new InvalidOperationException($"Projeto com ID {projectId} não encontrado");
        }

        project.WorkflowSettingsJson = JsonSerializer.Serialize(settings, _jsonOptions);
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Configurações de Workflow do projeto {ProjectName} atualizadas", project.Name);

        return settings;
    }

    #endregion

    #region Private Helpers

    private async Task RemoveDefaultFromAllProjectsAsync(CancellationToken cancellationToken)
    {
        var defaultProjects = await _context.ProjectSettings
            .Where(p => p.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var project in defaultProjects)
        {
            project.IsDefault = false;
        }
    }

    #endregion
}
