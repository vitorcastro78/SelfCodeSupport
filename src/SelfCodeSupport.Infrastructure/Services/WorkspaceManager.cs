using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Gerencia workspaces temporários para análise (útil para containers/servidores)
/// </summary>
public class WorkspaceManager : IDisposable
{
    private readonly GitSettings _gitSettings;
    private readonly ILogger<WorkspaceManager> _logger;
    private readonly IGitService _gitService;
    private string? _temporaryWorkspacePath;
    private bool _isTemporaryWorkspace;

    public WorkspaceManager(
        IOptions<GitSettings> gitSettings,
        ILogger<WorkspaceManager> logger,
        IGitService gitService)
    {
        _gitSettings = gitSettings.Value;
        _logger = logger;
        _gitService = gitService;
    }

    /// <summary>
    /// Obtém ou cria workspace para análise
    /// </summary>
    public async Task<string> GetOrCreateWorkspaceAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        // Se há repositório local configurado e existe, usar ele
        if (!string.IsNullOrEmpty(_gitSettings.RepositoryPath) && 
            Directory.Exists(_gitSettings.RepositoryPath) &&
            !_gitSettings.UseTemporaryWorkspace)
        {
            _logger.LogInformation("Using local repository: {Path}", _gitSettings.RepositoryPath);
            _isTemporaryWorkspace = false;
            return _gitSettings.RepositoryPath;
        }

        // Caso contrário, criar workspace temporário
        return await CreateTemporaryWorkspaceAsync(ticketId, cancellationToken);
    }

    private async Task<string> CreateTemporaryWorkspaceAsync(string ticketId, CancellationToken cancellationToken)
    {
        var basePath = _gitSettings.TemporaryWorkspaceBasePath ?? Path.GetTempPath();
        var workspacePath = Path.Combine(
            basePath,
            "SelfCodeSupport",
            "workspaces",
            $"analysis-{ticketId.ToLowerInvariant().Replace("-", "_")}-{Guid.NewGuid():N}");

        _logger.LogInformation("Creating temporary workspace: {Path}", workspacePath);

        try
        {
            // Criar diretório se não existir
            if (!Directory.Exists(workspacePath))
            {
                Directory.CreateDirectory(workspacePath);
            }

            // Clonar repositório se ainda não foi clonado
            if (string.IsNullOrEmpty(_gitSettings.RemoteUrl))
            {
                throw new InvalidOperationException(
                    "RemoteUrl deve ser configurado quando UseTemporaryWorkspace é true ou RepositoryPath não está configurado");
            }

            // Verificar se já existe repositório clonado
            var gitPath = Path.Combine(workspacePath, ".git");
            if (!Directory.Exists(gitPath))
            {
                _logger.LogInformation("Cloning repository {Url} to {Path}", 
                    _gitSettings.RemoteUrl, workspacePath);
                
                // Salvar path original
                var originalPath = _gitSettings.RepositoryPath;
                
                try
                {
                    await _gitService.CloneRepositoryAsync(
                        _gitSettings.RemoteUrl, 
                        workspacePath, 
                        cancellationToken);
                }
                finally
                {
                    // Não restaurar aqui - será feito pelo WorkflowOrchestrator
                }
            }
            else
            {
                _logger.LogInformation("Repository already exists at {Path}, will be used", workspacePath);
            }

            _temporaryWorkspacePath = workspacePath;
            _isTemporaryWorkspace = true;

            return workspacePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating temporary workspace");
            
            // Limpar em caso de erro
            if (Directory.Exists(workspacePath))
            {
                try
                {
                    Directory.Delete(workspacePath, true);
                }
                catch { }
            }
            
            throw;
        }
    }

    /// <summary>
    /// Limpa workspace temporário após análise
    /// </summary>
    public void CleanupTemporaryWorkspace()
    {
        if (_isTemporaryWorkspace && !string.IsNullOrEmpty(_temporaryWorkspacePath))
        {
            try
            {
                if (Directory.Exists(_temporaryWorkspacePath))
                {
                    _logger.LogInformation("Cleaning up temporary workspace: {Path}", _temporaryWorkspacePath);
                    
                    // Tentar remover com retry (pode falhar se arquivos estiverem em uso)
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            Directory.Delete(_temporaryWorkspacePath, true);
                            _logger.LogInformation("Temporary workspace removed successfully");
                            break;
                        }
                        catch (Exception ex) when (i < 2)
                        {
                            _logger.LogWarning(ex, "Attempt {Attempt} to remove workspace failed, retrying...", i + 1);
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove temporary workspace {Path}", _temporaryWorkspacePath);
            }
            finally
            {
                _temporaryWorkspacePath = null;
                _isTemporaryWorkspace = false;
            }
        }
    }

    public void Dispose()
    {
        CleanupTemporaryWorkspace();
    }
}
