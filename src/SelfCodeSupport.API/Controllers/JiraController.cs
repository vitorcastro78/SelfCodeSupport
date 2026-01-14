using Microsoft.AspNetCore.Mvc;
using SelfCodeSupport.Core.Interfaces;

namespace SelfCodeSupport.API.Controllers;

/// <summary>
/// Controller para operações com JIRA
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JiraController : ControllerBase
{
    private readonly IJiraService _jiraService;
    private readonly ILogger<JiraController> _logger;

    public JiraController(
        IJiraService jiraService,
        ILogger<JiraController> logger)
    {
        _jiraService = jiraService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém informações de um ticket JIRA
    /// </summary>
    /// <param name="ticketId">ID do ticket (ex: PAC-892)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações do ticket</returns>
    /// <response code="200">Ticket encontrado</response>
    /// <response code="404">Ticket não encontrado</response>
    /// <response code="500">Erro ao buscar ticket</response>
    [HttpGet("ticket/{ticketId}")]
    [ProducesResponseType(typeof(JiraTicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JiraTicketResponse>> GetTicket(
        string ticketId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ID do ticket inválido",
                Detail = "O ID do ticket JIRA é obrigatório"
            });
        }

        _logger.LogInformation("Buscando ticket {TicketId} do JIRA", ticketId);

        try
        {
            var ticket = await _jiraService.GetTicketAsync(ticketId, cancellationToken);

            var response = new JiraTicketResponse
            {
                Id = ticket.Id,
                Key = ticket.Id,
                Title = ticket.Title,
                Description = ticket.Description,
                Type = ticket.Type.ToString(),
                Priority = ticket.Priority.ToString(),
                Assignee = ticket.Assignee,
                Reporter = ticket.Reporter,
                Labels = ticket.Labels,
                Created = ticket.CreatedAt,
                Updated = ticket.UpdatedAt,
                Status = ticket.Status,
                Url = ticket.Url,
                Components = ticket.Components,
                AcceptanceCriteria = ticket.AcceptanceCriteria
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("404") || ex.Message.Contains("não encontrado"))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Ticket não encontrado",
                Detail = $"O ticket {ticketId} não foi encontrado no JIRA"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro ao buscar ticket",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtém todos os tickets de um projeto JIRA
    /// </summary>
    /// <param name="projectKey">Chave do projeto (ex: PAC)</param>
    /// <param name="status">Filtrar por status (opcional)</param>
    /// <param name="type">Filtrar por tipo (opcional: Bug, Story, Task, Epic, Sub-task)</param>
    /// <param name="assignee">Filtrar por responsável (opcional)</param>
    /// <param name="maxResults">Número máximo de resultados (padrão: 100, máximo: 1000)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de tickets do projeto</returns>
    /// <response code="200">Lista de tickets retornada com sucesso</response>
    /// <response code="400">Parâmetros inválidos</response>
    /// <response code="500">Erro ao buscar tickets</response>
    [HttpGet("project/{projectKey}/tickets")]
    [ProducesResponseType(typeof(PagedJiraTicketsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedJiraTicketsResponse>> GetProjectTickets(
        string projectKey,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? assignee = null,
        [FromQuery] int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chave do projeto inválida",
                Detail = "A chave do projeto JIRA é obrigatória"
            });
        }

        if (maxResults < 1 || maxResults > 1000)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "maxResults inválido",
                Detail = "maxResults deve estar entre 1 e 1000"
            });
        }

        _logger.LogInformation("Buscando tickets do projeto {ProjectKey} com filtros: status={Status}, type={Type}, assignee={Assignee}", 
            projectKey, status, type, assignee);

        try
        {
            // Construir JQL query
            var jql = $"project = {projectKey}";
            
            if (!string.IsNullOrWhiteSpace(status))
            {
                jql += $" AND status = \"{status}\"";
            }
            
            if (!string.IsNullOrWhiteSpace(type))
            {
                jql += $" AND issuetype = \"{type}\"";
            }
            
            if (!string.IsNullOrWhiteSpace(assignee))
            {
                jql += $" AND assignee = \"{assignee}\"";
            }

            jql += " ORDER BY created DESC";

            var tickets = await _jiraService.SearchTicketsAsync(jql, maxResults, cancellationToken);

            var ticketResponses = tickets.Select(ticket => new JiraTicketResponse
            {
                Id = ticket.Id,
                Key = ticket.Id,
                Title = ticket.Title,
                Description = ticket.Description,
                Type = ticket.Type.ToString(),
                Priority = ticket.Priority.ToString(),
                Assignee = ticket.Assignee,
                Reporter = ticket.Reporter,
                Labels = ticket.Labels,
                Created = ticket.CreatedAt,
                Updated = ticket.UpdatedAt,
                Status = ticket.Status,
                Url = ticket.Url,
                Components = ticket.Components,
                AcceptanceCriteria = ticket.AcceptanceCriteria
            }).ToList();

            var response = new PagedJiraTicketsResponse
            {
                Items = ticketResponses,
                Total = ticketResponses.Count,
                ProjectKey = projectKey
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar tickets do projeto {ProjectKey}", projectKey);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro ao buscar tickets",
                Detail = ex.Message
            });
        }
    }
}

/// <summary>
/// Resposta do ticket JIRA
/// </summary>
public class JiraTicketResponse
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Reporter { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = [];
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> Components { get; set; } = [];
    public List<string> AcceptanceCriteria { get; set; } = [];
}

/// <summary>
/// Resposta paginada de tickets JIRA
/// </summary>
public class PagedJiraTicketsResponse
{
    public List<JiraTicketResponse> Items { get; set; } = [];
    public int Total { get; set; }
    public string ProjectKey { get; set; } = string.Empty;
}
