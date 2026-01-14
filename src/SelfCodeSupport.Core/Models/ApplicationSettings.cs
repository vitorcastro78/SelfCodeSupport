using System.ComponentModel.DataAnnotations;

namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Configurações globais da aplicação
/// </summary>
public class ApplicationSettings
{
    [Key]
    public int Id { get; set; } = 1; // Sempre ID 1 para configurações globais

    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public string ApplicationName { get; set; } = "SelfCodeSupport";

    /// <summary>
    /// Versão da aplicação
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Configurações JIRA globais (JSON serializado)
    /// </summary>
    public string JiraSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Configurações Git globais (JSON serializado)
    /// </summary>
    public string GitSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Configurações Anthropic globais (JSON serializado)
    /// </summary>
    public string AnthropicSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Configurações de Workflow globais (JSON serializado)
    /// </summary>
    public string WorkflowSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usuário que fez a última atualização
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}
