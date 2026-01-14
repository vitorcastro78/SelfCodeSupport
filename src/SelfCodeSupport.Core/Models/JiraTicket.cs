namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Representa um ticket do JIRA com todas as informações necessárias para análise e desenvolvimento.
/// </summary>
public class JiraTicket
{
    /// <summary>
    /// ID único do ticket (ex: PROJ-1234)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Chave do projeto no JIRA
    /// </summary>
    public string ProjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Título do ticket
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Descrição detalhada do ticket
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do ticket (Bug, Story, Task, etc.)
    /// </summary>
    public TicketType Type { get; set; }

    /// <summary>
    /// Prioridade do ticket
    /// </summary>
    public TicketPriority Priority { get; set; }

    /// <summary>
    /// Status atual do ticket
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Critérios de aceitação
    /// </summary>
    public List<string> AcceptanceCriteria { get; set; } = [];

    /// <summary>
    /// Labels/Tags associadas ao ticket
    /// </summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>
    /// Componentes afetados
    /// </summary>
    public List<string> Components { get; set; } = [];

    /// <summary>
    /// Usuário responsável pelo ticket
    /// </summary>
    public string Assignee { get; set; } = string.Empty;

    /// <summary>
    /// Usuário que criou o ticket
    /// </summary>
    public string Reporter { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// URL do ticket no JIRA
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Sprint associada (se houver)
    /// </summary>
    public string? Sprint { get; set; }

    /// <summary>
    /// Story points estimados
    /// </summary>
    public int? StoryPoints { get; set; }

    /// <summary>
    /// Anexos do ticket
    /// </summary>
    public List<JiraAttachment> Attachments { get; set; } = [];

    /// <summary>
    /// Comentários do ticket
    /// </summary>
    public List<JiraComment> Comments { get; set; } = [];

    /// <summary>
    /// Tickets relacionados (links)
    /// </summary>
    public List<string> LinkedTickets { get; set; } = [];
}

/// <summary>
/// Tipo do ticket JIRA
/// </summary>
public enum TicketType
{
    Bug,
    Story,
    Task,
    Epic,
    SubTask,
    Improvement,
    NewFeature
}

/// <summary>
/// Prioridade do ticket
/// </summary>
public enum TicketPriority
{
    Lowest,
    Low,
    Medium,
    High,
    Highest,
    Critical
}

/// <summary>
/// Anexo do ticket JIRA
/// </summary>
public class JiraAttachment
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Comentário do ticket JIRA
/// </summary>
public class JiraComment
{
    public string Id { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
