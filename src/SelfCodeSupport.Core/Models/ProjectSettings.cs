using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Configurações de um projeto específico
/// </summary>
public class ProjectSettings
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Nome do projeto
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrição do projeto
    /// </summary>
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Chave do projeto no JIRA
    /// </summary>
    [MaxLength(50)]
    public string JiraProjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Caminho do repositório Git local
    /// </summary>
    [MaxLength(500)]
    public string GitRepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// URL remota do repositório Git
    /// </summary>
    [MaxLength(500)]
    public string GitRemoteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Branch padrão do projeto
    /// </summary>
    [MaxLength(100)]
    public string GitDefaultBranch { get; set; } = "main";

    /// <summary>
    /// Configurações JIRA do projeto (JSON serializado)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string JiraSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Configurações Git do projeto (JSON serializado)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string GitSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Configurações de Workflow do projeto (JSON serializado)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string WorkflowSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Configurações específicas do projeto (JSON serializado)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string ProjectSpecificSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Indica se este é o projeto padrão
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Indica se o projeto está ativo
    /// </summary>
    public bool IsActive { get; set; } = true;

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
    [MaxLength(200)]
    public string UpdatedBy { get; set; } = string.Empty;
}
