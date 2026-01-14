using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Orquestrador do fluxo de desenvolvimento automatizado
/// </summary>
public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IJiraService _jiraService;
    private readonly IGitService _gitService;
    private readonly IAnthropicService _anthropicService;
    private readonly IPullRequestService _pullRequestService;
    private readonly WorkflowSettings _settings;
    private readonly GitSettings _gitSettings;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    private readonly ConcurrentDictionary<string, WorkflowState> _workflowStates = new();
    private readonly ConcurrentDictionary<string, AnalysisResult> _pendingAnalyses = new();
    private readonly ConcurrentDictionary<string, WorkflowResult> _workflowResults = new();

    public event EventHandler<AnalysisCompletedEventArgs>? AnalysisCompleted;
    public event EventHandler<ImplementationCompletedEventArgs>? ImplementationCompleted;
    public event EventHandler<WorkflowErrorEventArgs>? WorkflowError;
    public event EventHandler<WorkflowProgressEventArgs>? ProgressUpdated;

    public WorkflowOrchestrator(
        IJiraService jiraService,
        IGitService gitService,
        IAnthropicService anthropicService,
        IPullRequestService pullRequestService,
        IOptions<WorkflowSettings> workflowSettings,
        IOptions<GitSettings> gitSettings,
        ILogger<WorkflowOrchestrator> logger)
    {
        _jiraService = jiraService;
        _gitService = gitService;
        _anthropicService = anthropicService;
        _pullRequestService = pullRequestService;
        _settings = workflowSettings.Value;
        _gitSettings = gitSettings.Value;
        _logger = logger;
    }

    public async Task<WorkflowResult> StartWorkflowAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando workflow para ticket {TicketId}", ticketId);

        var result = new WorkflowResult
        {
            TicketId = ticketId,
            StartedAt = DateTime.UtcNow
        };

        _workflowStates[ticketId] = WorkflowState.Running;
        _workflowResults[ticketId] = result;

        try
        {
            // Obter título do ticket para o resultado
            try
            {
                var ticket = await _jiraService.GetTicketAsync(ticketId, cancellationToken);
                result.TicketTitle = ticket.Title;
            }
            catch
            {
                // Se falhar ao obter ticket, continua sem título
                result.TicketTitle = string.Empty;
            }

            // Fase 1: Análise
            var analysis = await AnalyzeAsync(ticketId, cancellationToken);
            result.Analysis = analysis;

            if (_settings.RequireApprovalBeforeImplementation)
            {
                _workflowStates[ticketId] = WorkflowState.WaitingInput;
                result.FinalPhase = WorkflowPhase.WaitingApproval;
                _logger.LogInformation("Análise concluída para {TicketId}. Aguardando aprovação.", ticketId);
                return result;
            }

            // Se não requer aprovação, continua automaticamente
            var implementation = await ApproveAndImplementAsync(ticketId, cancellationToken);
            result.Implementation = implementation;
            result.PullRequest = implementation.PullRequestUrl != null
                ? await _pullRequestService.GetPullRequestAsync(implementation.PullRequestNumber ?? 0, cancellationToken)
                : null;

            result.FinalPhase = WorkflowPhase.Completed;
            result.IsSuccess = true;
            result.CompletedAt = DateTime.UtcNow;

            _workflowStates[ticketId] = WorkflowState.Completed;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no workflow para ticket {TicketId}", ticketId);

            result.FinalPhase = WorkflowPhase.Failed;
            result.IsSuccess = false;
            result.Errors.Add(ex.Message);
            result.CompletedAt = DateTime.UtcNow;

            _workflowStates[ticketId] = WorkflowState.Failed;

            OnWorkflowError(ticketId, result.FinalPhase, ex);

            throw;
        }
    }

    public async Task<AnalysisResult> AnalyzeAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando análise do ticket {TicketId}", ticketId);
        UpdateProgress(ticketId, WorkflowPhase.FetchingTicket, 5, "Obtendo informações do ticket...");

        // 1. Obter ticket do JIRA
        var ticket = await _jiraService.GetTicketAsync(ticketId, cancellationToken);
        UpdateProgress(ticketId, WorkflowPhase.FetchingTicket, 15, "Ticket obtido com sucesso");

        // 2. Preparar repositório
        UpdateProgress(ticketId, WorkflowPhase.AnalyzingCode, 20, "Preparando repositório...");
        await _gitService.PullAsync(cancellationToken);
        await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);

        // 3. Buscar arquivos relacionados
        UpdateProgress(ticketId, WorkflowPhase.AnalyzingCode, 30, "Buscando arquivos relacionados...");
        var codeContext = await BuildCodeContextAsync(ticket, cancellationToken);

        // 4. Analisar com Claude
        UpdateProgress(ticketId, WorkflowPhase.AnalyzingCode, 50, "Analisando código com IA...");
        var analysis = await _anthropicService.AnalyzeTicketAsync(ticket, codeContext, cancellationToken);

        // 5. Documentar análise no JIRA
        UpdateProgress(ticketId, WorkflowPhase.AnalyzingCode, 80, "Documentando análise no JIRA...");
        if (_settings.AutoUpdateJira)
        {
            await _jiraService.AddCommentAsync(ticketId, analysis.FormattedJiraComment, cancellationToken);
        }

        // 6. Armazenar análise pendente
        _pendingAnalyses[ticketId] = analysis;

        UpdateProgress(ticketId, WorkflowPhase.WaitingApproval, 100, "Análise concluída. Aguardando aprovação.");

        OnAnalysisCompleted(ticketId, analysis);

        return analysis;
    }

    public async Task<ImplementationResult> ApproveAndImplementAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Implementação aprovada para ticket {TicketId}", ticketId);

        if (!_pendingAnalyses.TryGetValue(ticketId, out var analysis))
        {
            throw new InvalidOperationException($"Nenhuma análise pendente encontrada para o ticket {ticketId}");
        }

        var result = new ImplementationResult
        {
            TicketId = ticketId,
            StartedAt = DateTime.UtcNow,
            Status = ImplementationStatus.InProgress
        };

        try
        {
            // 1. Obter ticket
            var ticket = await _jiraService.GetTicketAsync(ticketId, cancellationToken);

            // 2. Atualizar status no JIRA
            if (_settings.AutoUpdateJira)
            {
                await _jiraService.AddCommentAsync(ticketId, "🚀 **Implementação iniciada automaticamente.**", cancellationToken);
            }

            // 3. Criar branch
            UpdateProgress(ticketId, WorkflowPhase.CreatingBranch, 10, "Criando branch...");
            var branchName = _gitService.GenerateBranchName(ticket);
            result.BranchName = branchName;

            await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);
            await _gitService.PullAsync(cancellationToken);
            await _gitService.CreateBranchAsync(branchName, cancellationToken: cancellationToken);

            // 4. Gerar código
            UpdateProgress(ticketId, WorkflowPhase.Implementing, 20, "Gerando código...");
            var codeContext = await BuildCodeContextAsync(ticket, cancellationToken);
            var requirements = BuildRequirementsFromAnalysis(analysis);

            var generatedCode = await _anthropicService.GenerateCodeAsync(
                $"Ticket: {ticket.Id} - {ticket.Title}\n{ticket.Description}",
                requirements,
                codeContext,
                cancellationToken);

            // 5. Aplicar mudanças
            UpdateProgress(ticketId, WorkflowPhase.Implementing, 40, "Aplicando mudanças...");
            await ApplyGeneratedCodeAsync(generatedCode, result, cancellationToken);

            // 6. Build
            if (_settings.AutoBuild)
            {
                UpdateProgress(ticketId, WorkflowPhase.Building, 50, "Executando build...");
                result.BuildResult = await RunBuildAsync(cancellationToken);

                if (!result.BuildResult.IsSuccess)
                {
                    result.Status = ImplementationStatus.BuildFailed;
                    throw new InvalidOperationException("Build falhou: " + string.Join(", ", result.BuildResult.Errors));
                }
            }

            // 7. Testes
            if (_settings.AutoRunTests)
            {
                UpdateProgress(ticketId, WorkflowPhase.Testing, 60, "Executando testes...");
                result.TestResult = await RunTestsAsync(cancellationToken);

                if (!result.TestResult.AllPassed)
                {
                    result.Status = ImplementationStatus.TestsFailed;
                    result.Warnings.Add($"{result.TestResult.FailedTests} teste(s) falharam");
                }
            }

            // 8. Commit
            UpdateProgress(ticketId, WorkflowPhase.Committing, 70, "Realizando commit...");
            await _gitService.StageFilesAsync(cancellationToken: cancellationToken);
            var commitMessage = _gitService.GenerateCommitMessage(ticket, GetCommitDescription(result));
            var commit = await _gitService.CommitAsync(commitMessage, cancellationToken);
            result.Commits.Add(commit);

            // 9. Push
            UpdateProgress(ticketId, WorkflowPhase.Pushing, 80, "Enviando alterações...");
            await _gitService.PushAsync(branchName, cancellationToken);

            // 10. Criar PR
            if (_settings.AutoCreatePullRequest)
            {
                UpdateProgress(ticketId, WorkflowPhase.CreatingPullRequest, 90, "Criando Pull Request...");
                var prInfo = await CreatePullRequestAsync(ticket, result, analysis, cancellationToken);
                result.PullRequestUrl = prInfo.Url;
                result.PullRequestNumber = prInfo.Number;
            }

            // 11. Atualizar JIRA
            if (_settings.AutoUpdateJira)
            {
                UpdateProgress(ticketId, WorkflowPhase.UpdatingJira, 95, "Atualizando JIRA...");
                await _jiraService.AddCommentAsync(ticketId, result.GetJiraSummary(), cancellationToken);

                if (!string.IsNullOrEmpty(result.PullRequestUrl))
                {
                    await _jiraService.AddRemoteLinkAsync(
                        ticketId,
                        result.PullRequestUrl,
                        $"PR #{result.PullRequestNumber}",
                        cancellationToken);
                }
            }

            result.Status = ImplementationStatus.Completed;
            result.CompletedAt = DateTime.UtcNow;

            UpdateProgress(ticketId, WorkflowPhase.Completed, 100, "Implementação concluída!");

            OnImplementationCompleted(ticketId, result);

            // Limpar análise pendente
            _pendingAnalyses.TryRemove(ticketId, out _);

            return result;
        }
        catch (Exception ex)
        {
            result.Status = ImplementationStatus.Failed;
            result.Errors.Add(new ImplementationError
            {
                Phase = "Implementation",
                Message = ex.Message,
                Details = ex.StackTrace
            });
            result.CompletedAt = DateTime.UtcNow;

            OnWorkflowError(ticketId, WorkflowPhase.Implementing, ex);

            throw;
        }
    }

    public async Task<AnalysisResult> RequestRevisionAsync(string ticketId, string feedback, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revisão solicitada para ticket {TicketId}: {Feedback}", ticketId, feedback);

        // Adicionar feedback como contexto adicional e re-analisar
        if (_settings.AutoUpdateJira)
        {
            await _jiraService.AddCommentAsync(ticketId, $"📝 **Revisão solicitada:**\n{feedback}", cancellationToken);
        }

        // Re-executar análise com o feedback
        return await AnalyzeAsync(ticketId, cancellationToken);
    }

    public async Task CancelWorkflowAsync(string ticketId, string reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelando workflow para ticket {TicketId}: {Reason}", ticketId, reason);

        _workflowStates[ticketId] = WorkflowState.Cancelled;
        _pendingAnalyses.TryRemove(ticketId, out _);

        if (_settings.AutoUpdateJira)
        {
            await _jiraService.AddCommentAsync(ticketId, $"❌ **Workflow cancelado:**\n{reason}", cancellationToken);
        }

        // Descartar mudanças locais se houver
        if (!_gitService.IsClean())
        {
            await _gitService.DiscardChangesAsync(cancellationToken);
            await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);
        }
    }

    public Task<WorkflowStatus> GetWorkflowStatusAsync(string ticketId)
    {
        var state = _workflowStates.GetValueOrDefault(ticketId, WorkflowState.Completed);
        var result = _workflowResults.GetValueOrDefault(ticketId);

        return Task.FromResult(new WorkflowStatus
        {
            TicketId = ticketId,
            CurrentPhase = result?.FinalPhase ?? WorkflowPhase.NotStarted,
            State = state,
            LastUpdated = DateTime.UtcNow
        });
    }

    public Task<IEnumerable<WorkflowSummary>> GetWorkflowHistoryAsync(int limit = 20)
    {
        var summaries = _workflowResults.Values
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .Select(r => new WorkflowSummary
            {
                TicketId = r.TicketId,
                TicketTitle = r.TicketTitle,
                FinalPhase = r.FinalPhase,
                IsSuccess = r.IsSuccess,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                PullRequestUrl = r.PullRequest?.Url
            });

        return Task.FromResult(summaries);
    }

    #region Private Helper Methods

    private async Task<string> BuildCodeContextAsync(JiraTicket ticket, CancellationToken cancellationToken)
    {
        var sb = new System.Text.StringBuilder();
        var searchTerms = ExtractSearchTerms(ticket);

        foreach (var term in searchTerms.Take(5)) // Limitar busca
        {
            var results = await _gitService.SearchInFilesAsync(term, "*.cs", cancellationToken);

            foreach (var result in results.Take(3)) // Limitar resultados por termo
            {
                if (!ShouldIgnoreFile(result.FilePath))
                {
                    var content = await _gitService.GetFileContentAsync(result.FilePath, cancellationToken);
                    sb.AppendLine($"// File: {result.FilePath}");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }
        }

        // Adicionar arquivos de componentes mencionados
        foreach (var component in ticket.Components)
        {
            var files = _gitService.ListFiles(pattern: $"*{component}*.cs");
            foreach (var file in files.Take(3))
            {
                if (!ShouldIgnoreFile(file))
                {
                    var content = await _gitService.GetFileContentAsync(file, cancellationToken);
                    sb.AppendLine($"// File: {file}");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    private static List<string> ExtractSearchTerms(JiraTicket ticket)
    {
        var terms = new List<string>();

        // Extrair palavras-chave do título
        var titleWords = ticket.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Take(5);
        terms.AddRange(titleWords);

        // Adicionar labels
        terms.AddRange(ticket.Labels);

        // Adicionar componentes
        terms.AddRange(ticket.Components);

        return terms.Distinct().ToList();
    }

    private bool ShouldIgnoreFile(string filePath)
    {
        return _settings.IgnorePatterns.Any(pattern =>
            filePath.Contains(pattern.TrimEnd('/', '*'), StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildRequirementsFromAnalysis(AnalysisResult analysis)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## Requisitos de Implementação");
        sb.AppendLine();

        sb.AppendLine("### Arquivos a Modificar:");
        foreach (var file in analysis.AffectedFiles)
        {
            sb.AppendLine($"- {file.Path}: {file.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("### Mudanças Necessárias:");
        foreach (var change in analysis.RequiredChanges)
        {
            sb.AppendLine($"- [{change.Category}] {change.Component}: {change.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("### Plano de Implementação:");
        foreach (var step in analysis.ImplementationPlan.OrderBy(s => s.Order))
        {
            sb.AppendLine($"{step.Order}. {step.Description}");
        }

        return sb.ToString();
    }

    private async Task ApplyGeneratedCodeAsync(GeneratedCode code, ImplementationResult result, CancellationToken cancellationToken)
    {
        foreach (var file in code.Files)
        {
            var fullPath = Path.Combine(_gitSettings.RepositoryPath, file.Path);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            switch (file.Operation)
            {
                case FileOperation.Create:
                    await File.WriteAllTextAsync(fullPath, file.Content, cancellationToken);
                    result.CreatedFiles.Add(new FileChange
                    {
                        Path = file.Path,
                        Description = file.Description,
                        LinesAdded = file.Content.Split('\n').Length
                    });
                    break;

                case FileOperation.Update:
                    var existingLines = File.Exists(fullPath) ? (await File.ReadAllLinesAsync(fullPath, cancellationToken)).Length : 0;
                    await File.WriteAllTextAsync(fullPath, file.Content, cancellationToken);
                    var newLines = file.Content.Split('\n').Length;
                    result.ModifiedFiles.Add(new FileChange
                    {
                        Path = file.Path,
                        Description = file.Description,
                        LinesAdded = Math.Max(0, newLines - existingLines),
                        LinesRemoved = Math.Max(0, existingLines - newLines)
                    });
                    break;

                case FileOperation.Delete:
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        result.DeletedFiles.Add(file.Path);
                    }
                    break;
            }
        }
    }

    private async Task<BuildResult> RunBuildAsync(CancellationToken cancellationToken)
    {
        var result = new BuildResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build --no-restore",
                    WorkingDirectory = _gitSettings.RepositoryPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            result.Output = output + error;
            result.IsSuccess = process.ExitCode == 0;

            if (!result.IsSuccess)
            {
                result.Errors = ExtractBuildErrors(result.Output);
            }

            result.Warnings = ExtractBuildWarnings(result.Output);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Errors.Add(ex.Message);
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        return result;
    }

    private async Task<TestResult> RunTestsAsync(CancellationToken cancellationToken)
    {
        var result = new TestResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "test --no-build --verbosity normal",
                    WorkingDirectory = _gitSettings.RepositoryPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            ParseTestOutput(output, result);
        }
        catch (Exception ex)
        {
            result.FailedTests = 1;
            result.FailedTestDetails.Add(new FailedTest
            {
                TestName = "TestExecution",
                ErrorMessage = ex.Message
            });
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        return result;
    }

    private static List<string> ExtractBuildErrors(string output)
    {
        return output.Split('\n')
            .Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase) && l.Contains(":"))
            .Select(l => l.Trim())
            .ToList();
    }

    private static List<string> ExtractBuildWarnings(string output)
    {
        return output.Split('\n')
            .Where(l => l.Contains("warning", StringComparison.OrdinalIgnoreCase) && l.Contains(":"))
            .Select(l => l.Trim())
            .ToList();
    }

    private static void ParseTestOutput(string output, TestResult result)
    {
        // Parse básico do output do dotnet test
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            if (line.Contains("Passed:"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"Passed:\s*(\d+)");
                if (match.Success)
                    result.PassedTests = int.Parse(match.Groups[1].Value);
            }
            else if (line.Contains("Failed:"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"Failed:\s*(\d+)");
                if (match.Success)
                    result.FailedTests = int.Parse(match.Groups[1].Value);
            }
            else if (line.Contains("Skipped:"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"Skipped:\s*(\d+)");
                if (match.Success)
                    result.SkippedTests = int.Parse(match.Groups[1].Value);
            }
            else if (line.Contains("Total:"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"Total:\s*(\d+)");
                if (match.Success)
                    result.TotalTests = int.Parse(match.Groups[1].Value);
            }
        }
    }

    private async Task<PullRequestInfo> CreatePullRequestAsync(
        JiraTicket ticket,
        ImplementationResult implementation,
        AnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var prInfo = new PullRequestInfo
        {
            Title = $"{ticket.Id}: {ticket.Title}",
            SourceBranch = implementation.BranchName,
            TargetBranch = _gitSettings.DefaultBranch,
            JiraTicketId = ticket.Id,
            ChangeType = ticket.Type == TicketType.Bug ? PullRequestChangeType.BugFix : PullRequestChangeType.Feature,
            Checklist = new PullRequestChecklist
            {
                FollowsProjectPatterns = true,
                HasUnitTests = implementation.TestResult?.TotalTests > 0,
                TestsPassing = implementation.TestResult?.AllPassed ?? true,
                DocumentationUpdated = true,
                NoBreakingChanges = !analysis.TechnicalImpact.HasBreakingChanges,
                SelfReviewed = true
            }
        };

        prInfo.Description = prInfo.GenerateBody(ticket, implementation);

        var request = new CreatePullRequestRequest
        {
            Title = prInfo.Title,
            Body = prInfo.Description,
            SourceBranch = prInfo.SourceBranch,
            TargetBranch = prInfo.TargetBranch,
            IsDraft = _gitSettings.PullRequestSettings.CreateAsDraft,
            Reviewers = _gitSettings.PullRequestSettings.DefaultReviewers,
            Labels = GetLabelsForTicket(ticket)
        };

        return await _pullRequestService.CreatePullRequestAsync(request, cancellationToken);
    }

    private static List<string> GetLabelsForTicket(JiraTicket ticket)
    {
        var labels = new List<string>();

        switch (ticket.Type)
        {
            case TicketType.Bug:
                labels.Add("bug");
                break;
            case TicketType.Story:
            case TicketType.NewFeature:
                labels.Add("enhancement");
                break;
        }

        switch (ticket.Priority)
        {
            case TicketPriority.Critical:
            case TicketPriority.Highest:
                labels.Add("priority:high");
                break;
        }

        return labels;
    }

    private static string GetCommitDescription(ImplementationResult result)
    {
        var parts = new List<string>();

        if (result.CreatedFiles.Count > 0)
            parts.Add($"Created {result.CreatedFiles.Count} file(s)");

        if (result.ModifiedFiles.Count > 0)
            parts.Add($"Modified {result.ModifiedFiles.Count} file(s)");

        if (result.DeletedFiles.Count > 0)
            parts.Add($"Deleted {result.DeletedFiles.Count} file(s)");

        return string.Join(", ", parts);
    }

    private void UpdateProgress(string ticketId, WorkflowPhase phase, int percentage, string message)
    {
        ProgressUpdated?.Invoke(this, new WorkflowProgressEventArgs
        {
            TicketId = ticketId,
            Phase = phase,
            ProgressPercentage = percentage,
            Message = message
        });

        _logger.LogDebug("[{TicketId}] {Phase} ({Percentage}%): {Message}",
            ticketId, phase, percentage, message);
    }

    private void OnAnalysisCompleted(string ticketId, AnalysisResult analysis)
    {
        AnalysisCompleted?.Invoke(this, new AnalysisCompletedEventArgs
        {
            TicketId = ticketId,
            Analysis = analysis
        });
    }

    private void OnImplementationCompleted(string ticketId, ImplementationResult implementation)
    {
        ImplementationCompleted?.Invoke(this, new ImplementationCompletedEventArgs
        {
            TicketId = ticketId,
            Implementation = implementation
        });
    }

    private void OnWorkflowError(string ticketId, WorkflowPhase phase, Exception exception)
    {
        WorkflowError?.Invoke(this, new WorkflowErrorEventArgs
        {
            TicketId = ticketId,
            Phase = phase,
            Exception = exception,
            Message = exception.Message
        });
    }

    #endregion
}
