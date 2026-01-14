using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Interface para serviço de criação de Pull Requests
/// </summary>
public interface IPullRequestService
{
    /// <summary>
    /// Cria um Pull Request
    /// </summary>
    /// <param name="request">Dados do PR</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações do PR criado</returns>
    Task<PullRequestInfo> CreatePullRequestAsync(CreatePullRequestRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém informações de um PR existente
    /// </summary>
    /// <param name="prNumber">Número do PR</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações do PR</returns>
    Task<PullRequestInfo> GetPullRequestAsync(int prNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza um PR existente
    /// </summary>
    /// <param name="prNumber">Número do PR</param>
    /// <param name="update">Dados para atualização</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task UpdatePullRequestAsync(int prNumber, UpdatePullRequestRequest update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona reviewers ao PR
    /// </summary>
    /// <param name="prNumber">Número do PR</param>
    /// <param name="reviewers">Lista de reviewers</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task AddReviewersAsync(int prNumber, IEnumerable<string> reviewers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona labels ao PR
    /// </summary>
    /// <param name="prNumber">Número do PR</param>
    /// <param name="labels">Lista de labels</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task AddLabelsAsync(int prNumber, IEnumerable<string> labels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona um comentário ao PR
    /// </summary>
    /// <param name="prNumber">Número do PR</param>
    /// <param name="comment">Comentário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task AddCommentAsync(int prNumber, string comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fecha um PR
    /// </summary>
    /// <param name="prNumber">Número do PR</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task ClosePullRequestAsync(int prNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz merge de um PR
    /// </summary>
    /// <param name="prNumber">Número do PR</param>
    /// <param name="mergeMethod">Método de merge</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task MergePullRequestAsync(int prNumber, MergeMethod mergeMethod = MergeMethod.Squash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se a conexão com a plataforma está funcionando
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se conectado</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request para criar um PR
/// </summary>
public class CreatePullRequestRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string SourceBranch { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = string.Empty;
    public bool IsDraft { get; set; } = false;
    public List<string> Reviewers { get; set; } = [];
    public List<string> Labels { get; set; } = [];
}

/// <summary>
/// Request para atualizar um PR
/// </summary>
public class UpdatePullRequestRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public PullRequestStatus? Status { get; set; }
}

/// <summary>
/// Método de merge
/// </summary>
public enum MergeMethod
{
    Merge,
    Squash,
    Rebase
}
