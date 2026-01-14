using Microsoft.AspNetCore.Mvc;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.API.Controllers;

/// <summary>
/// Controller para gerenciamento de configurações da aplicação e projetos
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsService settingsService,
        ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    #region Application Settings

    /// <summary>
    /// Obtém as configurações globais da aplicação
    /// </summary>
    [HttpGet("application")]
    [ProducesResponseType(typeof(ApplicationSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplicationSettings>> GetApplicationSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetApplicationSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Atualiza as configurações globais da aplicação
    /// </summary>
    [HttpPut("application")]
    [ProducesResponseType(typeof(ApplicationSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplicationSettings>> UpdateApplicationSettings(
        [FromBody] ApplicationSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        var updated = await _settingsService.UpdateApplicationSettingsAsync(settings, updatedBy, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Obtém configurações JIRA globais
    /// </summary>
    [HttpGet("application/jira")]
    [ProducesResponseType(typeof(JiraSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<JiraSettings>> GetJiraSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetJiraSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configurações JIRA globais
    /// </summary>
    [HttpPut("application/jira")]
    [ProducesResponseType(typeof(JiraSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<JiraSettings>> UpdateJiraSettings(
        [FromBody] JiraSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        var updated = await _settingsService.UpdateJiraSettingsAsync(settings, updatedBy, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Obtém configurações Git globais
    /// </summary>
    [HttpGet("application/git")]
    [ProducesResponseType(typeof(GitSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<GitSettings>> GetGitSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetGitSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configurações Git globais
    /// </summary>
    [HttpPut("application/git")]
    [ProducesResponseType(typeof(GitSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<GitSettings>> UpdateGitSettings(
        [FromBody] GitSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        var updated = await _settingsService.UpdateGitSettingsAsync(settings, updatedBy, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Obtém configurações Anthropic globais
    /// </summary>
    [HttpGet("application/anthropic")]
    [ProducesResponseType(typeof(AnthropicSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnthropicSettings>> GetAnthropicSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAnthropicSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configurações Anthropic globais
    /// </summary>
    [HttpPut("application/anthropic")]
    [ProducesResponseType(typeof(AnthropicSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnthropicSettings>> UpdateAnthropicSettings(
        [FromBody] AnthropicSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        var updated = await _settingsService.UpdateAnthropicSettingsAsync(settings, updatedBy, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Obtém configurações de Workflow globais
    /// </summary>
    [HttpGet("application/workflow")]
    [ProducesResponseType(typeof(WorkflowSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkflowSettings>> GetWorkflowSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetWorkflowSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configurações de Workflow globais
    /// </summary>
    [HttpPut("application/workflow")]
    [ProducesResponseType(typeof(WorkflowSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkflowSettings>> UpdateWorkflowSettings(
        [FromBody] WorkflowSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        var updated = await _settingsService.UpdateWorkflowSettingsAsync(settings, updatedBy, cancellationToken);
        return Ok(updated);
    }

    #endregion

    #region Project Settings

    /// <summary>
    /// Lista todos os projetos
    /// </summary>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(IEnumerable<ProjectSettings>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectSettings>>> GetAllProjects(CancellationToken cancellationToken)
    {
        var projects = await _settingsService.GetAllProjectsAsync(cancellationToken);
        return Ok(projects);
    }

    /// <summary>
    /// Obtém um projeto por ID
    /// </summary>
    [HttpGet("projects/{projectId}")]
    [ProducesResponseType(typeof(ProjectSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectSettings>> GetProjectById(int projectId, CancellationToken cancellationToken)
    {
        var project = await _settingsService.GetProjectByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = $"Projeto com ID {projectId} não foi encontrado"
            });
        }

        return Ok(project);
    }

    /// <summary>
    /// Obtém um projeto por nome
    /// </summary>
    [HttpGet("projects/name/{name}")]
    [ProducesResponseType(typeof(ProjectSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectSettings>> GetProjectByName(string name, CancellationToken cancellationToken)
    {
        var project = await _settingsService.GetProjectByNameAsync(name, cancellationToken);
        if (project == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = $"Projeto '{name}' não foi encontrado"
            });
        }

        return Ok(project);
    }

    /// <summary>
    /// Obtém o projeto padrão
    /// </summary>
    [HttpGet("projects/default")]
    [ProducesResponseType(typeof(ProjectSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectSettings>> GetDefaultProject(CancellationToken cancellationToken)
    {
        var project = await _settingsService.GetDefaultProjectAsync(cancellationToken);
        if (project == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto padrão não encontrado",
                Detail = "Nenhum projeto padrão foi configurado"
            });
        }

        return Ok(project);
    }

    /// <summary>
    /// Cria um novo projeto
    /// </summary>
    [HttpPost("projects")]
    [ProducesResponseType(typeof(ProjectSettings), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectSettings>> CreateProject(
        [FromBody] ProjectSettings project,
        [FromQuery] string createdBy = "system",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(project.Name))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Nome do projeto obrigatório",
                Detail = "O nome do projeto é obrigatório"
            });
        }

        var created = await _settingsService.CreateProjectAsync(project, createdBy, cancellationToken);
        return CreatedAtAction(nameof(GetProjectById), new { projectId = created.Id }, created);
    }

    /// <summary>
    /// Atualiza um projeto existente
    /// </summary>
    [HttpPut("projects/{projectId}")]
    [ProducesResponseType(typeof(ProjectSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectSettings>> UpdateProject(
        int projectId,
        [FromBody] ProjectSettings project,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        project.Id = projectId;
        
        try
        {
            var updated = await _settingsService.UpdateProjectAsync(project, updatedBy, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Define um projeto como padrão
    /// </summary>
    [HttpPost("projects/{projectId}/set-default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultProject(
        int projectId,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _settingsService.SetDefaultProjectAsync(projectId, updatedBy, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Deleta um projeto (soft delete)
    /// </summary>
    [HttpDelete("projects/{projectId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _settingsService.DeleteProjectAsync(projectId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtém configurações JIRA de um projeto
    /// </summary>
    [HttpGet("projects/{projectId}/jira")]
    [ProducesResponseType(typeof(JiraSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JiraSettings>> GetProjectJiraSettings(int projectId, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetProjectJiraSettingsAsync(projectId, cancellationToken);
        if (settings == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Configurações não encontradas",
                Detail = $"Configurações JIRA do projeto {projectId} não foram encontradas"
            });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configurações JIRA de um projeto
    /// </summary>
    [HttpPut("projects/{projectId}/jira")]
    [ProducesResponseType(typeof(JiraSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JiraSettings>> UpdateProjectJiraSettings(
        int projectId,
        [FromBody] JiraSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _settingsService.UpdateProjectJiraSettingsAsync(projectId, settings, updatedBy, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtém configurações Git de um projeto
    /// </summary>
    [HttpGet("projects/{projectId}/git")]
    [ProducesResponseType(typeof(GitSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GitSettings>> GetProjectGitSettings(int projectId, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetProjectGitSettingsAsync(projectId, cancellationToken);
        if (settings == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Configurações não encontradas",
                Detail = $"Configurações Git do projeto {projectId} não foram encontradas"
            });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configurações Git de um projeto
    /// </summary>
    [HttpPut("projects/{projectId}/git")]
    [ProducesResponseType(typeof(GitSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GitSettings>> UpdateProjectGitSettings(
        int projectId,
        [FromBody] GitSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _settingsService.UpdateProjectGitSettingsAsync(projectId, settings, updatedBy, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtém configurações de Workflow de um projeto
    /// </summary>
    [HttpGet("projects/{projectId}/workflow")]
    [ProducesResponseType(typeof(WorkflowSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowSettings>> GetProjectWorkflowSettings(int projectId, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetProjectWorkflowSettingsAsync(projectId, cancellationToken);
        if (settings == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Configurações não encontradas",
                Detail = $"Configurações de Workflow do projeto {projectId} não foram encontradas"
            });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configurações de Workflow de um projeto
    /// </summary>
    [HttpPut("projects/{projectId}/workflow")]
    [ProducesResponseType(typeof(WorkflowSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowSettings>> UpdateProjectWorkflowSettings(
        int projectId,
        [FromBody] WorkflowSettings settings,
        [FromQuery] string updatedBy = "system",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _settingsService.UpdateProjectWorkflowSettingsAsync(projectId, settings, updatedBy, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Projeto não encontrado",
                Detail = ex.Message
            });
        }
    }

    #endregion
}
