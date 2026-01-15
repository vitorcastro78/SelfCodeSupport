namespace SelfCodeSupport.Core.Models;

/// <summary>
/// Resultado da implementação de um ticket
/// </summary>
public class ImplementationResult
{
    /// <summary>
    /// ID do ticket implementado
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// Nome da branch criada
    /// </summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// Status da implementação
    /// </summary>
    public ImplementationStatus Status { get; set; }

    /// <summary>
    /// Arquivos criados
    /// </summary>
    public List<FileChange> CreatedFiles { get; set; } = [];

    /// <summary>
    /// Arquivos modificados
    /// </summary>
    public List<FileChange> ModifiedFiles { get; set; } = [];

    /// <summary>
    /// Arquivos deletados
    /// </summary>
    public List<string> DeletedFiles { get; set; } = [];

    /// <summary>
    /// Commits realizados
    /// </summary>
    public List<CommitInfo> Commits { get; set; } = [];

    /// <summary>
    /// Resultado do build
    /// </summary>
    public BuildResult? BuildResult { get; set; }

    /// <summary>
    /// Resultado dos testes
    /// </summary>
    public TestResult? TestResult { get; set; }

    /// <summary>
    /// URL do Pull Request criado
    /// </summary>
    public string? PullRequestUrl { get; set; }

    /// <summary>
    /// Número do Pull Request
    /// </summary>
    public int? PullRequestNumber { get; set; }

    /// <summary>
    /// Erros encontrados durante a implementação
    /// </summary>
    public List<ImplementationError> Errors { get; set; } = [];

    /// <summary>
    /// Avisos gerados durante a implementação
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Data/hora de início da implementação
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Data/hora de conclusão da implementação
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duração total da implementação
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>
    /// Indica se a implementação foi bem-sucedida
    /// </summary>
    public bool IsSuccess => Status == ImplementationStatus.Completed && 
                             Errors.Count == 0 && 
                             (BuildResult?.IsSuccess ?? true) && 
                             (TestResult?.AllPassed ?? true);

    /// <summary>
    /// Resumo da implementação para o JIRA
    /// </summary>
    public string GetJiraSummary()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## 🚀 IMPLEMENTATION COMPLETED");
        sb.AppendLine();
        sb.AppendLine($"**Branch:** `{BranchName}`");
        sb.AppendLine($"**Status:** {Status}");
        sb.AppendLine($"**Duration:** {Duration?.TotalMinutes:F0} minutes");
        sb.AppendLine();

        sb.AppendLine("### 📝 Changes");
        sb.AppendLine($"- Files created: {CreatedFiles.Count}");
        sb.AppendLine($"- Files modified: {ModifiedFiles.Count}");
        sb.AppendLine($"- Files deleted: {DeletedFiles.Count}");
        sb.AppendLine();

        if (BuildResult != null)
        {
            sb.AppendLine("### 🔨 Build");
            sb.AppendLine($"- Status: {(BuildResult.IsSuccess ? "✅ Success" : "❌ Failed")}");
            if (!BuildResult.IsSuccess)
            {
                foreach (var error in BuildResult.Errors)
                {
                    sb.AppendLine($"  - {error}");
                }
            }
            sb.AppendLine();
        }

        if (TestResult != null)
        {
            sb.AppendLine("### 🧪 Tests");
            sb.AppendLine($"- Total: {TestResult.TotalTests}");
            sb.AppendLine($"- Passed: {TestResult.PassedTests}");
            sb.AppendLine($"- Failed: {TestResult.FailedTests}");
            sb.AppendLine($"- Skipped: {TestResult.SkippedTests}");
            if (TestResult.CodeCoverage.HasValue)
            {
                sb.AppendLine($"- Coverage: {TestResult.CodeCoverage:P1}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(PullRequestUrl))
        {
            sb.AppendLine("### 🔗 Pull Request");
            sb.AppendLine($"[PR #{PullRequestNumber}]({PullRequestUrl})");
            sb.AppendLine();
        }

        if (Errors.Count > 0)
        {
            sb.AppendLine("### ❌ Errors");
            foreach (var error in Errors)
            {
                sb.AppendLine($"- {error.Message}");
            }
        }

        return sb.ToString();
    }
}

public enum ImplementationStatus
{
    NotStarted,
    InProgress,
    BuildFailed,
    TestsFailed,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Informação sobre mudança em arquivo
/// </summary>
public class FileChange
{
    public string Path { get; set; } = string.Empty;
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Informação sobre commit
/// </summary>
public class CommitInfo
{
    public string Hash { get; set; } = string.Empty;
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Author { get; set; } = string.Empty;
}

/// <summary>
/// Resultado do build
/// </summary>
public class BuildResult
{
    public bool IsSuccess { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public TimeSpan Duration { get; set; }
    public string Output { get; set; } = string.Empty;
}

/// <summary>
/// Resultado dos testes
/// </summary>
public class TestResult
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    public bool AllPassed => FailedTests == 0;
    public double? CodeCoverage { get; set; }
    public TimeSpan Duration { get; set; }
    public List<FailedTest> FailedTestDetails { get; set; } = [];
}

/// <summary>
/// Detalhes de teste que falhou
/// </summary>
public class FailedTest
{
    public string TestName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}

/// <summary>
/// Erro durante implementação
/// </summary>
public class ImplementationError
{
    public string Phase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
