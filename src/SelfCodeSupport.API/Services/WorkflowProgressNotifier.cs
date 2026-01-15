using Microsoft.AspNetCore.SignalR;
using SelfCodeSupport.API.Hubs;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.API.Services;

/// <summary>
/// Service to notify frontend about workflow progress via SignalR
/// </summary>
public class WorkflowProgressNotifier
{
    private readonly IHubContext<WorkflowHub> _hubContext;
    private readonly ILogger<WorkflowProgressNotifier> _logger;

    public WorkflowProgressNotifier(
        IHubContext<WorkflowHub> hubContext,
        ILogger<WorkflowProgressNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Notifies workflow progress to all clients connected to the ticket
    /// </summary>
    public async Task NotifyProgressAsync(string ticketId, WorkflowPhase phase, int percentage, string message)
    {
        try
        {
            await _hubContext.Clients.Group($"ticket-{ticketId}").SendAsync("ProgressUpdate", new
            {
                TicketId = ticketId,
                Phase = phase.ToString(),
                ProgressPercentage = percentage,
                Message = message,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Progress notification sent for ticket {TicketId}: {Phase} ({Percentage}%)", 
                ticketId, phase, percentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending progress notification for ticket {TicketId}", ticketId);
        }
    }

    /// <summary>
    /// Notifies that analysis was completed
    /// </summary>
    public async Task NotifyAnalysisCompletedAsync(string ticketId, AnalysisResult analysis)
    {
        try
        {
            await _hubContext.Clients.Group($"ticket-{ticketId}").SendAsync("AnalysisCompleted", new
            {
                TicketId = ticketId,
                Analysis = analysis,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Analysis completed notification sent for ticket {TicketId}", ticketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending analysis completed notification for ticket {TicketId}", ticketId);
        }
    }

    /// <summary>
    /// Notifies that implementation was completed
    /// </summary>
    public async Task NotifyImplementationCompletedAsync(string ticketId, ImplementationResult implementation)
    {
        try
        {
            await _hubContext.Clients.Group($"ticket-{ticketId}").SendAsync("ImplementationCompleted", new
            {
                TicketId = ticketId,
                Implementation = implementation,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Implementation completed notification sent for ticket {TicketId}", ticketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending implementation completed notification for ticket {TicketId}", ticketId);
        }
    }

    /// <summary>
    /// Notifies about a workflow error
    /// </summary>
    public async Task NotifyErrorAsync(string ticketId, WorkflowPhase phase, string errorMessage)
    {
        try
        {
            await _hubContext.Clients.Group($"ticket-{ticketId}").SendAsync("WorkflowError", new
            {
                TicketId = ticketId,
                Phase = phase.ToString(),
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Error notification sent for ticket {TicketId}: {Error}", ticketId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending error notification for ticket {TicketId}", ticketId);
        }
    }
}
