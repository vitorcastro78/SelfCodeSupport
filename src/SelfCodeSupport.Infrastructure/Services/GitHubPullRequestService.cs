using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Infrastructure.Services;

/// <summary>
/// Serviço de Pull Request para GitHub
/// </summary>
public class GitHubPullRequestService : IPullRequestService
{
    private readonly HttpClient _httpClient;
    private readonly GitSettings _settings;
    private readonly ILogger<GitHubPullRequestService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GitHubPullRequestService(
        HttpClient httpClient,
        IOptions<GitSettings> settings,
        ILogger<GitHubPullRequestService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = string.IsNullOrEmpty(_settings.PullRequestSettings.ApiUrl)
            ? "https://api.github.com/"
            : _settings.PullRequestSettings.ApiUrl.TrimEnd('/') + "/";

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.PullRequestSettings.ApiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SelfCodeSupport", "1.0"));
    }

    private string GetRepoPath() =>
        $"repos/{_settings.PullRequestSettings.Owner}/{_settings.PullRequestSettings.Repository}";

    public async Task<PullRequestInfo> CreatePullRequestAsync(
        CreatePullRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Criando Pull Request: {Title}", request.Title);

        var prBody = new
        {
            title = request.Title,
            body = request.Body,
            head = request.SourceBranch,
            @base = request.TargetBranch,
            draft = request.IsDraft
        };

        var json = JsonSerializer.Serialize(prBody, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{GetRepoPath()}/pulls",
            httpContent,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Erro ao criar PR: {StatusCode} - {Content}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Erro ao criar PR: {response.StatusCode} - {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var ghPr = JsonSerializer.Deserialize<GitHubPullRequestResponse>(content, _jsonOptions);

        if (ghPr == null)
            throw new InvalidOperationException("Não foi possível deserializar resposta do PR");

        var prInfo = MapToPullRequestInfo(ghPr);

        // Adicionar reviewers se especificados
        if (request.Reviewers.Count > 0)
        {
            await AddReviewersAsync(prInfo.Number, request.Reviewers, cancellationToken);
        }

        // Adicionar labels se especificadas
        if (request.Labels.Count > 0)
        {
            await AddLabelsAsync(prInfo.Number, request.Labels, cancellationToken);
        }

        _logger.LogInformation("Pull Request #{Number} criado com sucesso: {Url}",
            prInfo.Number, prInfo.Url);

        return prInfo;
    }

    public async Task<PullRequestInfo> GetPullRequestAsync(
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Obtendo PR #{Number}", prNumber);

        var response = await _httpClient.GetAsync(
            $"{GetRepoPath()}/pulls/{prNumber}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var ghPr = JsonSerializer.Deserialize<GitHubPullRequestResponse>(content, _jsonOptions);

        if (ghPr == null)
            throw new InvalidOperationException($"Não foi possível obter PR #{prNumber}");

        return MapToPullRequestInfo(ghPr);
    }

    public async Task UpdatePullRequestAsync(
        int prNumber,
        UpdatePullRequestRequest update,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Atualizando PR #{Number}", prNumber);

        var updateBody = new Dictionary<string, object>();

        if (update.Title != null)
            updateBody["title"] = update.Title;

        if (update.Body != null)
            updateBody["body"] = update.Body;

        if (update.Status.HasValue)
        {
            updateBody["state"] = update.Status.Value switch
            {
                PullRequestStatus.Closed => "closed",
                PullRequestStatus.Open => "open",
                _ => "open"
            };
        }

        var json = JsonSerializer.Serialize(updateBody, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PatchAsync(
            $"{GetRepoPath()}/pulls/{prNumber}",
            httpContent,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("PR #{Number} atualizado com sucesso", prNumber);
    }

    public async Task AddReviewersAsync(
        int prNumber,
        IEnumerable<string> reviewers,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adicionando reviewers ao PR #{Number}", prNumber);

        var body = new { reviewers = reviewers.ToList() };
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{GetRepoPath()}/pulls/{prNumber}/requested_reviewers",
            httpContent,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Não foi possível adicionar reviewers: {Content}", errorContent);
        }
    }

    public async Task AddLabelsAsync(
        int prNumber,
        IEnumerable<string> labels,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adicionando labels ao PR #{Number}", prNumber);

        var body = new { labels = labels.ToList() };
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{GetRepoPath()}/issues/{prNumber}/labels",
            httpContent,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Não foi possível adicionar labels: {Content}", errorContent);
        }
    }

    public async Task AddCommentAsync(
        int prNumber,
        string comment,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adicionando comentário ao PR #{Number}", prNumber);

        var body = new { body = comment };
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{GetRepoPath()}/issues/{prNumber}/comments",
            httpContent,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Comentário adicionado ao PR #{Number}", prNumber);
    }

    public async Task ClosePullRequestAsync(
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        await UpdatePullRequestAsync(prNumber, new UpdatePullRequestRequest
        {
            Status = PullRequestStatus.Closed
        }, cancellationToken);
    }

    public async Task MergePullRequestAsync(
        int prNumber,
        MergeMethod mergeMethod = MergeMethod.Squash,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fazendo merge do PR #{Number} usando método {Method}",
            prNumber, mergeMethod);

        var body = new
        {
            merge_method = mergeMethod switch
            {
                MergeMethod.Merge => "merge",
                MergeMethod.Squash => "squash",
                MergeMethod.Rebase => "rebase",
                _ => "squash"
            }
        };

        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(
            $"{GetRepoPath()}/pulls/{prNumber}/merge",
            httpContent,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Erro ao fazer merge: {response.StatusCode} - {errorContent}");
        }

        _logger.LogInformation("PR #{Number} merged com sucesso", prNumber);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Usa um timeout menor para teste de conexão (10 segundos)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            var response = await _httpClient.GetAsync("user", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout ao testar conexão com GitHub (teste cancelado após 10s)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão com GitHub");
            return false;
        }
    }

    private PullRequestInfo MapToPullRequestInfo(GitHubPullRequestResponse ghPr)
    {
        return new PullRequestInfo
        {
            Number = ghPr.Number,
            Title = ghPr.Title ?? string.Empty,
            Description = ghPr.Body ?? string.Empty,
            SourceBranch = ghPr.Head?.Ref ?? string.Empty,
            TargetBranch = ghPr.Base?.Ref ?? string.Empty,
            Url = ghPr.HtmlUrl ?? string.Empty,
            Status = ghPr.State?.ToLowerInvariant() switch
            {
                "open" => ghPr.Draft == true ? PullRequestStatus.Draft : PullRequestStatus.Open,
                "closed" => ghPr.MergedAt.HasValue ? PullRequestStatus.Merged : PullRequestStatus.Closed,
                _ => PullRequestStatus.Open
            },
            Author = ghPr.User?.Login ?? string.Empty,
            CreatedAt = ghPr.CreatedAt ?? DateTime.MinValue,
            MergedAt = ghPr.MergedAt,
            Labels = ghPr.Labels?.Select(l => l.Name ?? string.Empty).ToList() ?? [],
            Reviewers = ghPr.RequestedReviewers?.Select(r => r.Login ?? string.Empty).ToList() ?? []
        };
    }
}

#region GitHub API Response Models

internal class GitHubPullRequestResponse
{
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? State { get; set; }
    public bool? Draft { get; set; }
    public string? HtmlUrl { get; set; }
    public GitHubUserResponse? User { get; set; }
    public GitHubBranchRefResponse? Head { get; set; }
    public GitHubBranchRefResponse? Base { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? MergedAt { get; set; }
    public List<GitHubLabelResponse>? Labels { get; set; }
    public List<GitHubUserResponse>? RequestedReviewers { get; set; }
}

internal class GitHubUserResponse
{
    public string? Login { get; set; }
}

internal class GitHubBranchRefResponse
{
    public string? Ref { get; set; }
    public string? Sha { get; set; }
}

internal class GitHubLabelResponse
{
    public string? Name { get; set; }
}

#endregion
