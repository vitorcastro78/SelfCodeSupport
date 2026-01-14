using Microsoft.AspNetCore.Mvc;
using SelfCodeSupport.Core.Interfaces;

namespace SelfCodeSupport.API.Controllers;

/// <summary>
/// Controller para verificação de saúde da aplicação e suas integrações
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IJiraService _jiraService;
    private readonly IAnthropicService _anthropicService;
    private readonly IPullRequestService _pullRequestService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IJiraService jiraService,
        IAnthropicService anthropicService,
        IPullRequestService pullRequestService,
        ILogger<HealthController> logger)
    {
        _jiraService = jiraService;
        _anthropicService = anthropicService;
        _pullRequestService = pullRequestService;
        _logger = logger;
    }

    /// <summary>
    /// Verifica a saúde básica da aplicação
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }

    /// <summary>
    /// Verifica a saúde detalhada incluindo todas as integrações
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DetailedHealthResponse>> GetDetailedHealth(CancellationToken cancellationToken)
    {
        var response = new DetailedHealthResponse
        {
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        };

        // Verificar JIRA
        try
        {
            response.Integrations["Jira"] = new IntegrationHealth
            {
                IsHealthy = await _jiraService.TestConnectionAsync(cancellationToken),
                Message = "Conexão verificada"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar conexão com JIRA");
            response.Integrations["Jira"] = new IntegrationHealth
            {
                IsHealthy = false,
                Message = ex.Message
            };
        }

        // Verificar Anthropic
        try
        {
            response.Integrations["Anthropic"] = new IntegrationHealth
            {
                IsHealthy = await _anthropicService.TestConnectionAsync(cancellationToken),
                Message = "Conexão verificada"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar conexão com Anthropic");
            response.Integrations["Anthropic"] = new IntegrationHealth
            {
                IsHealthy = false,
                Message = ex.Message
            };
        }

        // Verificar GitHub
        try
        {
            response.Integrations["GitHub"] = new IntegrationHealth
            {
                IsHealthy = await _pullRequestService.TestConnectionAsync(cancellationToken),
                Message = "Conexão verificada"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar conexão com GitHub");
            response.Integrations["GitHub"] = new IntegrationHealth
            {
                IsHealthy = false,
                Message = ex.Message
            };
        }

        response.Status = response.Integrations.Values.All(i => i.IsHealthy) 
            ? "Healthy" 
            : response.Integrations.Values.Any(i => i.IsHealthy) 
                ? "Degraded" 
                : "Unhealthy";

        return Ok(response);
    }
}

/// <summary>
/// Resposta básica de health check
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Resposta detalhada de health check
/// </summary>
public class DetailedHealthResponse : HealthResponse
{
    public Dictionary<string, IntegrationHealth> Integrations { get; set; } = new();
}

/// <summary>
/// Status de saúde de uma integração
/// </summary>
public class IntegrationHealth
{
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
}
