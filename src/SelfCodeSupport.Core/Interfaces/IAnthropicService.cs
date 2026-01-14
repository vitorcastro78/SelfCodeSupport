using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Core.Interfaces;

/// <summary>
/// Interface para serviço de integração com Anthropic Claude API
/// </summary>
public interface IAnthropicService
{
    /// <summary>
    /// Analisa um ticket e o código relacionado
    /// </summary>
    /// <param name="ticket">Ticket JIRA</param>
    /// <param name="codeContext">Contexto do código (arquivos relevantes)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da análise</returns>
    Task<AnalysisResult> AnalyzeTicketAsync(JiraTicket ticket, string codeContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera código para implementar uma solução
    /// </summary>
    /// <param name="context">Contexto da implementação</param>
    /// <param name="requirements">Requisitos da implementação</param>
    /// <param name="existingCode">Código existente para referência</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Código gerado</returns>
    Task<GeneratedCode> GenerateCodeAsync(string context, string requirements, string existingCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera testes unitários para um código
    /// </summary>
    /// <param name="code">Código a ser testado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Código dos testes</returns>
    Task<string> GenerateTestsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revisa código e fornece feedback
    /// </summary>
    /// <param name="code">Código a ser revisado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da revisão</returns>
    Task<CodeReviewResult> ReviewCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia uma mensagem genérica para o Claude
    /// </summary>
    /// <param name="message">Mensagem</param>
    /// <param name="systemPrompt">System prompt opcional</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resposta do Claude</returns>
    Task<string> SendMessageAsync(string message, string? systemPrompt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia uma conversa com histórico
    /// </summary>
    /// <param name="messages">Lista de mensagens</param>
    /// <param name="systemPrompt">System prompt opcional</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resposta do Claude</returns>
    Task<string> SendConversationAsync(IEnumerable<ConversationMessage> messages, string? systemPrompt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se a conexão com a API está funcionando
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se conectado</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Código gerado pela IA
/// </summary>
public class GeneratedCode
{
    /// <summary>
    /// Arquivos gerados
    /// </summary>
    public List<GeneratedFile> Files { get; set; } = [];

    /// <summary>
    /// Explicação das mudanças
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Dependências necessárias
    /// </summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>
    /// Instruções adicionais
    /// </summary>
    public string? AdditionalInstructions { get; set; }
}

/// <summary>
/// Arquivo gerado
/// </summary>
public class GeneratedFile
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public FileOperation Operation { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum FileOperation
{
    Create,
    Update,
    Delete
}

/// <summary>
/// Resultado da revisão de código
/// </summary>
public class CodeReviewResult
{
    /// <summary>
    /// Score geral (0-100)
    /// </summary>
    public int OverallScore { get; set; }

    /// <summary>
    /// Issues encontradas
    /// </summary>
    public List<CodeIssue> Issues { get; set; } = [];

    /// <summary>
    /// Sugestões de melhoria
    /// </summary>
    public List<string> Suggestions { get; set; } = [];

    /// <summary>
    /// Resumo da revisão
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Aprovado para merge?
    /// </summary>
    public bool Approved => OverallScore >= 70 && !Issues.Any(i => i.Severity == IssueSeverity.Critical);
}

/// <summary>
/// Issue encontrada no código
/// </summary>
public class CodeIssue
{
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public IssueCategory Category { get; set; }
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string? SuggestedFix { get; set; }
}

public enum IssueSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public enum IssueCategory
{
    Bug,
    Security,
    Performance,
    CodeStyle,
    BestPractice,
    Documentation,
    Maintainability
}

/// <summary>
/// Mensagem de conversa
/// </summary>
public class ConversationMessage
{
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
}

public enum MessageRole
{
    User,
    Assistant
}
