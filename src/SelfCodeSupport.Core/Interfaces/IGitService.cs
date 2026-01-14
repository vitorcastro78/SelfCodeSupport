using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Interface para serviço de integração com Git
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Clona um repositório
    /// </summary>
    /// <param name="url">URL do repositório</param>
    /// <param name="localPath">Caminho local</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task CloneRepositoryAsync(string url, string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o repositório local (pull)
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task PullAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria uma nova branch
    /// </summary>
    /// <param name="branchName">Nome da branch</param>
    /// <param name="baseBranch">Branch base (opcional, usa default se não informado)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task CreateBranchAsync(string branchName, string? baseBranch = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Muda para uma branch existente
    /// </summary>
    /// <param name="branchName">Nome da branch</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task CheckoutAsync(string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona arquivos ao staging
    /// </summary>
    /// <param name="paths">Caminhos dos arquivos (null para todos)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task StageFilesAsync(IEnumerable<string>? paths = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Realiza um commit
    /// </summary>
    /// <param name="message">Mensagem do commit</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações do commit</returns>
    Task<CommitInfo> CommitAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia alterações para o remote
    /// </summary>
    /// <param name="branchName">Nome da branch</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task PushAsync(string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o status do repositório
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Status do repositório</returns>
    Task<GitStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a branch atual
    /// </summary>
    /// <returns>Nome da branch atual</returns>
    string GetCurrentBranch();

    /// <summary>
    /// Lista todas as branches
    /// </summary>
    /// <param name="includeRemote">Incluir branches remotas</param>
    /// <returns>Lista de branches</returns>
    IEnumerable<string> ListBranches(bool includeRemote = false);

    /// <summary>
    /// Obtém o conteúdo de um arquivo
    /// </summary>
    /// <param name="filePath">Caminho do arquivo</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Conteúdo do arquivo</returns>
    Task<string> GetFileContentAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista arquivos no repositório
    /// </summary>
    /// <param name="path">Caminho base (opcional)</param>
    /// <param name="pattern">Padrão de busca (ex: *.cs)</param>
    /// <returns>Lista de caminhos de arquivos</returns>
    IEnumerable<string> ListFiles(string? path = null, string? pattern = null);

    /// <summary>
    /// Busca arquivos por conteúdo
    /// </summary>
    /// <param name="searchTerm">Termo de busca</param>
    /// <param name="filePattern">Padrão de arquivo (ex: *.cs)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de arquivos que contêm o termo</returns>
    Task<IEnumerable<FileSearchResult>> SearchInFilesAsync(string searchTerm, string? filePattern = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera o nome da branch baseado no ticket
    /// </summary>
    /// <param name="ticket">Ticket JIRA</param>
    /// <returns>Nome da branch</returns>
    string GenerateBranchName(JiraTicket ticket);

    /// <summary>
    /// Gera a mensagem de commit baseada no ticket
    /// </summary>
    /// <param name="ticket">Ticket JIRA</param>
    /// <param name="description">Descrição das mudanças</param>
    /// <returns>Mensagem de commit formatada</returns>
    string GenerateCommitMessage(JiraTicket ticket, string description);

    /// <summary>
    /// Verifica se o repositório está limpo (sem mudanças pendentes)
    /// </summary>
    /// <returns>True se limpo</returns>
    bool IsClean();

    /// <summary>
    /// Descarta todas as mudanças locais
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task DiscardChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Status do repositório Git
/// </summary>
public class GitStatus
{
    public string CurrentBranch { get; set; } = string.Empty;
    public bool IsClean { get; set; }
    public int AheadBy { get; set; }
    public int BehindBy { get; set; }
    public List<string> ModifiedFiles { get; set; } = [];
    public List<string> AddedFiles { get; set; } = [];
    public List<string> DeletedFiles { get; set; } = [];
    public List<string> UntrackedFiles { get; set; } = [];
    public List<string> StagedFiles { get; set; } = [];
}

/// <summary>
/// Resultado de busca em arquivo
/// </summary>
public class FileSearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
}
