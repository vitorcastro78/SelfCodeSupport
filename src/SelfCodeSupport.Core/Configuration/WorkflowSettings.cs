namespace SelfCodeSupport.Core.Configuration;

/// <summary>
/// Configurações do fluxo de trabalho de desenvolvimento
/// </summary>
public class WorkflowSettings
{
    public const string SectionName = "Workflow";

    /// <summary>
    /// Habilitar aprovação manual antes da implementação
    /// </summary>
    public bool RequireApprovalBeforeImplementation { get; set; } = true;

    /// <summary>
    /// Timeout em minutos para aguardar aprovação
    /// </summary>
    public int ApprovalTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Executar build automaticamente
    /// </summary>
    public bool AutoBuild { get; set; } = true;

    /// <summary>
    /// Executar testes automaticamente
    /// </summary>
    public bool AutoRunTests { get; set; } = true;

    /// <summary>
    /// Cobertura mínima de código exigida (0-100)
    /// </summary>
    public int MinimumCodeCoverage { get; set; } = 80;

    /// <summary>
    /// Criar PR automaticamente após implementação
    /// </summary>
    public bool AutoCreatePullRequest { get; set; } = true;

    /// <summary>
    /// Atualizar JIRA automaticamente
    /// </summary>
    public bool AutoUpdateJira { get; set; } = true;

    /// <summary>
    /// Configurações de validação de qualidade
    /// </summary>
    public QualitySettings Quality { get; set; } = new();

    /// <summary>
    /// Configurações de notificação
    /// </summary>
    public NotificationSettings Notifications { get; set; } = new();

    /// <summary>
    /// Padrões de arquivos a ignorar na análise
    /// </summary>
    public List<string> IgnorePatterns { get; set; } =
    [
        "bin/",
        "obj/",
        "node_modules/",
        ".git/",
        "*.Designer.cs",
        "*.g.cs",
        "Migrations/"
    ];

    /// <summary>
    /// Extensões de arquivo a analisar
    /// </summary>
    public List<string> AnalyzableExtensions { get; set; } =
    [
        ".cs",
        ".json",
        ".xml",
        ".csproj",
        ".sln"
    ];
}

/// <summary>
/// Configurações de qualidade de código
/// </summary>
public class QualitySettings
{
    /// <summary>
    /// Executar análise estática (SonarQube/SonarLint)
    /// </summary>
    public bool EnableStaticAnalysis { get; set; } = false;

    /// <summary>
    /// URL do servidor SonarQube
    /// </summary>
    public string SonarQubeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Token do SonarQube
    /// </summary>
    public string SonarQubeToken { get; set; } = string.Empty;

    /// <summary>
    /// Chave do projeto no SonarQube
    /// </summary>
    public string SonarQubeProjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Executar scan de segurança
    /// </summary>
    public bool EnableSecurityScan { get; set; } = false;

    /// <summary>
    /// Falhar se houver vulnerabilidades críticas
    /// </summary>
    public bool FailOnCriticalVulnerabilities { get; set; } = true;

    /// <summary>
    /// Verificar formatação de código
    /// </summary>
    public bool EnforceCodeFormatting { get; set; } = true;

    /// <summary>
    /// Máximo de warnings permitidos
    /// </summary>
    public int MaxWarningsAllowed { get; set; } = 10;
}

/// <summary>
/// Configurações de notificação
/// </summary>
public class NotificationSettings
{
    /// <summary>
    /// Habilitar notificações
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Webhook do Slack
    /// </summary>
    public string SlackWebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Webhook do Microsoft Teams
    /// </summary>
    public string TeamsWebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Configurações de email
    /// </summary>
    public EmailSettings Email { get; set; } = new();

    /// <summary>
    /// Notificar quando análise for concluída
    /// </summary>
    public bool NotifyOnAnalysisComplete { get; set; } = true;

    /// <summary>
    /// Notificar quando PR for criado
    /// </summary>
    public bool NotifyOnPullRequestCreated { get; set; } = true;

    /// <summary>
    /// Notificar em caso de falha
    /// </summary>
    public bool NotifyOnFailure { get; set; } = true;
}

/// <summary>
/// Configurações de email
/// </summary>
public class EmailSettings
{
    public bool Enabled { get; set; } = false;
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public List<string> ToAddresses { get; set; } = [];
    public bool UseSsl { get; set; } = true;
}
