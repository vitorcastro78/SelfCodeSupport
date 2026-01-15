namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Resultado da análise técnica de um ticket
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// ID do ticket analisado
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// Data/hora da análise
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Arquivos identificados que precisam ser modificados
    /// </summary>
    public List<AffectedFile> AffectedFiles { get; set; } = [];

    /// <summary>
    /// Mudanças necessárias identificadas
    /// </summary>
    public List<RequiredChange> RequiredChanges { get; set; } = [];

    /// <summary>
    /// Impactos técnicos identificados
    /// </summary>
    public TechnicalImpact TechnicalImpact { get; set; } = new();

    /// <summary>
    /// Riscos identificados
    /// </summary>
    public List<Risk> Risks { get; set; } = [];

    /// <summary>
    /// Oportunidades de melhoria identificadas
    /// </summary>
    public List<Opportunity> Opportunities { get; set; } = [];

    /// <summary>
    /// Plano de implementação sugerido
    /// </summary>
    public List<ImplementationStep> ImplementationPlan { get; set; } = [];

    /// <summary>
    /// Critérios de validação
    /// </summary>
    public List<ValidationCriteria> ValidationCriteria { get; set; } = [];

    /// <summary>
    /// Estimativa de esforço (em horas)
    /// </summary>
    public int EstimatedEffortHours { get; set; }

    /// <summary>
    /// Complexidade da implementação
    /// </summary>
    public Complexity Complexity { get; set; }

    /// <summary>
    /// Status da análise
    /// </summary>
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;

    /// <summary>
    /// Comentário formatado para o JIRA
    /// </summary>
    public string FormattedJiraComment => GenerateJiraComment();

    private string GenerateJiraComment()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## 🔍 TECHNICAL ANALYSIS");
        sb.AppendLine();
        sb.AppendLine($"**Analysis Date:** {AnalyzedAt:MM/dd/yyyy HH:mm} UTC");
        sb.AppendLine($"**Complexity:** {Complexity}");
        sb.AppendLine($"**Estimate:** ~{EstimatedEffortHours}h");
        sb.AppendLine();

        sb.AppendLine("### 📁 Identified Files");
        foreach (var file in AffectedFiles)
        {
            sb.AppendLine($"- `{file.Path}` - {file.ChangeType}");
        }
        sb.AppendLine();

        sb.AppendLine("### 🔧 Required Changes");
        foreach (var change in RequiredChanges)
        {
            sb.AppendLine($"- **{change.Component}**: {change.Description}");
        }
        sb.AppendLine();

        sb.AppendLine("### ⚠️ Impacts and Risks");
        if (TechnicalImpact.HasBreakingChanges)
            sb.AppendLine("- ⚠️ **BREAKING CHANGE** detected");
        if (TechnicalImpact.RequiresMigration)
            sb.AppendLine("- 🗄️ Database migration required");
        if (TechnicalImpact.NewDependencies.Count > 0)
            sb.AppendLine($"- 📦 New dependencies: {string.Join(", ", TechnicalImpact.NewDependencies)}");

        foreach (var risk in Risks)
        {
            sb.AppendLine($"- [{risk.Severity}] {risk.Description}");
        }
        sb.AppendLine();

        if (Opportunities.Count > 0)
        {
            sb.AppendLine("### ✨ Improvement Opportunities");
            foreach (var opp in Opportunities)
            {
                sb.AppendLine($"- {opp.Description}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### 📋 Implementation Plan");
        for (int i = 0; i < ImplementationPlan.Count; i++)
        {
            var step = ImplementationPlan[i];
            sb.AppendLine($"{i + 1}. {step.Description}");
        }
        sb.AppendLine();

        sb.AppendLine("### ✅ Validation Criteria");
        foreach (var criteria in ValidationCriteria)
        {
            sb.AppendLine($"- [ ] {criteria.Description}");
        }
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine("⏸️ **Awaiting approval to proceed with implementation.**");
        sb.AppendLine("Type **\"APPROVED\"** to continue or **\"REVIEW\"** for adjustments.");

        return sb.ToString();
    }
}

/// <summary>
/// Arquivo afetado pela mudança
/// </summary>
public class AffectedFile
{
    public string Path { get; set; } = string.Empty;
    public FileChangeType ChangeType { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> MethodsAffected { get; set; } = [];
}

public enum FileChangeType
{
    Create,
    Modify,
    Delete,
    Rename
}

/// <summary>
/// Mudança necessária identificada
/// </summary>
public class RequiredChange
{
    public string Component { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChangeCategory Category { get; set; }
}

public enum ChangeCategory
{
    Controller,
    Service,
    Repository,
    Model,
    DTO,
    Validator,
    Migration,
    Configuration,
    Test,
    Documentation
}

/// <summary>
/// Impacto técnico da mudança
/// </summary>
public class TechnicalImpact
{
    public bool HasBreakingChanges { get; set; }
    public bool RequiresMigration { get; set; }
    public bool AffectsPerformance { get; set; }
    public bool HasSecurityImplications { get; set; }
    public List<string> NewDependencies { get; set; } = [];
    public List<string> AffectedEndpoints { get; set; } = [];
    public List<string> AffectedServices { get; set; } = [];
}

/// <summary>
/// Risco identificado
/// </summary>
public class Risk
{
    public string Description { get; set; } = string.Empty;
    public RiskSeverity Severity { get; set; }
    public string Mitigation { get; set; } = string.Empty;
}

public enum RiskSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Oportunidade de melhoria
/// </summary>
public class Opportunity
{
    public string Description { get; set; } = string.Empty;
    public OpportunityType Type { get; set; }
    public int EstimatedEffortHours { get; set; }
}

public enum OpportunityType
{
    Refactoring,
    Performance,
    Security,
    CodeQuality,
    Pattern,
    Documentation
}

/// <summary>
/// Passo do plano de implementação
/// </summary>
public class ImplementationStep
{
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> Files { get; set; } = [];
    public int EstimatedMinutes { get; set; }
}

/// <summary>
/// Critério de validação
/// </summary>
public class ValidationCriteria
{
    public string Description { get; set; } = string.Empty;
    public ValidationType Type { get; set; }
    public bool IsAutomatable { get; set; }
}

public enum ValidationType
{
    UnitTest,
    IntegrationTest,
    ManualTest,
    CodeReview,
    PerformanceTest,
    SecurityScan
}

/// <summary>
/// Complexidade da implementação
/// </summary>
public enum Complexity
{
    Trivial,
    Simple,
    Medium,
    Complex,
    VeryComplex
}

/// <summary>
/// Status da análise
/// </summary>
public enum AnalysisStatus
{
    Pending,
    InProgress,
    Completed,
    Approved,
    Rejected,
    NeedsRevision
}
