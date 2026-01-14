using Microsoft.AspNetCore.Mvc;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.API.Controllers;

/// <summary>
/// Controller para gerenciamento do workflow de desenvolvimento automatizado
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowOrchestrator _orchestrator;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowOrchestrator orchestrator,
        ILogger<WorkflowController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Inicia o workflow completo para um ticket JIRA
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA (ex: PROJ-1234)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do workflow</returns>
    /// <response code="200">Workflow iniciado com sucesso</response>
    /// <response code="400">ID do ticket inválido</response>
    /// <response code="500">Erro interno durante o workflow</response>
    [HttpPost("start/{ticketId}")]
    [ProducesResponseType(typeof(WorkflowResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkflowResult>> StartWorkflow(
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

        _logger.LogInformation("Iniciando workflow para ticket {TicketId}", ticketId);

        try
        {
            var result = await _orchestrator.StartWorkflowAsync(ticketId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar workflow para ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro no workflow",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Executa apenas a fase de análise para um ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da análise</returns>
    [HttpPost("analyze/{ticketId}")]
    [ProducesResponseType(typeof(AnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisResult>> Analyze(
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

        _logger.LogInformation("Iniciando análise para ticket {TicketId}", ticketId);

        try
        {
            var result = await _orchestrator.AnalyzeAsync(ticketId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na análise do ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro na análise",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Aprova a análise e inicia a implementação
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da implementação</returns>
    [HttpPost("approve/{ticketId}")]
    [ProducesResponseType(typeof(ImplementationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ImplementationResult>> ApproveAndImplement(
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

        _logger.LogInformation("Aprovando implementação para ticket {TicketId}", ticketId);

        try
        {
            var result = await _orchestrator.ApproveAndImplementAsync(ticketId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("análise pendente"))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Análise não encontrada",
                Detail = $"Nenhuma análise pendente encontrada para o ticket {ticketId}. Execute a análise primeiro."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na implementação do ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro na implementação",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Solicita revisão da análise com feedback
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="request">Feedback para revisão</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Nova análise revisada</returns>
    [HttpPost("revise/{ticketId}")]
    [ProducesResponseType(typeof(AnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisResult>> RequestRevision(
        string ticketId,
        [FromBody] RevisionRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Feedback))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Feedback inválido",
                Detail = "O feedback para revisão é obrigatório"
            });
        }

        _logger.LogInformation("Solicitando revisão para ticket {TicketId}", ticketId);

        try
        {
            var result = await _orchestrator.RequestRevisionAsync(ticketId, request.Feedback, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na revisão do ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro na revisão",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Cancela o workflow em andamento
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="request">Motivo do cancelamento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    [HttpPost("cancel/{ticketId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelWorkflow(
        string ticketId,
        [FromBody] CancelRequest request,
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

        _logger.LogInformation("Cancelando workflow para ticket {TicketId}", ticketId);

        try
        {
            await _orchestrator.CancelWorkflowAsync(ticketId, request.Reason ?? "Cancelado pelo usuário", cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar workflow do ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro ao cancelar",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtém o status atual do workflow para um ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <returns>Status do workflow</returns>
    [HttpGet("status/{ticketId}")]
    [ProducesResponseType(typeof(WorkflowStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowStatus>> GetStatus(string ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ID do ticket inválido",
                Detail = "O ID do ticket JIRA é obrigatório"
            });
        }

        var status = await _orchestrator.GetWorkflowStatusAsync(ticketId);
        return Ok(status);
    }

    /// <summary>
    /// Obtém o histórico de workflows executados
    /// </summary>
    /// <param name="limit">Número máximo de resultados (padrão: 20)</param>
    /// <param name="offset">Número de resultados para pular (padrão: 0)</param>
    /// <returns>Histórico paginado de workflows</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedWorkflowHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedWorkflowHistoryResponse>> GetHistory(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0)
    {
        var history = await _orchestrator.GetWorkflowHistoryAsync(limit + offset);
        var items = history.Skip(offset).Take(limit).ToList();
        var total = history.Count();

        var response = new PagedWorkflowHistoryResponse
        {
            Items = items,
            Total = total,
            Limit = limit,
            Offset = offset
        };

        return Ok(response);
    }

    /// <summary>
    /// Obtém métricas dos workflows
    /// </summary>
    /// <returns>Métricas dos workflows</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(WorkflowMetricsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkflowMetricsResponse>> GetMetrics()
    {
        var history = await _orchestrator.GetWorkflowHistoryAsync(int.MaxValue);
        var workflows = history.ToList();

        var totalWorkflows = workflows.Count;
        var successfulWorkflows = workflows.Count(w => w.IsSuccess);
        var successRate = totalWorkflows > 0 
            ? (double)successfulWorkflows / totalWorkflows * 100 
            : 0;

        var completedWorkflows = workflows
            .Where(w => w.CompletedAt.HasValue && w.StartedAt != default)
            .ToList();

        var averageImplementationTime = completedWorkflows.Any()
            ? (int)completedWorkflows
                .Average(w => (w.CompletedAt!.Value - w.StartedAt).TotalSeconds)
            : 0;

        var prsCreatedToday = workflows
            .Count(w => !string.IsNullOrEmpty(w.PullRequestUrl) && 
                       w.CompletedAt.HasValue && 
                       w.CompletedAt.Value.Date == DateTime.UtcNow.Date);

        var response = new WorkflowMetricsResponse
        {
            TotalWorkflows = totalWorkflows,
            SuccessRate = Math.Round(successRate, 2),
            AverageImplementationTime = averageImplementationTime,
            PrsCreatedToday = prsCreatedToday
        };

        return Ok(response);
    }
}

/// <summary>
/// Request para solicitar revisão
/// </summary>
public class RevisionRequest
{
    /// <summary>
    /// Feedback detalhado para a revisão
    /// </summary>
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>
/// Request para cancelar workflow
/// </summary>
public class CancelRequest
{
    /// <summary>
    /// Motivo do cancelamento
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Resposta paginada do histórico de workflows
/// </summary>
public class PagedWorkflowHistoryResponse
{
    public List<WorkflowSummary> Items { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

/// <summary>
/// Resposta com métricas dos workflows
/// </summary>
public class WorkflowMetricsResponse
{
    public int TotalWorkflows { get; set; }
    public double SuccessRate { get; set; }
    public int AverageImplementationTime { get; set; }
    public int PrsCreatedToday { get; set; }
}
