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
    private readonly ISavedAnalysisService _savedAnalysisService;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowOrchestrator orchestrator,
        ISavedAnalysisService savedAnalysisService,
        ILogger<WorkflowController> logger)
    {
        _orchestrator = orchestrator;
        _savedAnalysisService = savedAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Cria um registro de workflow para um ticket sem iniciar a análise
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA (ex: PROJ-1234)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Workflow criado com status pending</returns>
    /// <response code="200">Workflow criado com sucesso</response>
    /// <response code="400">ID do ticket inválido</response>
    /// <response code="404">Ticket não encontrado no JIRA</response>
    /// <response code="500">Erro interno ao criar workflow</response>
    [HttpPost("create/{ticketId}")]
    [ProducesResponseType(typeof(WorkflowResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkflowResult>> CreateWorkflow(
        string ticketId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid ticket ID",
                Detail = "JIRA ticket ID is required"
            });
        }

        _logger.LogInformation("Creating workflow for ticket {TicketId}", ticketId);

        try
        {
            var result = await _orchestrator.CreateWorkflowAsync(ticketId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Could not fetch ticket"))
        {
            _logger.LogError(ex, "Ticket {TicketId} not found in JIRA", ticketId);
            return NotFound(new ProblemDetails
            {
                Title = "Ticket not found",
                Detail = $"Ticket {ticketId} was not found in JIRA"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow for ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Error creating workflow",
                Detail = ex.Message
            });
        }
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

    /// <summary>
    /// Envia a análise para o JIRA após revisão/modificação no frontend
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="request">Comentário da análise (pode ser modificado pelo frontend)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Status da operação</returns>
    [HttpPost("send-analysis/{ticketId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendAnalysisToJira(
        string ticketId,
        [FromBody] SendAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid ticket ID",
                Detail = "JIRA ticket ID is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid comment",
                Detail = "Analysis comment is required"
            });
        }

        _logger.LogInformation("Sending analysis to JIRA for ticket {TicketId}", ticketId);

        try
        {
            await _orchestrator.SendAnalysisToJiraAsync(ticketId, request.Comment, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending analysis to JIRA for ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Error sending analysis",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Salva a análise realizada para um ticket (permite decidir depois se envia para JIRA ou implementa)
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="request">Request com análise e notas opcionais</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Análise salva</returns>
    [HttpPost("save-analysis/{ticketId}")]
    [ProducesResponseType(typeof(SavedAnalysis), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SavedAnalysis>> SaveAnalysis(
        string ticketId,
        [FromBody] SaveAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid ticket ID",
                Detail = "JIRA ticket ID is required"
            });
        }

        if (request.Analysis == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Analysis is required"
            });
        }

        _logger.LogInformation("Saving analysis for ticket {TicketId}", ticketId);

        try
        {
            var savedAnalysis = await _savedAnalysisService.SaveAnalysisAsync(
                ticketId, 
                request.Analysis, 
                cancellationToken);

            // Atualizar notas se fornecidas
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                savedAnalysis.Notes = request.Notes;
                // Re-save with notes
                var filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SelfCodeSupport",
                    "saved-analyses",
                    $"{savedAnalysis.Id}.json");
                
                var json = System.Text.Json.JsonSerializer.Serialize(savedAnalysis, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                await System.IO.File.WriteAllTextAsync(filePath, json, cancellationToken);
            }

            return Ok(savedAnalysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving analysis for ticket {TicketId}", ticketId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Error saving analysis",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtém todas as análises salvas para um ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de análises salvas</returns>
    [HttpGet("saved-analyses/{ticketId}")]
    [ProducesResponseType(typeof(List<SavedAnalysis>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SavedAnalysis>>> GetSavedAnalyses(
        string ticketId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid ticket ID",
                Detail = "JIRA ticket ID is required"
            });
        }

        var savedAnalyses = await _savedAnalysisService.GetSavedAnalysesAsync(ticketId, cancellationToken);
        return Ok(savedAnalyses);
    }

    /// <summary>
    /// Obtém uma análise salva específica por ID
    /// </summary>
    /// <param name="analysisId">ID da análise salva</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Análise salva</returns>
    [HttpGet("saved-analyses/by-id/{analysisId}")]
    [ProducesResponseType(typeof(SavedAnalysis), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedAnalysis>> GetSavedAnalysis(
        string analysisId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(analysisId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid analysis ID",
                Detail = "Analysis ID is required"
            });
        }

        var savedAnalysis = await _savedAnalysisService.GetSavedAnalysisAsync(analysisId, cancellationToken);
        
        if (savedAnalysis == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Analysis not found",
                Detail = $"Saved analysis with ID {analysisId} was not found"
            });
        }

        return Ok(savedAnalysis);
    }

    /// <summary>
    /// Obtém todas as análises salvas (de todos os tickets)
    /// </summary>
    /// <param name="limit">Número máximo de resultados (padrão: 50)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de análises salvas</returns>
    [HttpGet("saved-analyses")]
    [ProducesResponseType(typeof(List<SavedAnalysis>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SavedAnalysis>>> GetAllSavedAnalyses(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var savedAnalyses = await _savedAnalysisService.GetAllSavedAnalysesAsync(limit, cancellationToken);
        return Ok(savedAnalyses);
    }

    /// <summary>
    /// Deleta uma análise salva
    /// </summary>
    /// <param name="analysisId">ID da análise salva</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    [HttpDelete("saved-analyses/{analysisId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSavedAnalysis(
        string analysisId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(analysisId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid analysis ID",
                Detail = "Analysis ID is required"
            });
        }

        try
        {
            await _savedAnalysisService.DeleteSavedAnalysisAsync(analysisId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting saved analysis {AnalysisId}", analysisId);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Error deleting analysis",
                Detail = ex.Message
            });
        }
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

/// <summary>
/// Request para enviar análise ao JIRA
/// </summary>
public class SendAnalysisRequest
{
    /// <summary>
    /// Comentário formatado da análise (pode ser modificado pelo frontend)
    /// </summary>
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// Request para salvar análise
/// </summary>
public class SaveAnalysisRequest
{
    /// <summary>
    /// Análise a ser salva
    /// </summary>
    public AnalysisResult Analysis { get; set; } = new();

    /// <summary>
    /// Notas opcionais sobre a análise
    /// </summary>
    public string? Notes { get; set; }
}
