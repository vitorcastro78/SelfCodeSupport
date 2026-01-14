using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Interface para o orquestrador do fluxo de desenvolvimento
/// </summary>
public interface IWorkflowOrchestrator
{
    /// <summary>
    /// Inicia o fluxo completo de desenvolvimento para um ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do workflow</returns>
    Task<WorkflowResult> StartWorkflowAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa apenas a fase de análise
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da análise</returns>
    Task<AnalysisResult> AnalyzeAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aprova a análise e continua com a implementação
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da implementação</returns>
    Task<ImplementationResult> ApproveAndImplementAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita revisão da análise
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="feedback">Feedback para revisão</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Nova análise revisada</returns>
    Task<AnalysisResult> RequestRevisionAsync(string ticketId, string feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela o workflow em andamento
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="reason">Motivo do cancelamento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task CancelWorkflowAsync(string ticketId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o status atual do workflow
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <returns>Status do workflow</returns>
    Task<WorkflowStatus> GetWorkflowStatusAsync(string ticketId);

    /// <summary>
    /// Obtém o histórico de workflows
    /// </summary>
    /// <param name="limit">Limite de resultados</param>
    /// <returns>Lista de workflows</returns>
    Task<IEnumerable<WorkflowSummary>> GetWorkflowHistoryAsync(int limit = 20);

    /// <summary>
    /// Evento disparado quando a análise é concluída
    /// </summary>
    event EventHandler<AnalysisCompletedEventArgs>? AnalysisCompleted;

    /// <summary>
    /// Evento disparado quando a implementação é concluída
    /// </summary>
    event EventHandler<ImplementationCompletedEventArgs>? ImplementationCompleted;

    /// <summary>
    /// Evento disparado quando ocorre um erro
    /// </summary>
    event EventHandler<WorkflowErrorEventArgs>? WorkflowError;

    /// <summary>
    /// Evento disparado para atualização de progresso
    /// </summary>
    event EventHandler<WorkflowProgressEventArgs>? ProgressUpdated;
}

/// <summary>
/// Resultado completo do workflow
/// </summary>
public class WorkflowResult
{
    public string TicketId { get; set; } = string.Empty;
    public string TicketTitle { get; set; } = string.Empty;
    public WorkflowPhase FinalPhase { get; set; }
    public bool IsSuccess { get; set; }
    public AnalysisResult? Analysis { get; set; }
    public ImplementationResult? Implementation { get; set; }
    public PullRequestInfo? PullRequest { get; set; }
    public List<string> Errors { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// Status do workflow
/// </summary>
public class WorkflowStatus
{
    public string TicketId { get; set; } = string.Empty;
    public WorkflowPhase CurrentPhase { get; set; }
    public WorkflowState State { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Resumo do workflow para histórico
/// </summary>
public class WorkflowSummary
{
    public string TicketId { get; set; } = string.Empty;
    public string TicketTitle { get; set; } = string.Empty;
    public WorkflowPhase FinalPhase { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? PullRequestUrl { get; set; }
}

/// <summary>
/// Fase do workflow
/// </summary>
public enum WorkflowPhase
{
    NotStarted,
    FetchingTicket,
    AnalyzingCode,
    WaitingApproval,
    Implementing,
    Building,
    Testing,
    CreatingBranch,
    Committing,
    Pushing,
    CreatingPullRequest,
    UpdatingJira,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Estado do workflow
/// </summary>
public enum WorkflowState
{
    Running,
    Paused,
    WaitingInput,
    Completed,
    Failed,
    Cancelled
}

#region Event Args

public class AnalysisCompletedEventArgs : EventArgs
{
    public string TicketId { get; set; } = string.Empty;
    public AnalysisResult Analysis { get; set; } = new();
}

public class ImplementationCompletedEventArgs : EventArgs
{
    public string TicketId { get; set; } = string.Empty;
    public ImplementationResult Implementation { get; set; } = new();
}

public class WorkflowErrorEventArgs : EventArgs
{
    public string TicketId { get; set; } = string.Empty;
    public WorkflowPhase Phase { get; set; }
    public Exception Exception { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
}

public class WorkflowProgressEventArgs : EventArgs
{
    public string TicketId { get; set; } = string.Empty;
    public WorkflowPhase Phase { get; set; }
    public int ProgressPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

#endregion
