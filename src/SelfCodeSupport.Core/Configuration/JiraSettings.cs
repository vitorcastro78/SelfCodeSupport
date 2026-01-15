namespace SelfCodeSupport.Core.Configuration;

/// <summary>
/// Configurações de conexão com o JIRA
/// </summary>
public class JiraSettings
{
    public const string SectionName = "Jira";

    /// <summary>
    /// URL base do JIRA (ex: https://your-domain.atlassian.net)
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Email do usuário para autenticação
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// API Token gerado no JIRA
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Chave do projeto padrão
    /// </summary>
    public string DefaultProjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Mapeamento de status do JIRA
    /// </summary>
    public JiraStatusMapping StatusMapping { get; set; } = new();

    /// <summary>
    /// Campos customizados do JIRA
    /// </summary>
    public JiraCustomFields CustomFields { get; set; } = new();

    /// <summary>
    /// Versão da API do JIRA a ser usada (2, 3, ou "latest")
    /// </summary>
    public string ApiVersion { get; set; } = "3";
}

/// <summary>
/// Mapeamento de status do JIRA para o fluxo de trabalho
/// </summary>
public class JiraStatusMapping
{
    public string Todo { get; set; } = "To Do";
    public string InProgress { get; set; } = "In Progress";
    public string InReview { get; set; } = "In Review";
    public string Done { get; set; } = "Done";
    public string Blocked { get; set; } = "Blocked";
}

/// <summary>
/// IDs de campos customizados do JIRA
/// </summary>
public class JiraCustomFields
{
    /// <summary>
    /// Campo de Story Points (ex: customfield_10016)
    /// </summary>
    public string StoryPoints { get; set; } = "customfield_10016";

    /// <summary>
    /// Campo de Sprint (ex: customfield_10020)
    /// </summary>
    public string Sprint { get; set; } = "customfield_10020";

    /// <summary>
    /// Campo de Acceptance Criteria
    /// </summary>
    public string AcceptanceCriteria { get; set; } = "customfield_10021";
}
