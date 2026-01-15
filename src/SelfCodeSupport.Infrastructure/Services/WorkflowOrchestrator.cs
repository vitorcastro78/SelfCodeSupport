using System.Collections.Concurrent;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;
using SelfCodeSupport.Infrastructure.Data;
using SelfCodeSupport.Infrastructure.Data.Entities;

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
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ContextOptimizer _contextOptimizer;
    private readonly WorkflowSettings _settings;
    private readonly GitSettings _gitSettings;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    private readonly ConcurrentDictionary<string, WorkflowState> _workflowStates = new();
    private readonly ConcurrentDictionary<string, AnalysisResult> _pendingAnalyses = new();
    private readonly ConcurrentDictionary<string, WorkflowResult> _workflowResults = new();
    private readonly ConcurrentDictionary<string, WorkflowStatus> _workflowProgress = new();

    public event EventHandler<AnalysisCompletedEventArgs>? AnalysisCompleted;
    public event EventHandler<ImplementationCompletedEventArgs>? ImplementationCompleted;
    public event EventHandler<WorkflowErrorEventArgs>? WorkflowError;
    public event EventHandler<WorkflowProgressEventArgs>? ProgressUpdated;

    public WorkflowOrchestrator(
        IJiraService jiraService,
        IGitService gitService,
        IAnthropicService anthropicService,
        IPullRequestService pullRequestService,
        ICodeAnalysisService codeAnalysisService,
        IServiceProvider serviceProvider,
        ContextOptimizer contextOptimizer,
        IOptions<WorkflowSettings> workflowSettings,
        IOptions<GitSettings> gitSettings,
        ILogger<WorkflowOrchestrator> logger)
    {
        _jiraService = jiraService;
        _gitService = gitService;
        _anthropicService = anthropicService;
        _pullRequestService = pullRequestService;
        _codeAnalysisService = codeAnalysisService;
        _serviceProvider = serviceProvider;
        _contextOptimizer = contextOptimizer;
        _settings = workflowSettings.Value;
        _gitSettings = gitSettings.Value;
        _logger = logger;
    }

    public async Task<WorkflowResult> CreateWorkflowAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating workflow record for ticket {TicketId}", ticketId);

        // Verificar cache em memória primeiro
        if (_workflowResults.TryGetValue(ticketId, out var existingWorkflow))
        {
            _logger.LogInformation("Workflow found in memory cache for ticket {TicketId}, returning cached workflow", ticketId);
            return existingWorkflow;
        }

        // Tentar carregar do banco de dados
        var dbWorkflow = await LoadWorkflowFromDatabaseAsync(ticketId);
        if (dbWorkflow != null)
        {
            _logger.LogInformation("Workflow loaded from database for ticket {TicketId}", ticketId);
            _workflowResults[ticketId] = dbWorkflow;
            return dbWorkflow;
        }

        // Buscar dados do ticket no JIRA
        JiraTicket ticket;
        try
        {
            ticket = await _jiraService.GetTicketAsync(ticketId, cancellationToken);
            _logger.LogInformation("Ticket {TicketId} fetched from JIRA: {Title}", ticketId, ticket.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticket {TicketId} from JIRA", ticketId);
            throw new InvalidOperationException($"Could not fetch ticket {ticketId} from JIRA: {ex.Message}", ex);
        }

        // Criar registro de Workflow com status pending
        var workflow = new WorkflowResult
        {
            TicketId = ticketId,
            TicketTitle = ticket.Title,
            FinalPhase = WorkflowPhase.NotStarted,
            IsSuccess = false,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null
        };

        // Armazenar workflow com estado pending (cache em memória)
        _workflowResults[ticketId] = workflow;
        _workflowStates[ticketId] = WorkflowState.Paused; // Paused indica que está aguardando para iniciar

        // Persistir no banco de dados
        await SaveWorkflowToDatabaseAsync(workflow);

        // Atualizar progresso
        UpdateProgress(ticketId, WorkflowPhase.NotStarted, 0, "Workflow created, pending analysis start");

        _logger.LogInformation("Workflow created successfully for ticket {TicketId} with status pending", ticketId);

        return workflow;
    }

    public async Task<WorkflowResult> StartWorkflowAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workflow for ticket {TicketId}", ticketId);

        var result = new WorkflowResult
        {
            TicketId = ticketId,
            StartedAt = DateTime.UtcNow
        };

        _workflowStates[ticketId] = WorkflowState.Running;
        _workflowResults[ticketId] = result;
        
        // Persistir no banco de dados
        await SaveWorkflowToDatabaseAsync(result);

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
                _logger.LogInformation("Analysis completed for {TicketId}. Awaiting approval.", ticketId);
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
            
            // Persistir no banco de dados
            await SaveWorkflowToDatabaseAsync(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in workflow for ticket {TicketId}", ticketId);

            result.FinalPhase = WorkflowPhase.Failed;
            result.IsSuccess = false;
            result.Errors.Add(ex.Message);
            result.CompletedAt = DateTime.UtcNow;

            _workflowStates[ticketId] = WorkflowState.Failed;
            
            // Persistir no banco de dados
            await SaveWorkflowToDatabaseAsync(result);

            OnWorkflowError(ticketId, result.FinalPhase, ex);

            throw;
        }
    }

    public async Task<AnalysisResult> AnalyzeAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting analysis for ticket {TicketId}", ticketId);
        UpdateProgress(ticketId, WorkflowPhase.FetchingTicket, 5, "Fetching ticket information...");

        // 1. Obter ticket do JIRA
        var ticket = await _jiraService.GetTicketAsync(ticketId, cancellationToken);
        UpdateProgress(ticketId, WorkflowPhase.FetchingTicket, 15, "Ticket retrieved successfully");

        // 1.5. Verificar cache (economiza créditos)
        var ticketHash = ComputeTicketHash(ticket);
        
        // Resolver IAnalysisCacheService do scope (Scoped service)
        using var cacheScope = _serviceProvider.CreateScope();
        var analysisCache = cacheScope.ServiceProvider.GetRequiredService<IAnalysisCacheService>();
        
        var cachedAnalysis = await analysisCache.GetCachedAnalysisAsync(ticketId, ticketHash);
        if (cachedAnalysis != null)
        {
            _logger.LogInformation("Analysis found in cache for ticket {TicketId} - saving credits!", ticketId);
            UpdateProgress(ticketId, WorkflowPhase.WaitingApproval, 100, "Analysis retrieved from cache.");
            _pendingAnalyses[ticketId] = cachedAnalysis;
            OnAnalysisCompleted(ticketId, cachedAnalysis);
            return cachedAnalysis;
        }

        // 2. Preparar workspace (local ou temporário)
        UpdateProgress(ticketId, WorkflowPhase.AnalyzingCode, 20, "Preparing workspace...");
        
        // Resolver WorkspaceManager do scope atual (Scoped service)
        // Criar scope que será descartado no finally
        var scope = _serviceProvider.CreateScope();
        WorkspaceManager? workspaceManager = null;
        string? originalRepositoryPath = null;
        string? workspacePath = null;
        
        try
        {
            workspaceManager = scope.ServiceProvider.GetRequiredService<WorkspaceManager>();
            workspacePath = await workspaceManager.GetOrCreateWorkspaceAsync(ticketId, cancellationToken);
        
            // Se workspace temporário foi criado, mudar repositório do GitService
            if (workspacePath != _gitSettings.RepositoryPath)
            {
                try
                {
                    originalRepositoryPath = _gitSettings.RepositoryPath;
                    _logger.LogInformation("Switching to temporary workspace: {Path}", workspacePath);
                    _gitService.SwitchRepository(workspacePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error switching to temporary workspace");
                    throw;
                }
            }
        
            // Verificar se há mudanças locais antes de fazer checkout
            GitStatus? status = null;
            string? originalBranch = null;
            bool hasLocalChanges = false;
        
            try
            {
                status = await _gitService.GetStatusAsync(cancellationToken);
                originalBranch = status.CurrentBranch;
                hasLocalChanges = !status.IsClean;
            }
            catch
            {
                // Se falhar, assume que não há mudanças
                hasLocalChanges = false;
            }
        
            // Criar branch temporário para análise (isolado do trabalho local)
            var analysisBranchName = $"analysis/{ticketId.ToLowerInvariant().Replace("-", "/")}";
        
            try
            {
                // Atualizar branch padrão
        await _gitService.PullAsync(cancellationToken);
        await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);

                // Criar branch temporário para análise (se não existir)
                var branches = _gitService.ListBranches();
                if (!branches.Contains(analysisBranchName))
                {
                    await _gitService.CreateBranchAsync(analysisBranchName, _gitSettings.DefaultBranch, cancellationToken);
                }
                else
                {
                    // Se já existe, fazer checkout (não precisa de pull pois é branch local temporário)
                    await _gitService.CheckoutAsync(analysisBranchName, cancellationToken);
                }
            
                _logger.LogInformation("Analysis will be performed on temporary branch: {BranchName}", analysisBranchName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating temporary branch, using default branch");
                // Fallback: usar branch padrão se falhar
                await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);
            }

            // 3. Buscar arquivos relacionados usando análise semântica (como Cursor IDE)
            UpdateProgress(ticketId, WorkflowPhase.AnalyzingCode, 30, "Analyzing code semantically...");
            var codeContext = await BuildSemanticCodeContextAsync(ticket, cancellationToken);

            // 3.5. Otimizar contexto para reduzir tokens (economiza créditos)
            var optimizedContext = _contextOptimizer.OptimizeContext(codeContext, maxSize: 30000);
            _logger.LogInformation("Context optimized: {OriginalSize} -> {OptimizedSize} characters", 
                codeContext.Length, optimizedContext.Length);

        // 4. Analisar com Claude
            UpdateProgress(ticketId, WorkflowPhase.AnalyzingCode, 50, "Analyzing code with AI...");
            var analysis = await _anthropicService.AnalyzeTicketAsync(ticket, optimizedContext, cancellationToken);

            // 4.5. Armazenar em cache para futuras análises similares
            using var cacheScope3 = _serviceProvider.CreateScope();
            var analysisCache3 = cacheScope3.ServiceProvider.GetRequiredService<IAnalysisCacheService>();
            await analysisCache3.CacheAnalysisAsync(ticketId, ticketHash, analysis);

            // 5. Armazenar análise pendente (não envia para JIRA automaticamente)
        _pendingAnalyses[ticketId] = analysis;

            UpdateProgress(ticketId, WorkflowPhase.WaitingApproval, 100, "Analysis completed. Awaiting approval.");

            // Restaurar branch original se havia mudanças locais
            try
            {
                if (hasLocalChanges && !string.IsNullOrEmpty(originalBranch))
                {
                    _logger.LogInformation("Restoring original branch: {BranchName}", originalBranch);
                    await _gitService.CheckoutAsync(originalBranch, cancellationToken);
                }
                else
                {
                    // Voltar para branch padrão
                    await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error restoring original branch");
            }

        OnAnalysisCompleted(ticketId, analysis);

        return analysis;
        }
        finally
        {
            // Limpar workspace temporário se foi criado
            if (workspaceManager != null)
            {
                try
                {
                    workspaceManager.CleanupTemporaryWorkspace();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up temporary workspace");
                }
            }

            // Restaurar repositório original se foi usado workspace temporário
            if (!string.IsNullOrEmpty(originalRepositoryPath) && !string.IsNullOrEmpty(workspacePath) && originalRepositoryPath != workspacePath)
            {
                try
                {
                    _logger.LogInformation("Restoring original repository: {Path}", originalRepositoryPath);
                    _gitService.SwitchRepository(originalRepositoryPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error restoring original repository");
                }
            }

            // Descarta o scope (libera o WorkspaceManager)
            scope?.Dispose();
        }
    }

    public async Task<ImplementationResult> ApproveAndImplementAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Implementation approved for ticket {TicketId}", ticketId);

        if (!_pendingAnalyses.TryGetValue(ticketId, out var analysis))
        {
            throw new InvalidOperationException($"No pending analysis found for ticket {ticketId}");
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
                await _jiraService.AddCommentAsync(ticketId, "🚀 **Implementation started automatically.**", cancellationToken);
            }

            // 3. Criar branch
            UpdateProgress(ticketId, WorkflowPhase.CreatingBranch, 10, "Creating branch...");
            var branchName = _gitService.GenerateBranchName(ticket);
            result.BranchName = branchName;

            await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);
            await _gitService.PullAsync(cancellationToken);
            await _gitService.CreateBranchAsync(branchName, cancellationToken: cancellationToken);

            // 4. Gerar código
            UpdateProgress(ticketId, WorkflowPhase.Implementing, 20, "Generating code...");
            var codeContext = await BuildSemanticCodeContextAsync(ticket, cancellationToken);
            var requirements = BuildRequirementsFromAnalysis(analysis);

            var generatedCode = await _anthropicService.GenerateCodeAsync(
                $"Ticket: {ticket.Id} - {ticket.Title}\n{ticket.Description}",
                requirements,
                codeContext,
                cancellationToken);

            // 5. Aplicar mudanças
            UpdateProgress(ticketId, WorkflowPhase.Implementing, 40, "Applying changes...");
            await ApplyGeneratedCodeAsync(generatedCode, result, cancellationToken);

            // 6. Build
            if (_settings.AutoBuild)
            {
                UpdateProgress(ticketId, WorkflowPhase.Building, 50, "Running build...");
                result.BuildResult = await RunBuildAsync(cancellationToken);

                if (!result.BuildResult.IsSuccess)
                {
                    result.Status = ImplementationStatus.BuildFailed;
                    throw new InvalidOperationException("Build failed: " + string.Join(", ", result.BuildResult.Errors));
                }
            }

            // 7. Testes
            if (_settings.AutoRunTests)
            {
                UpdateProgress(ticketId, WorkflowPhase.Testing, 60, "Running tests...");
                result.TestResult = await RunTestsAsync(cancellationToken);

                if (!result.TestResult.AllPassed)
                {
                    result.Status = ImplementationStatus.TestsFailed;
                    result.Warnings.Add($"{result.TestResult.FailedTests} test(s) failed");
                }
            }

            // 8. Commit
            UpdateProgress(ticketId, WorkflowPhase.Committing, 70, "Committing changes...");
            await _gitService.StageFilesAsync(cancellationToken: cancellationToken);
            var commitMessage = _gitService.GenerateCommitMessage(ticket, GetCommitDescription(result));
            var commit = await _gitService.CommitAsync(commitMessage, cancellationToken);
            result.Commits.Add(commit);

            // 9. Push
            UpdateProgress(ticketId, WorkflowPhase.Pushing, 80, "Pushing changes...");
            await _gitService.PushAsync(branchName, cancellationToken);

            // 10. Criar PR
            if (_settings.AutoCreatePullRequest)
            {
                UpdateProgress(ticketId, WorkflowPhase.CreatingPullRequest, 90, "Creating Pull Request...");
                var prInfo = await CreatePullRequestAsync(ticket, result, analysis, cancellationToken);
                result.PullRequestUrl = prInfo.Url;
                result.PullRequestNumber = prInfo.Number;
            }

            // 11. Atualizar JIRA
            if (_settings.AutoUpdateJira)
            {
                UpdateProgress(ticketId, WorkflowPhase.UpdatingJira, 95, "Updating JIRA...");
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

            UpdateProgress(ticketId, WorkflowPhase.Completed, 100, "Implementation completed!");

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
        _logger.LogInformation("Revision requested for ticket {TicketId}: {Feedback}", ticketId, feedback);

        // Re-executar análise com o feedback (não envia para JIRA automaticamente)
        return await AnalyzeAsync(ticketId, cancellationToken);
    }

    public async Task CancelWorkflowAsync(string ticketId, string reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelling workflow for ticket {TicketId}: {Reason}", ticketId, reason);

        _workflowStates[ticketId] = WorkflowState.Cancelled;
        _pendingAnalyses.TryRemove(ticketId, out _);

        if (_settings.AutoUpdateJira)
        {
            await _jiraService.AddCommentAsync(ticketId, $"❌ **Workflow cancelled:**\n{reason}", cancellationToken);
        }

        // Descartar mudanças locais se houver
        if (!_gitService.IsClean())
        {
            await _gitService.DiscardChangesAsync(cancellationToken);
            await _gitService.CheckoutAsync(_gitSettings.DefaultBranch, cancellationToken);
        }
    }

    public async Task<WorkflowStatus> GetWorkflowStatusAsync(string ticketId)
    {
        WorkflowStatus? status = null;
        AnalysisResult? analysis = null;

        // Verificar cache em memória primeiro
        if (_workflowProgress.TryGetValue(ticketId, out var progress))
        {
            // Atualizar estado atual
            progress.State = _workflowStates.GetValueOrDefault(ticketId, WorkflowState.Completed);
            status = progress;
        }

        // Buscar análise em memória
        if (_pendingAnalyses.TryGetValue(ticketId, out var pendingAnalysis))
        {
            analysis = pendingAnalysis;
        }
        else if (_workflowResults.TryGetValue(ticketId, out var workflowResult) && workflowResult.Analysis != null)
        {
            analysis = workflowResult.Analysis;
        }

        // Tentar carregar do banco de dados se não encontrado em memória
        if (status == null)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var latestProgress = await dbContext.WorkflowProgress
                    .Where(p => p.TicketId == ticketId)
                    .OrderByDescending(p => p.Timestamp)
                    .FirstOrDefaultAsync();

                if (latestProgress != null)
                {
                    status = new WorkflowStatus
                    {
                        TicketId = latestProgress.TicketId,
                        CurrentPhase = Enum.Parse<WorkflowPhase>(latestProgress.Phase),
                        State = Enum.Parse<WorkflowState>(latestProgress.State),
                        ProgressPercentage = latestProgress.ProgressPercentage,
                        Message = latestProgress.Message,
                        LastUpdated = latestProgress.Timestamp
                    };
                    
                    // Atualizar cache em memória
                    _workflowProgress[ticketId] = status;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading workflow status from database for ticket {TicketId}", ticketId);
            }
        }

        // Se ainda não encontrou status, buscar workflow do banco
        if (status == null || analysis == null)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var workflowEntity = await dbContext.Workflows
                    .FirstOrDefaultAsync(w => w.TicketId == ticketId);

                if (workflowEntity != null)
                {
                    if (status == null)
                    {
                        status = new WorkflowStatus
                        {
                            TicketId = workflowEntity.TicketId,
                            CurrentPhase = Enum.Parse<WorkflowPhase>(workflowEntity.CurrentPhase),
                            State = Enum.Parse<WorkflowState>(workflowEntity.State),
                            ProgressPercentage = workflowEntity.State == "Completed" ? 100 : 0,
                            Message = workflowEntity.State == "Completed" ? "Completed" : "In progress",
                            LastUpdated = workflowEntity.LastUpdatedAt
                        };
                    }

                    // Carregar análise do banco se não encontrada em memória
                    if (analysis == null && !string.IsNullOrEmpty(workflowEntity.AnalysisJson))
                    {
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        analysis = JsonSerializer.Deserialize<AnalysisResult>(workflowEntity.AnalysisJson, jsonOptions);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading workflow from database for ticket {TicketId}", ticketId);
            }
        }

        // Fallback: criar status básico
        if (status == null)
    {
        var state = _workflowStates.GetValueOrDefault(ticketId, WorkflowState.Completed);
        var result = _workflowResults.GetValueOrDefault(ticketId);

            status = new WorkflowStatus
        {
            TicketId = ticketId,
            CurrentPhase = result?.FinalPhase ?? WorkflowPhase.NotStarted,
            State = state,
                ProgressPercentage = state == WorkflowState.Completed ? 100 : 0,
                Message = state == WorkflowState.Completed ? "Completed" : "Not started",
            LastUpdated = DateTime.UtcNow
            };
        }

        // Mapear análise para DTO se disponível
        if (analysis != null)
        {
            status.Analysis = MapToTechnicalAnalysisDto(analysis);
        }

        return status;
    }

    private static Core.Interfaces.TechnicalAnalysisDto MapToTechnicalAnalysisDto(AnalysisResult analysis)
    {
        return new Core.Interfaces.TechnicalAnalysisDto
        {
            AnalyzedAt = analysis.AnalyzedAt,
            Status = analysis.Status.ToString(),
            AffectedFiles = analysis.AffectedFiles.Select(f => new Core.Interfaces.AffectedFileDto
            {
                Path = f.Path,
                Action = f.ChangeType.ToString().ToLowerInvariant(),
                Description = f.Description,
                MethodsAffected = f.MethodsAffected ?? new List<string>()
            }).ToList(),
            RequiredChanges = analysis.RequiredChanges.Select(c => new Core.Interfaces.RequiredChangeDto
            {
                Component = c.Component,
                Description = c.Description,
                Category = c.Category.ToString()
            }).ToList(),
            TechnicalImpact = new Core.Interfaces.TechnicalImpactDto
            {
                HasBreakingChanges = analysis.TechnicalImpact.HasBreakingChanges,
                RequiresMigration = analysis.TechnicalImpact.RequiresMigration,
                AffectsPerformance = analysis.TechnicalImpact.AffectsPerformance,
                HasSecurityImplications = analysis.TechnicalImpact.HasSecurityImplications,
                NewDependencies = analysis.TechnicalImpact.NewDependencies ?? new List<string>(),
                AffectedEndpoints = analysis.TechnicalImpact.AffectedEndpoints ?? new List<string>(),
                AffectedServices = analysis.TechnicalImpact.AffectedServices ?? new List<string>()
            },
            Risks = analysis.Risks.Select(r => new Core.Interfaces.RiskDto
            {
                Severity = r.Severity.ToString().ToLowerInvariant(),
                Description = r.Description,
                Mitigation = r.Mitigation
            }).ToList(),
            Improvements = analysis.Opportunities.Select(o => new Core.Interfaces.ImprovementDto
            {
                Description = o.Description,
                Type = o.Type.ToString(),
                EstimatedEffortHours = o.EstimatedEffortHours
            }).ToList(),
            ImplementationPlan = analysis.ImplementationPlan.Select(step => new Core.Interfaces.ImplementationPlanItemDto
            {
                Order = step.Order,
                Description = step.Description,
                Completed = false, // Sempre false, pois ainda não foi implementado
                Files = step.Files ?? new List<string>(),
                EstimatedMinutes = step.EstimatedMinutes,
                EstimatedTime = FormatEstimatedTime(step.EstimatedMinutes)
            }).ToList(),
            ValidationCriteria = analysis.ValidationCriteria.Select(v => new Core.Interfaces.ValidationCriteriaDto
            {
                Description = v.Description,
                Type = v.Type.ToString(),
                IsAutomatable = v.IsAutomatable
            }).ToList(),
            Complexity = analysis.Complexity.ToString(),
            EstimatedEffort = $"{analysis.EstimatedEffortHours} horas",
            EstimatedEffortHours = analysis.EstimatedEffortHours
        };
    }

    private static string FormatEstimatedTime(int minutes)
    {
        if (minutes < 60)
        {
            return $"{minutes} minutos";
        }
        else if (minutes == 60)
        {
            return "1 hora";
        }
        else
        {
            var hours = minutes / 60;
            var remainingMinutes = minutes % 60;
            if (remainingMinutes == 0)
            {
                return $"{hours} horas";
            }
            return $"{hours}h {remainingMinutes}min";
        }
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

    public async Task SendAnalysisToJiraAsync(string ticketId, string analysisComment, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending analysis to JIRA for ticket {TicketId}", ticketId);

        if (string.IsNullOrWhiteSpace(analysisComment))
        {
            throw new ArgumentException("Analysis comment cannot be empty", nameof(analysisComment));
        }

        await _jiraService.AddCommentAsync(ticketId, analysisComment, cancellationToken);
        
        _logger.LogInformation("Analysis sent to JIRA successfully for ticket {TicketId}", ticketId);
    }

    #region Private Helper Methods

    /// <summary>
    /// Constrói contexto de código usando análise semântica (similar ao Cursor IDE)
    /// </summary>
    private async Task<string> BuildSemanticCodeContextAsync(JiraTicket ticket, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building semantic context for ticket {TicketId}", ticket.Id);

        try
        {
            // Usar análise semântica para encontrar arquivos relevantes
            var semanticContext = await _codeAnalysisService.BuildSemanticContextAsync(ticket, cancellationToken);

            var sb = new System.Text.StringBuilder();
            const int maxContextSize = 50000; // Limitar tamanho total do contexto (50KB)

            // Adicionar contexto estruturado
            sb.AppendLine(semanticContext.StructuredContext);
            sb.AppendLine();

            // Adicionar conteúdo dos arquivos mais relevantes (otimizado)
            foreach (var file in semanticContext.RelevantFiles.OrderByDescending(f => f.RelevanceScore).Take(5))
            {
                if (semanticContext.FileContents.TryGetValue(file.FilePath, out var content))
                {
                    // Criar resumo para arquivos grandes (economiza tokens)
                    var optimizedContent = content.Length > 5000 
                        ? _contextOptimizer.CreateFileSummary(file.FilePath, content, maxLines: 100)
                        : content;

                    var fileSection = $"// File: {file.FilePath} (Relevância: {file.RelevanceScore:F2})\n";
                    fileSection += $"// Razões: {string.Join(", ", file.Reasons)}\n";
                    fileSection += $"{optimizedContent}\n\n";

                    if (sb.Length + fileSection.Length > maxContextSize)
                    {
                        sb.AppendLine("// ... (contexto truncado devido ao limite de tamanho)");
                        break;
                    }

                    sb.Append(fileSection);
                }
            }

            _logger.LogInformation("Semantic context built: {FileCount} files, {SymbolCount} symbols", 
                semanticContext.RelevantFiles.Count, semanticContext.RelevantSymbols.Count);

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in semantic analysis, using fallback to text search");
            // Fallback para método antigo se análise semântica falhar
            return await BuildCodeContextAsync(ticket, cancellationToken);
        }
    }

    /// <summary>
    /// Método de fallback usando busca textual (mantido para compatibilidade)
    /// </summary>
    private async Task<string> BuildCodeContextAsync(JiraTicket ticket, CancellationToken cancellationToken)
    {
        const int maxContextSize = 50000; // Limitar tamanho total do contexto (50KB)
        const int maxFileSize = 10000; // Limitar tamanho de cada arquivo (10KB)
        const int maxFilesPerTerm = 2; // Reduzir de 3 para 2
        const int maxSearchTerms = 3; // Reduzir de 5 para 3

        var sb = new System.Text.StringBuilder();
        var searchTerms = ExtractSearchTerms(ticket);
        var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Paralelizar buscas por termos
        var searchTasks = searchTerms
            .Take(maxSearchTerms)
            .Select(async term =>
            {
                try
        {
            var results = await _gitService.SearchInFilesAsync(term, "*.cs", cancellationToken);
                    return results
                        .Where(r => !ShouldIgnoreFile(r.FilePath) && !processedFiles.Contains(r.FilePath))
                        .Take(maxFilesPerTerm)
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error searching for term {Term}", term);
                    return new List<FileSearchResult>();
                }
            });

        var allResults = (await Task.WhenAll(searchTasks))
            .SelectMany(r => r)
            .GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()) // Remover duplicatas
            .Take(10) // Limitar total de arquivos
            .ToList();

        // Processar arquivos encontrados em paralelo
        var fileTasks = allResults.Select(async result =>
        {
            try
            {
                if (processedFiles.Contains(result.FilePath))
                    return null;

                processedFiles.Add(result.FilePath);

                    var content = await _gitService.GetFileContentAsync(result.FilePath, cancellationToken);
                
                // Limitar tamanho do arquivo
                if (content.Length > maxFileSize)
                {
                    content = content.Substring(0, maxFileSize) + "\n// ... (arquivo truncado)";
                }

                return new { Path = result.FilePath, Content = content };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading file {FilePath}", result.FilePath);
                return null;
            }
        });

        var fileContents = (await Task.WhenAll(fileTasks))
            .Where(f => f != null)
            .ToList();

        // Adicionar arquivos ao contexto respeitando limite de tamanho
        foreach (var file in fileContents)
        {
            var fileSection = $"// File: {file!.Path}\n{file.Content}\n\n";
            
            if (sb.Length + fileSection.Length > maxContextSize)
            {
                sb.AppendLine("// ... (contexto truncado devido ao limite de tamanho)");
                break;
            }

            sb.Append(fileSection);
        }

        // Adicionar arquivos de componentes mencionados (limitado e em paralelo)
        if (ticket.Components.Any() && sb.Length < maxContextSize)
        {
            var componentTasks = ticket.Components
                .Take(2) // Limitar componentes
                .Select(async component =>
                {
                    try
        {
                        var files = _gitService.ListFiles(pattern: $"*{component}*.cs")
                            .Where(f => !ShouldIgnoreFile(f) && !processedFiles.Contains(f))
                            .Take(2)
                            .ToList();

                        var fileContents = new List<(string Path, string Content)>();
                        foreach (var file in files)
            {
                            if (processedFiles.Contains(file) || sb.Length >= maxContextSize)
                                break;

                            processedFiles.Add(file);
                    var content = await _gitService.GetFileContentAsync(file, cancellationToken);
                            
                            if (content.Length > maxFileSize)
                            {
                                content = content.Substring(0, maxFileSize) + "\n// ... (arquivo truncado)";
                            }

                            fileContents.Add((file, content));
                        }

                        return fileContents;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error searching for component files {Component}", component);
                        return new List<(string, string)>();
                    }
                });

            var componentFiles = (await Task.WhenAll(componentTasks))
                .SelectMany(f => f)
                .ToList();

            foreach (var (path, content) in componentFiles)
            {
                var fileSection = $"// File: {path}\n{content}\n\n";
                
                if (sb.Length + fileSection.Length > maxContextSize)
                {
                    sb.AppendLine("// ... (contexto truncado devido ao limite de tamanho)");
                    break;
                }

                sb.Append(fileSection);
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
        var state = _workflowStates.GetValueOrDefault(ticketId, WorkflowState.Running);
        
        // Armazenar progresso atual em memória (cache)
        _workflowProgress.AddOrUpdate(ticketId, 
            new WorkflowStatus
            {
                TicketId = ticketId,
                CurrentPhase = phase,
                ProgressPercentage = percentage,
                Message = message,
                LastUpdated = DateTime.UtcNow,
                State = state
            },
            (key, existing) => new WorkflowStatus
            {
                TicketId = ticketId,
                CurrentPhase = phase,
                ProgressPercentage = percentage,
                Message = message,
                LastUpdated = DateTime.UtcNow,
                State = existing.State
            });

        // Persistir progresso no banco de dados (assíncrono, não bloqueia)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var progressEntity = new WorkflowProgressEntity
                {
                    TicketId = ticketId,
                    Phase = phase.ToString(),
                    State = state.ToString(),
                    ProgressPercentage = percentage,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                };
                
                dbContext.WorkflowProgress.Add(progressEntity);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error saving workflow progress to database for ticket {TicketId}", ticketId);
            }
        });

        // Notificar via SignalR (se disponível)
        _ = Task.Run(async () =>
        {
            try
            {
                var notifierType = Type.GetType("SelfCodeSupport.API.Services.WorkflowProgressNotifier, SelfCodeSupport.API");
                if (notifierType != null)
                {
                    var notifier = _serviceProvider.GetService(notifierType);
                    if (notifier != null)
                    {
                        var method = notifierType.GetMethod("NotifyProgressAsync");
                        if (method != null)
                        {
                            await (Task)method.Invoke(notifier, new object[] { ticketId, phase, percentage, message })!;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending SignalR notification for ticket {TicketId}", ticketId);
            }
        });

        // Disparar evento
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
        // Notificar via SignalR (se disponível)
        _ = Task.Run(async () =>
        {
            try
            {
                var notifierType = Type.GetType("SelfCodeSupport.API.Services.WorkflowProgressNotifier, SelfCodeSupport.API");
                if (notifierType != null)
                {
                    var notifier = _serviceProvider.GetService(notifierType);
                    if (notifier != null)
                    {
                        var method = notifierType.GetMethod("NotifyAnalysisCompletedAsync");
                        if (method != null)
                        {
                            await (Task)method.Invoke(notifier, new object[] { ticketId, analysis })!;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending SignalR analysis completed notification for ticket {TicketId}", ticketId);
            }
        });

        AnalysisCompleted?.Invoke(this, new AnalysisCompletedEventArgs
        {
            TicketId = ticketId,
            Analysis = analysis
        });
    }

    private void OnImplementationCompleted(string ticketId, ImplementationResult implementation)
    {
        // Notificar via SignalR (se disponível)
        _ = Task.Run(async () =>
        {
            try
            {
                var notifierType = Type.GetType("SelfCodeSupport.API.Services.WorkflowProgressNotifier, SelfCodeSupport.API");
                if (notifierType != null)
                {
                    var notifier = _serviceProvider.GetService(notifierType);
                    if (notifier != null)
                    {
                        var method = notifierType.GetMethod("NotifyImplementationCompletedAsync");
                        if (method != null)
                        {
                            await (Task)method.Invoke(notifier, new object[] { ticketId, implementation })!;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending SignalR implementation completed notification for ticket {TicketId}", ticketId);
            }
        });

        ImplementationCompleted?.Invoke(this, new ImplementationCompletedEventArgs
        {
            TicketId = ticketId,
            Implementation = implementation
        });
    }

    private void OnWorkflowError(string ticketId, WorkflowPhase phase, Exception exception)
    {
        // Notificar via SignalR (se disponível)
        _ = Task.Run(async () =>
        {
            try
            {
                var notifierType = Type.GetType("SelfCodeSupport.API.Services.WorkflowProgressNotifier, SelfCodeSupport.API");
                if (notifierType != null)
                {
                    var notifier = _serviceProvider.GetService(notifierType);
                    if (notifier != null)
                    {
                        var method = notifierType.GetMethod("NotifyErrorAsync");
                        if (method != null)
                        {
                            await (Task)method.Invoke(notifier, new object[] { ticketId, phase, exception.Message })!;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending SignalR error notification for ticket {TicketId}", ticketId);
            }
        });

        WorkflowError?.Invoke(this, new WorkflowErrorEventArgs
        {
            TicketId = ticketId,
            Phase = phase,
            Exception = exception,
            Message = exception.Message
        });
    }

    private static string ComputeTicketHash(JiraTicket ticket)
    {
        var content = $"{ticket.Id}|{ticket.Title}|{ticket.Description}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash)[..16]; // Primeiros 16 caracteres
    }

    private async Task SaveWorkflowToDatabaseAsync(WorkflowResult workflow)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var entity = await dbContext.Workflows
                .FirstOrDefaultAsync(w => w.TicketId == workflow.TicketId);

            if (entity == null)
            {
                entity = new WorkflowEntity
                {
                    TicketId = workflow.TicketId,
                    TicketTitle = workflow.TicketTitle,
                    CurrentPhase = workflow.FinalPhase.ToString(),
                    State = _workflowStates.GetValueOrDefault(workflow.TicketId, WorkflowState.Running).ToString(),
                    StartedAt = workflow.StartedAt,
                    LastUpdatedAt = DateTime.UtcNow
                };
                dbContext.Workflows.Add(entity);
            }
            else
            {
                entity.TicketTitle = workflow.TicketTitle;
                entity.CurrentPhase = workflow.FinalPhase.ToString();
                entity.State = _workflowStates.GetValueOrDefault(workflow.TicketId, WorkflowState.Running).ToString();
                entity.LastUpdatedAt = DateTime.UtcNow;
            }

            // Serializar objetos complexos
            entity.AnalysisJson = workflow.Analysis != null 
                ? JsonSerializer.Serialize(workflow.Analysis, jsonOptions) 
                : null;
            entity.ImplementationJson = workflow.Implementation != null 
                ? JsonSerializer.Serialize(workflow.Implementation, jsonOptions) 
                : null;
            entity.PullRequestJson = workflow.PullRequest != null 
                ? JsonSerializer.Serialize(workflow.PullRequest, jsonOptions) 
                : null;
            entity.ErrorsJson = workflow.Errors.Count > 0 
                ? JsonSerializer.Serialize(workflow.Errors, jsonOptions) 
                : null;
            entity.IsSuccess = workflow.IsSuccess;
            entity.CompletedAt = workflow.CompletedAt;

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error saving workflow to database for ticket {TicketId}", workflow.TicketId);
        }
    }

    private async Task<WorkflowResult?> LoadWorkflowFromDatabaseAsync(string ticketId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var entity = await dbContext.Workflows
                .FirstOrDefaultAsync(w => w.TicketId == ticketId);

            if (entity == null)
            {
                return null;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var workflow = new WorkflowResult
            {
                TicketId = entity.TicketId,
                TicketTitle = entity.TicketTitle,
                FinalPhase = Enum.Parse<WorkflowPhase>(entity.CurrentPhase),
                IsSuccess = entity.IsSuccess,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt
            };

            // Deserializar objetos complexos
            if (!string.IsNullOrEmpty(entity.AnalysisJson))
            {
                workflow.Analysis = JsonSerializer.Deserialize<AnalysisResult>(entity.AnalysisJson, jsonOptions);
            }
            if (!string.IsNullOrEmpty(entity.ImplementationJson))
            {
                workflow.Implementation = JsonSerializer.Deserialize<ImplementationResult>(entity.ImplementationJson, jsonOptions);
            }
            if (!string.IsNullOrEmpty(entity.PullRequestJson))
            {
                workflow.PullRequest = JsonSerializer.Deserialize<PullRequestInfo>(entity.PullRequestJson, jsonOptions);
            }
            if (!string.IsNullOrEmpty(entity.ErrorsJson))
            {
                workflow.Errors = JsonSerializer.Deserialize<List<string>>(entity.ErrorsJson, jsonOptions) ?? new List<string>();
            }

            // Restaurar estado em memória
            if (Enum.TryParse<WorkflowState>(entity.State, out var state))
            {
                _workflowStates[ticketId] = state;
            }

            return workflow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading workflow from database for ticket {TicketId}", ticketId);
            return null;
        }
    }

    #endregion
}
