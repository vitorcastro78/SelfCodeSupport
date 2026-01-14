namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Informações do Pull Request
/// </summary>
public class PullRequestInfo
{
    /// <summary>
    /// Número do PR
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Título do PR
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Descrição/Body do PR
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Branch de origem
    /// </summary>
    public string SourceBranch { get; set; } = string.Empty;

    /// <summary>
    /// Branch de destino
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;

    /// <summary>
    /// URL do PR
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Status do PR
    /// </summary>
    public PullRequestStatus Status { get; set; }

    /// <summary>
    /// Autor do PR
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Reviewers solicitados
    /// </summary>
    public List<string> Reviewers { get; set; } = [];

    /// <summary>
    /// Labels do PR
    /// </summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>
    /// ID do ticket JIRA associado
    /// </summary>
    public string JiraTicketId { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data de merge (se aplicável)
    /// </summary>
    public DateTime? MergedAt { get; set; }

    /// <summary>
    /// Tipo de mudança
    /// </summary>
    public PullRequestChangeType ChangeType { get; set; }

    /// <summary>
    /// Checklist de validação
    /// </summary>
    public PullRequestChecklist Checklist { get; set; } = new();

    /// <summary>
    /// Gera o body do PR no formato markdown
    /// </summary>
    public string GenerateBody(JiraTicket ticket, ImplementationResult implementation)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"## 🎫 {ticket.Id}: {ticket.Title}");
        sb.AppendLine();

        sb.AppendLine("### 📋 Descrição");
        sb.AppendLine(Description);
        sb.AppendLine();

        sb.AppendLine("### 🔗 Link do Ticket");
        sb.AppendLine($"[{ticket.Id}]({ticket.Url})");
        sb.AppendLine();

        sb.AppendLine("### 🛠️ Tipo de Mudança");
        sb.AppendLine($"- [{(ChangeType == PullRequestChangeType.BugFix ? "x" : " ")}] Bug fix");
        sb.AppendLine($"- [{(ChangeType == PullRequestChangeType.Feature ? "x" : " ")}] Nova feature");
        sb.AppendLine($"- [{(ChangeType == PullRequestChangeType.BreakingChange ? "x" : " ")}] Breaking change");
        sb.AppendLine($"- [{(ChangeType == PullRequestChangeType.Refactoring ? "x" : " ")}] Refatoração");
        sb.AppendLine($"- [{(ChangeType == PullRequestChangeType.Documentation ? "x" : " ")}] Documentação");
        sb.AppendLine();

        sb.AppendLine("### ✨ O que foi desenvolvido");
        foreach (var file in implementation.CreatedFiles)
        {
            sb.AppendLine($"- ➕ `{file.Path}` - {file.Description}");
        }
        foreach (var file in implementation.ModifiedFiles)
        {
            sb.AppendLine($"- ✏️ `{file.Path}` - {file.Description}");
        }
        foreach (var file in implementation.DeletedFiles)
        {
            sb.AppendLine($"- ❌ `{file}`");
        }
        sb.AppendLine();

        sb.AppendLine("### 🧪 Como testar");
        sb.AppendLine("1. Checkout da branch: `git checkout " + SourceBranch + "`");
        sb.AppendLine("2. Executar build: `dotnet build`");
        sb.AppendLine("3. Executar testes: `dotnet test`");
        sb.AppendLine("4. Executar aplicação: `dotnet run`");
        sb.AppendLine("5. Validar endpoints afetados");
        sb.AppendLine();

        sb.AppendLine("### ✅ Checklist");
        sb.AppendLine($"- [{(Checklist.FollowsProjectPatterns ? "x" : " ")}] Código segue os padrões do projeto");
        sb.AppendLine($"- [{(Checklist.HasUnitTests ? "x" : " ")}] Testes unitários criados/atualizados");
        sb.AppendLine($"- [{(Checklist.TestsPassing ? "x" : " ")}] Testes passando localmente");
        sb.AppendLine($"- [{(Checklist.DocumentationUpdated ? "x" : " ")}] Documentação atualizada");
        sb.AppendLine($"- [{(Checklist.NoBreakingChanges ? "x" : " ")}] Sem breaking changes ou devidamente documentados");
        sb.AppendLine($"- [{(Checklist.SelfReviewed ? "x" : " ")}] Code review próprio realizado");
        sb.AppendLine();

        if (implementation.TestResult != null)
        {
            sb.AppendLine("### 🧪 Resultado dos Testes");
            sb.AppendLine($"- Total: {implementation.TestResult.TotalTests}");
            sb.AppendLine($"- ✅ Passaram: {implementation.TestResult.PassedTests}");
            sb.AppendLine($"- ❌ Falharam: {implementation.TestResult.FailedTests}");
            if (implementation.TestResult.CodeCoverage.HasValue)
            {
                sb.AppendLine($"- 📊 Cobertura: {implementation.TestResult.CodeCoverage:P1}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### 📝 Notas Adicionais");
        sb.AppendLine("_PR gerado automaticamente pelo SelfCodeSupport._");

        return sb.ToString();
    }
}

public enum PullRequestStatus
{
    Open,
    Closed,
    Merged,
    Draft
}

public enum PullRequestChangeType
{
    BugFix,
    Feature,
    BreakingChange,
    Refactoring,
    Documentation,
    Hotfix
}

/// <summary>
/// Checklist de validação do PR
/// </summary>
public class PullRequestChecklist
{
    public bool FollowsProjectPatterns { get; set; } = true;
    public bool HasUnitTests { get; set; } = true;
    public bool TestsPassing { get; set; } = true;
    public bool DocumentationUpdated { get; set; } = true;
    public bool NoBreakingChanges { get; set; } = true;
    public bool SelfReviewed { get; set; } = true;
}
