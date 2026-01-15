using Microsoft.AspNetCore.SignalR;

namespace SelfCodeSupport.API.Hubs;

/// <summary>
/// SignalR Hub para notificações de progresso do workflow
/// </summary>
public class WorkflowHub : Hub
{
    /// <summary>
    /// Junta o cliente a um grupo específico do ticket para receber atualizações
    /// </summary>
    public async Task JoinTicketGroup(string ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
    }

    /// <summary>
    /// Remove o cliente de um grupo específico do ticket
    /// </summary>
    public async Task LeaveTicketGroup(string ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
    }
}
