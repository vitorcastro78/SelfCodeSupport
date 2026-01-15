using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Interface para o orquestrador do fluxo de desenvolvimento
/// </summary>
public interface IWorkflowOrchestrator
{
    /// <summary>
    /// Cria um registro de workflow para um ticket sem iniciar a análise
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Workflow criado com status pending</returns>
    Task<WorkflowResult> CreateWorkflowAsync(string ticketId, CancellationToken cancellationToken = default);

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
    /// Envia a análise para o JIRA (após revisão/modificação no frontend)
    /// </summary>
    /// <param name="ticketId">ID do ticket JIRA</param>
    /// <param name="analysisComment">Comentário formatado da análise (pode ser modificado pelo frontend)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task SendAnalysisToJiraAsync(string ticketId, string analysisComment, CancellationToken cancellationToken = default);

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
/// DTO para análise técnica no formato esperado pelo frontend
/// </summary>
public class TechnicalAnalysisDto
{
    /// <summary>
    /// Data/hora em que a análise foi realizada
    /// </summary>
    public DateTime AnalyzedAt { get; set; }
    /// <summary>
    /// Status atual da análise
    /// </summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>
    /// Arquivos identificados que precisam ser modificados
    /// </summary>
    public List<AffectedFileDto> AffectedFiles { get; set; } = [];
    /// <summary>
    /// Mudanças necessárias identificadas por componente
    /// </summary>
    public List<RequiredChangeDto> RequiredChanges { get; set; } = [];
    /// <summary>
    /// Impactos técnicos da mudança
    /// </summary>
    public TechnicalImpactDto TechnicalImpact { get; set; } = new();
    /// <summary>
    /// Riscos identificados
    /// </summary>
    public List<RiskDto> Risks { get; set; } = [];
    /// <summary>
    /// Oportunidades de melhoria identificadas
    /// </summary>
    public List<ImprovementDto> Improvements { get; set; } = [];
    /// <summary>
    /// Plano de implementação passo a passo
    /// </summary>
    public List<ImplementationPlanItemDto> ImplementationPlan { get; set; } = [];
    /// <summary>
    /// Critérios de validação para garantir qualidade
    /// </summary>
    public List<ValidationCriteriaDto> ValidationCriteria { get; set; } = [];
    /// <summary>
    /// Complexidade da implementação
    /// </summary>
    public string Complexity { get; set; } = string.Empty;
    /// <summary>
    /// Estimativa de esforço total formatada
    /// </summary>
    public string EstimatedEffort { get; set; } = string.Empty;
    /// <summary>
    /// Estimativa de esforço total em horas
    /// </summary>
    public int EstimatedEffortHours { get; set; }
}

public class AffectedFileDto
{
    public string Path { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    /// <summary>
    /// Descrição detalhada das mudanças necessárias neste arquivo
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Lista de métodos/funções que serão afetados pela mudança
    /// </summary>
    public List<string> MethodsAffected { get; set; } = [];
}

public class RiskDto
{
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Estratégia de mitigação do risco
    /// </summary>
    public string Mitigation { get; set; } = string.Empty;
}

public class ImprovementDto
{
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Tipo de melhoria (Refactoring, Performance, Security, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>
    /// Esforço estimado em horas para implementar a melhoria
    /// </summary>
    public int EstimatedEffortHours { get; set; }
}

public class ImplementationPlanItemDto
{
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Completed { get; set; }
    /// <summary>
    /// Lista de arquivos que serão modificados neste passo
    /// </summary>
    public List<string> Files { get; set; } = [];
    /// <summary>
    /// Tempo estimado em minutos para completar este passo
    /// </summary>
    public int EstimatedMinutes { get; set; }
    /// <summary>
    /// Tempo estimado formatado (ex: "30 minutos", "1 hora")
    /// </summary>
    public string EstimatedTime { get; set; } = string.Empty;
}

/// <summary>
/// Impacto técnico da mudança - informações críticas para guiar o programador
/// </summary>
public class TechnicalImpactDto
{
    /// <summary>
    /// Indica se a mudança quebra compatibilidade com código existente
    /// </summary>
    public bool HasBreakingChanges { get; set; }
    /// <summary>
    /// Indica se é necessária migração de banco de dados
    /// </summary>
    public bool RequiresMigration { get; set; }
    /// <summary>
    /// Indica se a mudança afeta performance do sistema
    /// </summary>
    public bool AffectsPerformance { get; set; }
    /// <summary>
    /// Indica se há implicações de segurança
    /// </summary>
    public bool HasSecurityImplications { get; set; }
    /// <summary>
    /// Lista de novas dependências (pacotes NuGet, bibliotecas) necessárias
    /// </summary>
    public List<string> NewDependencies { get; set; } = [];
    /// <summary>
    /// Lista de endpoints da API que serão afetados
    /// </summary>
    public List<string> AffectedEndpoints { get; set; } = [];
    /// <summary>
    /// Lista de serviços que serão afetados
    /// </summary>
    public List<string> AffectedServices { get; set; } = [];
}

/// <summary>
/// Mudança necessária identificada por componente
/// </summary>
public class RequiredChangeDto
{
    /// <summary>
    /// Nome do componente que precisa ser modificado
    /// </summary>
    public string Component { get; set; } = string.Empty;
    /// <summary>
    /// Descrição detalhada da mudança necessária
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Categoria do componente (Controller, Service, Repository, etc.)
    /// </summary>
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Critério de validação para garantir qualidade da implementação
/// </summary>
public class ValidationCriteriaDto
{
    /// <summary>
    /// Descrição do critério de validação
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Tipo de validação (UnitTest, IntegrationTest, ManualTest, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>
    /// Indica se o critério pode ser automatizado
    /// </summary>
    public bool IsAutomatable { get; set; }
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
    /// <summary>
    /// Análise técnica quando disponível (após análise ser concluída)
    /// </summary>
    public TechnicalAnalysisDto? Analysis { get; set; }
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
