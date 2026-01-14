using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Interface para serviço de gerenciamento de configurações
/// </summary>
public interface ISettingsService
{
    #region Application Settings

    /// <summary>
    /// Obtém as configurações globais da aplicação
    /// </summary>
    Task<ApplicationSettings> GetApplicationSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza as configurações globais da aplicação
    /// </summary>
    Task<ApplicationSettings> UpdateApplicationSettingsAsync(
        ApplicationSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém configurações JIRA globais
    /// </summary>
    Task<JiraSettings> GetJiraSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza configurações JIRA globais
    /// </summary>
    Task<JiraSettings> UpdateJiraSettingsAsync(
        JiraSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém configurações Git globais
    /// </summary>
    Task<GitSettings> GetGitSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza configurações Git globais
    /// </summary>
    Task<GitSettings> UpdateGitSettingsAsync(
        GitSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém configurações Anthropic globais
    /// </summary>
    Task<AnthropicSettings> GetAnthropicSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza configurações Anthropic globais
    /// </summary>
    Task<AnthropicSettings> UpdateAnthropicSettingsAsync(
        AnthropicSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém configurações de Workflow globais
    /// </summary>
    Task<WorkflowSettings> GetWorkflowSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza configurações de Workflow globais
    /// </summary>
    Task<WorkflowSettings> UpdateWorkflowSettingsAsync(
        WorkflowSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    #endregion

    #region Project Settings

    /// <summary>
    /// Lista todos os projetos
    /// </summary>
    Task<IEnumerable<ProjectSettings>> GetAllProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um projeto por ID
    /// </summary>
    Task<ProjectSettings?> GetProjectByIdAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um projeto por nome
    /// </summary>
    Task<ProjectSettings?> GetProjectByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o projeto padrão
    /// </summary>
    Task<ProjectSettings?> GetDefaultProjectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um novo projeto
    /// </summary>
    Task<ProjectSettings> CreateProjectAsync(
        ProjectSettings project,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza um projeto existente
    /// </summary>
    Task<ProjectSettings> UpdateProjectAsync(
        ProjectSettings project,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Define um projeto como padrão
    /// </summary>
    Task SetDefaultProjectAsync(int projectId, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deleta um projeto
    /// </summary>
    Task DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém configurações JIRA de um projeto
    /// </summary>
    Task<JiraSettings?> GetProjectJiraSettingsAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza configurações JIRA de um projeto
    /// </summary>
    Task<JiraSettings> UpdateProjectJiraSettingsAsync(
        int projectId,
        JiraSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém configurações Git de um projeto
    /// </summary>
    Task<GitSettings?> GetProjectGitSettingsAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza configurações Git de um projeto
    /// </summary>
    Task<GitSettings> UpdateProjectGitSettingsAsync(
        int projectId,
        GitSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém configurações de Workflow de um projeto
    /// </summary>
    Task<WorkflowSettings?> GetProjectWorkflowSettingsAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza configurações de Workflow de um projeto
    /// </summary>
    Task<WorkflowSettings> UpdateProjectWorkflowSettingsAsync(
        int projectId,
        WorkflowSettings settings,
        string updatedBy,
        CancellationToken cancellationToken = default);

    #endregion
}
