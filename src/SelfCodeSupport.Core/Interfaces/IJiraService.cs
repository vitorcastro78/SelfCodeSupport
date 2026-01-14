using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Interface para serviço de integração com JIRA
/// </summary>
public interface IJiraService
{
    /// <summary>
    /// Obtém um ticket pelo ID
    /// </summary>
    /// <param name="ticketId">ID do ticket (ex: PROJ-1234)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Ticket do JIRA</returns>
    Task<JiraTicket> GetTicketAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca tickets por JQL
    /// </summary>
    /// <param name="jql">Query JQL</param>
    /// <param name="maxResults">Máximo de resultados</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de tickets</returns>
    Task<IEnumerable<JiraTicket>> SearchTicketsAsync(string jql, int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona um comentário ao ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="comment">Texto do comentário (suporta markdown)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task AddCommentAsync(string ticketId, string comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o status do ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="status">Novo status</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task UpdateStatusAsync(string ticketId, string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza campos do ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="fields">Dicionário de campos a atualizar</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task UpdateFieldsAsync(string ticketId, Dictionary<string, object> fields, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona um link ao ticket (ex: link do PR)
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="url">URL do link</param>
    /// <param name="title">Título do link</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task AddRemoteLinkAsync(string ticketId, string url, string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém as transições disponíveis para o ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de transições disponíveis</returns>
    Task<IEnumerable<JiraTransition>> GetAvailableTransitionsAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa uma transição no ticket
    /// </summary>
    /// <param name="ticketId">ID do ticket</param>
    /// <param name="transitionId">ID da transição</param>
    /// <param name="comment">Comentário opcional</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task TransitionTicketAsync(string ticketId, string transitionId, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se a conexão com o JIRA está funcionando
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se conectado</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Transição disponível no JIRA
/// </summary>
public class JiraTransition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
}
