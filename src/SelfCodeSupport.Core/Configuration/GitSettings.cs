namespace SelfCodeSupport.Core.Configuration;

/// <summary>
/// Configurações do Git
/// </summary>
public class GitSettings
{
    public const string SectionName = "Git";

    /// <summary>
    /// Caminho do repositório local (opcional - se vazio, clona em workspace temporário)
    /// </summary>
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Usar workspace temporário para cada análise (útil para containers/servidores)
    /// Se true, clona o repositório em um diretório temporário para cada análise
    /// </summary>
    public bool UseTemporaryWorkspace { get; set; } = false;

    /// <summary>
    /// Caminho base para workspaces temporários (padrão: temp do sistema)
    /// </summary>
    public string? TemporaryWorkspaceBasePath { get; set; }

    /// <summary>
    /// URL remota do repositório (para clone)
    /// </summary>
    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Nome do remote (padrão: origin)
    /// </summary>
    public string RemoteName { get; set; } = "origin";

    /// <summary>
    /// Branch principal (main ou master)
    /// </summary>
    public string DefaultBranch { get; set; } = "main";

    /// <summary>
    /// Configurações de branch
    /// </summary>
    public BranchSettings BranchSettings { get; set; } = new();

    /// <summary>
    /// Configurações de commit
    /// </summary>
    public CommitSettings CommitSettings { get; set; } = new();

    /// <summary>
    /// Credenciais Git
    /// </summary>
    public GitCredentials Credentials { get; set; } = new();

    /// <summary>
    /// Configurações de Pull Request (GitHub/GitLab/Azure DevOps)
    /// </summary>
    public PullRequestSettings PullRequestSettings { get; set; } = new();
}

/// <summary>
/// Configurações de nomenclatura de branches
/// </summary>
public class BranchSettings
{
    /// <summary>
    /// Prefixo para features
    /// </summary>
    public string FeaturePrefix { get; set; } = "feature/";

    /// <summary>
    /// Prefixo para bugfixes
    /// </summary>
    public string BugfixPrefix { get; set; } = "bugfix/";

    /// <summary>
    /// Prefixo para hotfixes
    /// </summary>
    public string HotfixPrefix { get; set; } = "hotfix/";

    /// <summary>
    /// Prefixo para releases
    /// </summary>
    public string ReleasePrefix { get; set; } = "release/";

    /// <summary>
    /// Template do nome da branch
    /// {prefix} - prefixo baseado no tipo
    /// {ticketId} - ID do ticket JIRA
    /// {description} - descrição curta
    /// </summary>
    public string BranchNameTemplate { get; set; } = "{prefix}{ticketId}-{description}";
}

/// <summary>
/// Configurações de commit
/// </summary>
public class CommitSettings
{
    /// <summary>
    /// Nome do autor dos commits
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Email do autor dos commits
    /// </summary>
    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>
    /// Template da mensagem de commit (Conventional Commits)
    /// {type} - tipo (feat, fix, etc)
    /// {scope} - escopo opcional
    /// {ticketId} - ID do ticket
    /// {description} - descrição
    /// {body} - corpo detalhado
    /// </summary>
    public string CommitMessageTemplate { get; set; } = "{type}({ticketId}): {description}\n\n{body}\n\nCloses {ticketId}";

    /// <summary>
    /// Habilitar assinatura GPG
    /// </summary>
    public bool SignCommits { get; set; } = false;
}

/// <summary>
/// Credenciais Git
/// </summary>
public class GitCredentials
{
    /// <summary>
    /// Username para autenticação HTTPS
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Token de acesso pessoal (PAT)
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Caminho da chave SSH privada (se usar SSH)
    /// </summary>
    public string SshKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Passphrase da chave SSH
    /// </summary>
    public string SshPassphrase { get; set; } = string.Empty;
}

/// <summary>
/// Configurações de Pull Request
/// </summary>
public class PullRequestSettings
{
    /// <summary>
    /// Tipo de plataforma (GitHub, GitLab, AzureDevOps, Bitbucket)
    /// </summary>
    public GitPlatform Platform { get; set; } = GitPlatform.GitHub;

    /// <summary>
    /// URL da API (para plataformas self-hosted)
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Token de acesso para API
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Organização/Owner do repositório
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Nome do repositório
    /// </summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// Reviewers padrão para PRs
    /// </summary>
    public List<string> DefaultReviewers { get; set; } = [];

    /// <summary>
    /// Labels padrão para PRs
    /// </summary>
    public List<string> DefaultLabels { get; set; } = [];

    /// <summary>
    /// Criar PR como draft
    /// </summary>
    public bool CreateAsDraft { get; set; } = false;

    /// <summary>
    /// Habilitar auto-merge quando aprovado
    /// </summary>
    public bool EnableAutoMerge { get; set; } = false;

    /// <summary>
    /// Deletar branch após merge
    /// </summary>
    public bool DeleteBranchOnMerge { get; set; } = true;
}

/// <summary>
/// Plataformas Git suportadas
/// </summary>
public enum GitPlatform
{
    GitHub,
    GitLab,
    AzureDevOps,
    Bitbucket
}
