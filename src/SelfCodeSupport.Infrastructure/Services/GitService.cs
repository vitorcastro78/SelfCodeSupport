using System.Text.RegularExpressions;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;
using CommitInfo = SelfCodeSupport.Core.Models.CommitInfo;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Serviço de integração com Git usando LibGit2Sharp
/// </summary>
public partial class GitService : IGitService, IDisposable
{
    private readonly GitSettings _settings;
    private readonly ILogger<GitService> _logger;
    private Repository? _repository;
    private bool _disposed;

    public GitService(
        IOptions<GitSettings> settings,
        ILogger<GitService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_settings.RepositoryPath) && Directory.Exists(_settings.RepositoryPath))
        {
            InitializeRepository();
        }
    }

    private void InitializeRepository()
    {
        try
        {
            _repository = new Repository(_settings.RepositoryPath);
            _logger.LogInformation("Repositório inicializado: {Path}", _settings.RepositoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar repositório em {Path}", _settings.RepositoryPath);
            throw;
        }
    }

    private void EnsureRepository()
    {
        if (_repository == null)
        {
            throw new InvalidOperationException(
                "Repositório não inicializado. Configure o caminho do repositório ou clone primeiro.");
        }
    }

    public async Task CloneRepositoryAsync(string url, string localPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clonando repositório de {Url} para {Path}", url, localPath);

        await Task.Run(() =>
        {
            var options = new CloneOptions();
            options.FetchOptions.CredentialsProvider = GetCredentialsHandler();

            Repository.Clone(url, localPath, options);
        }, cancellationToken);

        _settings.RepositoryPath = localPath;
        InitializeRepository();

        _logger.LogInformation("Repositório clonado com sucesso");
    }

    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        EnsureRepository();
        _logger.LogInformation("Atualizando repositório (pull)");

        await Task.Run(() =>
        {
            var signature = GetSignature();
            var options = new PullOptions
            {
                FetchOptions = new FetchOptions
                {
                    CredentialsProvider = GetCredentialsHandler()
                }
            };

            Commands.Pull(_repository!, signature, options);
        }, cancellationToken);

        _logger.LogInformation("Repositório atualizado com sucesso");
    }

    public async Task CreateBranchAsync(string branchName, string? baseBranch = null, CancellationToken cancellationToken = default)
    {
        EnsureRepository();
        _logger.LogInformation("Criando branch {BranchName} a partir de {BaseBranch}", 
            branchName, baseBranch ?? _settings.DefaultBranch);

        await Task.Run(() =>
        {
            // Primeiro, fazer checkout da branch base
            var baseRef = baseBranch ?? _settings.DefaultBranch;
            var baseBranchObj = _repository!.Branches[baseRef] ?? 
                               _repository.Branches[$"{_settings.RemoteName}/{baseRef}"];

            if (baseBranchObj == null)
            {
                throw new InvalidOperationException($"Branch base '{baseRef}' não encontrada");
            }

            // Criar nova branch
            var newBranch = _repository.CreateBranch(branchName, baseBranchObj.Tip);
            
            // Fazer checkout da nova branch
            Commands.Checkout(_repository, newBranch);
        }, cancellationToken);

        _logger.LogInformation("Branch {BranchName} criada e ativada", branchName);
    }

    public async Task CheckoutAsync(string branchName, CancellationToken cancellationToken = default)
    {
        EnsureRepository();
        _logger.LogInformation("Mudando para branch {BranchName}", branchName);

        await Task.Run(() =>
        {
            var branch = _repository!.Branches[branchName] ?? 
                        _repository.Branches[$"{_settings.RemoteName}/{branchName}"];

            if (branch == null)
            {
                throw new InvalidOperationException($"Branch '{branchName}' não encontrada");
            }

            Commands.Checkout(_repository, branch);
        }, cancellationToken);

        _logger.LogInformation("Checkout para {BranchName} realizado", branchName);
    }

    public async Task StageFilesAsync(IEnumerable<string>? paths = null, CancellationToken cancellationToken = default)
    {
        EnsureRepository();

        await Task.Run(() =>
        {
            if (paths == null || !paths.Any())
            {
                Commands.Stage(_repository!, "*");
                _logger.LogInformation("Todos os arquivos adicionados ao staging");
            }
            else
            {
                foreach (var path in paths)
                {
                    Commands.Stage(_repository!, path);
                }
                _logger.LogInformation("Arquivos adicionados ao staging: {Count}", paths.Count());
            }
        }, cancellationToken);
    }

    public async Task<CommitInfo> CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        EnsureRepository();
        _logger.LogInformation("Realizando commit: {Message}", message.Split('\n').First());

        return await Task.Run(() =>
        {
            var signature = GetSignature();
            var commit = _repository!.Commit(message, signature, signature);

            return new CommitInfo
            {
                Hash = commit.Sha,
                Message = commit.Message,
                Timestamp = commit.Author.When.DateTime,
                Author = $"{commit.Author.Name} <{commit.Author.Email}>"
            };
        }, cancellationToken);
    }

    public async Task PushAsync(string branchName, CancellationToken cancellationToken = default)
    {
        EnsureRepository();
        _logger.LogInformation("Enviando alterações para {Remote}/{Branch}", _settings.RemoteName, branchName);

        await Task.Run(() =>
        {
            var remote = _repository!.Network.Remotes[_settings.RemoteName];
            var options = new PushOptions
            {
                CredentialsProvider = GetCredentialsHandler()
            };

            var localBranch = _repository.Branches[branchName];
            if (localBranch == null)
            {
                throw new InvalidOperationException($"Branch local '{branchName}' não encontrada");
            }

            _repository.Network.Push(localBranch, options);
        }, cancellationToken);

        _logger.LogInformation("Push realizado com sucesso");
    }

    public async Task<GitStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        EnsureRepository();

        return await Task.Run(() =>
        {
            var status = _repository!.RetrieveStatus();
            var branch = _repository!.Head;

            return new GitStatus
            {
                CurrentBranch = branch.FriendlyName,
                IsClean = !status.IsDirty,
                ModifiedFiles = status.Modified.Select(e => e.FilePath).ToList(),
                AddedFiles = status.Added.Select(e => e.FilePath).ToList(),
                DeletedFiles = status.Removed.Select(e => e.FilePath).ToList(),
                UntrackedFiles = status.Untracked.Select(e => e.FilePath).ToList(),
                StagedFiles = status.Staged.Select(e => e.FilePath).ToList(),
                AheadBy = branch.TrackingDetails?.AheadBy ?? 0,
                BehindBy = branch.TrackingDetails?.BehindBy ?? 0
            };
        }, cancellationToken);
    }

    public string GetCurrentBranch()
    {
        EnsureRepository();
        return _repository!.Head.FriendlyName;
    }

    public IEnumerable<string> ListBranches(bool includeRemote = false)
    {
        EnsureRepository();
        
        var branches = _repository!.Branches
            .Where(b => includeRemote || !b.IsRemote)
            .Select(b => b.FriendlyName);

        return branches;
    }

    public async Task<string> GetFileContentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        EnsureRepository();

        var fullPath = Path.Combine(_settings.RepositoryPath, filePath);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Arquivo não encontrado: {filePath}");
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public IEnumerable<string> ListFiles(string? path = null, string? pattern = null)
    {
        EnsureRepository();

        var basePath = string.IsNullOrEmpty(path) 
            ? _settings.RepositoryPath 
            : Path.Combine(_settings.RepositoryPath, path);

        if (!Directory.Exists(basePath))
        {
            return Enumerable.Empty<string>();
        }

        var searchPattern = pattern ?? "*.*";
        var files = Directory.GetFiles(basePath, searchPattern, SearchOption.AllDirectories);

        return files.Select(f => Path.GetRelativePath(_settings.RepositoryPath, f));
    }

    public async Task<IEnumerable<FileSearchResult>> SearchInFilesAsync(
        string searchTerm, 
        string? filePattern = null, 
        CancellationToken cancellationToken = default)
    {
        EnsureRepository();

        var results = new List<FileSearchResult>();
        var files = ListFiles(pattern: filePattern ?? "*.cs");

        await Task.Run(() =>
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(_settings.RepositoryPath, file);
                var lines = File.ReadAllLines(fullPath);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new FileSearchResult
                        {
                            FilePath = file,
                            LineNumber = i + 1,
                            LineContent = lines[i].Trim(),
                            Context = GetContext(lines, i, 2)
                        });
                    }
                }
            }
        }, cancellationToken);

        return results;
    }

    private static string GetContext(string[] lines, int lineIndex, int contextLines)
    {
        var start = Math.Max(0, lineIndex - contextLines);
        var end = Math.Min(lines.Length - 1, lineIndex + contextLines);
        
        return string.Join("\n", lines.Skip(start).Take(end - start + 1));
    }

    public string GenerateBranchName(JiraTicket ticket)
    {
        var prefix = ticket.Type switch
        {
            TicketType.Bug => _settings.BranchSettings.BugfixPrefix,
            TicketType.Story or TicketType.NewFeature => _settings.BranchSettings.FeaturePrefix,
            _ => _settings.BranchSettings.FeaturePrefix
        };

        var description = SanitizeBranchName(ticket.Title);
        
        // Limitar tamanho da descrição
        if (description.Length > 50)
        {
            description = description[..50];
        }

        return $"{prefix}{ticket.Id}-{description}";
    }

    [GeneratedRegex(@"[^a-z0-9-]")]
    private static partial Regex InvalidBranchCharsRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();

    private static string SanitizeBranchName(string name)
    {
        var sanitized = name.ToLowerInvariant();
        sanitized = InvalidBranchCharsRegex().Replace(sanitized, "-");
        sanitized = MultipleHyphensRegex().Replace(sanitized, "-");
        return sanitized.Trim('-');
    }

    public string GenerateCommitMessage(JiraTicket ticket, string description)
    {
        var type = ticket.Type switch
        {
            TicketType.Bug => "fix",
            TicketType.Story or TicketType.NewFeature => "feat",
            TicketType.Improvement => "improve",
            _ => "chore"
        };

        var template = _settings.CommitSettings.CommitMessageTemplate;
        
        return template
            .Replace("{type}", type)
            .Replace("{ticketId}", ticket.Id)
            .Replace("{description}", description)
            .Replace("{body}", $"Implementação do ticket {ticket.Id}: {ticket.Title}");
    }

    public bool IsClean()
    {
        EnsureRepository();
        return !_repository!.RetrieveStatus().IsDirty;
    }

    public async Task DiscardChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureRepository();

        await Task.Run(() =>
        {
            _repository!.Reset(ResetMode.Hard);
            // Remove untracked files manually
            var status = _repository.RetrieveStatus();
            foreach (var item in status.Untracked)
            {
                var fullPath = Path.Combine(_settings.RepositoryPath, item.FilePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }, cancellationToken);

        _logger.LogInformation("Todas as alterações locais foram descartadas");
    }

    private Signature GetSignature()
    {
        return new Signature(
            _settings.CommitSettings.AuthorName,
            _settings.CommitSettings.AuthorEmail,
            DateTimeOffset.Now);
    }

    private LibGit2Sharp.Handlers.CredentialsHandler GetCredentialsHandler()
    {
        return (url, usernameFromUrl, types) =>
        {
            // Para autenticação HTTPS com token
            return new UsernamePasswordCredentials
            {
                Username = _settings.Credentials.Username,
                Password = _settings.Credentials.PersonalAccessToken
            };
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _repository?.Dispose();
            }
            _disposed = true;
        }
    }
}
