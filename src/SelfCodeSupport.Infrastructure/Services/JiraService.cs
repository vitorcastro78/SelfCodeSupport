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
/// Serviço de integração com JIRA REST API
/// </summary>
public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraSettings _settings;
    private readonly ILogger<JiraService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JiraService(
        HttpClient httpClient,
        IOptions<JiraSettings> settings,
        ILogger<JiraService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JiraDateTimeConverter() }
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? "3" : _settings.ApiVersion;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + $"/rest/api/{apiVersion}/");
        
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_settings.Email}:{_settings.ApiToken}"));
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<JiraTicket> GetTicketAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching ticket {TicketId} from JIRA", ticketId);

        try
        {
            // Solicitar todos os campos necessários incluindo attachments e comments
            var fields = "summary,description,issuetype,priority,status,assignee,reporter,created,updated,labels,components,attachment,comment";
            var response = await _httpClient.GetAsync(
                $"issue/{ticketId}?expand=changelog,renderedFields&fields={fields}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Gone || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Tentar com API v2 como fallback
                _logger.LogWarning("API version {Version} returned {StatusCode} for ticket {TicketId}, trying API v2 as fallback", 
                    _settings.ApiVersion, response.StatusCode, ticketId);
                
                return await GetTicketWithFallbackAsync(ticketId, cancellationToken);
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jiraIssue = JsonSerializer.Deserialize<JiraIssueResponse>(content, _jsonOptions);

            if (jiraIssue == null)
                throw new InvalidOperationException($"Could not deserialize ticket {ticketId}");

            return MapToJiraTicket(jiraIssue);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("410") || ex.Message.Contains("Gone"))
        {
            _logger.LogWarning(ex, "API version {Version} not available for ticket {TicketId}, trying API v2 as fallback", 
                _settings.ApiVersion, ticketId);
            return await GetTicketWithFallbackAsync(ticketId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error fetching ticket {TicketId} from JIRA", ticketId);
            throw new InvalidOperationException($"Error fetching ticket {ticketId}: {ex.Message}", ex);
        }
    }

    private async Task<JiraTicket> GetTicketWithFallbackAsync(string ticketId, CancellationToken cancellationToken)
    {
        // Criar um HttpClient temporário com API v2
        using var fallbackClient = new HttpClient();
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_settings.Email}:{_settings.ApiToken}"));
        
        fallbackClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/rest/api/2/");
        fallbackClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);
        fallbackClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var fields = "summary,description,issuetype,priority,status,assignee,reporter,created,updated,labels,components,attachment,comment";
        var response = await fallbackClient.GetAsync(
            $"issue/{ticketId}?expand=changelog,renderedFields&fields={fields}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var jiraIssue = JsonSerializer.Deserialize<JiraIssueResponse>(content, _jsonOptions);

        if (jiraIssue == null)
            throw new InvalidOperationException($"Could not deserialize ticket {ticketId}");

        return MapToJiraTicket(jiraIssue);
    }

    public async Task<IEnumerable<JiraTicket>> SearchTicketsAsync(string jql, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching tickets with JQL: {JQL}", jql);

        var searchRequest = new
        {
            jql,
            maxResults,
            fields = new[] { "summary", "description", "issuetype", "priority", "status", "assignee", "reporter", "created", "updated", "labels", "components", "attachment", "comment" }
        };

        var json = JsonSerializer.Serialize(searchRequest, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("search", httpContent, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Gone || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Tentar com API v2 como fallback
                _logger.LogWarning("API version {Version} returned {StatusCode}, trying API v2 as fallback", 
                    _settings.ApiVersion, response.StatusCode);
                
                return await SearchTicketsWithFallbackAsync(jql, maxResults, cancellationToken);
            }
            
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResult = JsonSerializer.Deserialize<JiraSearchResponse>(content, _jsonOptions);

            return searchResult?.Issues?.Select(MapToJiraTicket) ?? Enumerable.Empty<JiraTicket>();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("410") || ex.Message.Contains("Gone"))
        {
            _logger.LogWarning(ex, "API version {Version} not available, trying API v2 as fallback", _settings.ApiVersion);
            return await SearchTicketsWithFallbackAsync(jql, maxResults, cancellationToken);
        }
    }

    private async Task<IEnumerable<JiraTicket>> SearchTicketsWithFallbackAsync(string jql, int maxResults, CancellationToken cancellationToken)
    {
        // Criar um HttpClient temporário com API v2
        using var fallbackClient = new HttpClient();
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_settings.Email}:{_settings.ApiToken}"));
        
        fallbackClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/rest/api/2/");
        fallbackClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);
        fallbackClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var searchRequest = new
        {
            jql,
            maxResults,
            fields = new[] { "summary", "description", "issuetype", "priority", "status", "assignee", "reporter", "created", "updated", "labels", "components", "attachment", "comment" }
        };

        var json = JsonSerializer.Serialize(searchRequest, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await fallbackClient.PostAsync("search", httpContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var searchResult = JsonSerializer.Deserialize<JiraSearchResponse>(content, _jsonOptions);

        return searchResult?.Issues?.Select(MapToJiraTicket) ?? Enumerable.Empty<JiraTicket>();
    }

    public async Task AddCommentAsync(string ticketId, string comment, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adicionando comentário ao ticket {TicketId}", ticketId);

        var commentBody = new
        {
            body = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[]
                        {
                            new { type = "text", text = comment }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(commentBody, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"issue/{ticketId}/comment", httpContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Comentário adicionado com sucesso ao ticket {TicketId}", ticketId);
    }

    public async Task UpdateStatusAsync(string ticketId, string status, CancellationToken cancellationToken = default)
    {
        var transitions = await GetAvailableTransitionsAsync(ticketId, cancellationToken);
        var transition = transitions.FirstOrDefault(t => 
            t.Name.Equals(status, StringComparison.OrdinalIgnoreCase) ||
            t.ToStatus.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (transition == null)
        {
            throw new InvalidOperationException(
                $"Transição para status '{status}' não disponível. " +
                $"Transições disponíveis: {string.Join(", ", transitions.Select(t => t.Name))}");
        }

        await TransitionTicketAsync(ticketId, transition.Id, cancellationToken: cancellationToken);
    }

    public async Task UpdateFieldsAsync(string ticketId, Dictionary<string, object> fields, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Atualizando campos do ticket {TicketId}", ticketId);

        var updateBody = new { fields };
        var json = JsonSerializer.Serialize(updateBody, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync($"issue/{ticketId}", httpContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Campos atualizados com sucesso no ticket {TicketId}", ticketId);
    }

    public async Task AddRemoteLinkAsync(string ticketId, string url, string title, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adicionando link remoto ao ticket {TicketId}: {Title}", ticketId, title);

        var linkBody = new
        {
            @object = new
            {
                url,
                title
            }
        };

        var json = JsonSerializer.Serialize(linkBody, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"issue/{ticketId}/remotelink", httpContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Link remoto adicionado com sucesso ao ticket {TicketId}", ticketId);
    }

    public async Task<IEnumerable<JiraTransition>> GetAvailableTransitionsAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"issue/{ticketId}/transitions", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var transitionsResponse = JsonSerializer.Deserialize<JiraTransitionsResponse>(content, _jsonOptions);

        return transitionsResponse?.Transitions?.Select(t => new JiraTransition
        {
            Id = t.Id ?? string.Empty,
            Name = t.Name ?? string.Empty,
            ToStatus = t.To?.Name ?? string.Empty
        }) ?? Enumerable.Empty<JiraTransition>();
    }

    public async Task TransitionTicketAsync(string ticketId, string transitionId, string? comment = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executando transição {TransitionId} no ticket {TicketId}", transitionId, ticketId);

        var transitionBody = new Dictionary<string, object>
        {
            ["transition"] = new { id = transitionId }
        };

        if (!string.IsNullOrEmpty(comment))
        {
            transitionBody["update"] = new
            {
                comment = new[]
                {
                    new
                    {
                        add = new
                        {
                            body = new
                            {
                                type = "doc",
                                version = 1,
                                content = new[]
                                {
                                    new
                                    {
                                        type = "paragraph",
                                        content = new[]
                                        {
                                            new { type = "text", text = comment }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        var json = JsonSerializer.Serialize(transitionBody, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"issue/{ticketId}/transitions", httpContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Transição executada com sucesso no ticket {TicketId}", ticketId);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("myself", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão com JIRA");
            return false;
        }
    }

    #region Private Mapping Methods

    private JiraTicket MapToJiraTicket(JiraIssueResponse issue)
    {
        var fields = issue.Fields;
        
        return new JiraTicket
        {
            Id = issue.Key ?? string.Empty,
            ProjectKey = issue.Key?.Split('-').FirstOrDefault() ?? string.Empty,
            Title = fields?.Summary ?? string.Empty,
            Description = ExtractTextFromAdf(fields?.Description),
            Type = MapTicketType(fields?.IssueType?.Name),
            Priority = MapPriority(fields?.Priority?.Name),
            Status = fields?.Status?.Name ?? string.Empty,
            Assignee = fields?.Assignee?.DisplayName ?? string.Empty,
            Reporter = fields?.Reporter?.DisplayName ?? string.Empty,
            CreatedAt = fields?.Created ?? DateTime.MinValue,
            UpdatedAt = fields?.Updated ?? DateTime.MinValue,
            Labels = fields?.Labels ?? [],
            Components = fields?.Components?.Select(c => c.Name ?? string.Empty).ToList() ?? [],
            Url = $"{_settings.BaseUrl}/browse/{issue.Key}",
            AcceptanceCriteria = ExtractAcceptanceCriteria(fields?.Description),
            Comments = issue.Fields?.Comment?.Comments?.Select(c => new JiraComment
            {
                Id = c.Id ?? string.Empty,
                Author = c.Author?.DisplayName ?? string.Empty,
                Body = ExtractTextFromAdf(c.Body),
                CreatedAt = c.Created ?? DateTime.MinValue,
                UpdatedAt = c.Updated
            }).ToList() ?? [],
            Attachments = issue.Fields?.Attachments?.Select(a => new JiraAttachment
            {
                Id = a.Id ?? string.Empty,
                FileName = a.FileName ?? string.Empty,
                ContentType = a.MimeType ?? string.Empty,
                Size = a.Size ?? 0,
                Url = a.Content ?? string.Empty,
                CreatedAt = a.Created ?? DateTime.MinValue
            }).ToList() ?? []
        };
    }

    private static string ExtractTextFromAdf(object? adfContent)
    {
        if (adfContent == null) return string.Empty;

        try
        {
            if (adfContent is JsonElement element)
            {
                return ExtractTextFromJsonElement(element);
            }
            return adfContent.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractTextFromJsonElement(JsonElement element)
    {
        var sb = new StringBuilder();
        ExtractTextRecursive(element, sb);
        return sb.ToString().Trim();
    }

    private static void ExtractTextRecursive(JsonElement element, StringBuilder sb)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var textProp))
            {
                sb.Append(textProp.GetString());
            }

            if (element.TryGetProperty("content", out var contentProp) && 
                contentProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contentProp.EnumerateArray())
                {
                    ExtractTextRecursive(item, sb);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ExtractTextRecursive(item, sb);
            }
        }
    }

    private static List<string> ExtractAcceptanceCriteria(object? description)
    {
        var text = ExtractTextFromAdf(description);
        var criteria = new List<string>();

        // Procura por padrões comuns de acceptance criteria
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var inAcSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (trimmed.Contains("acceptance criteria", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("critérios de aceitação", StringComparison.OrdinalIgnoreCase))
            {
                inAcSection = true;
                continue;
            }

            if (inAcSection && (trimmed.StartsWith("-") || trimmed.StartsWith("*") || trimmed.StartsWith("•")))
            {
                criteria.Add(trimmed.TrimStart('-', '*', '•', ' '));
            }
            else if (inAcSection && string.IsNullOrWhiteSpace(trimmed))
            {
                inAcSection = false;
            }
        }

        return criteria;
    }

    private static TicketType MapTicketType(string? typeName) => typeName?.ToLowerInvariant() switch
    {
        "bug" => TicketType.Bug,
        "story" => TicketType.Story,
        "task" => TicketType.Task,
        "epic" => TicketType.Epic,
        "sub-task" or "subtask" => TicketType.SubTask,
        "improvement" => TicketType.Improvement,
        "new feature" => TicketType.NewFeature,
        _ => TicketType.Task
    };

    private static TicketPriority MapPriority(string? priorityName) => priorityName?.ToLowerInvariant() switch
    {
        "lowest" => TicketPriority.Lowest,
        "low" => TicketPriority.Low,
        "medium" => TicketPriority.Medium,
        "high" => TicketPriority.High,
        "highest" => TicketPriority.Highest,
        "critical" or "blocker" => TicketPriority.Critical,
        _ => TicketPriority.Medium
    };

    #endregion
}

#region JIRA API Response Models

internal class JiraIssueResponse
{
    public string? Key { get; set; }
    public string? Id { get; set; }
    public JiraFieldsResponse? Fields { get; set; }
}

internal class JiraFieldsResponse
{
    public string? Summary { get; set; }
    public object? Description { get; set; }
    public JiraIssueTypeResponse? IssueType { get; set; }
    public JiraPriorityResponse? Priority { get; set; }
    public JiraStatusResponse? Status { get; set; }
    public JiraUserResponse? Assignee { get; set; }
    public JiraUserResponse? Reporter { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Updated { get; set; }
    public List<string>? Labels { get; set; }
    public List<JiraComponentResponse>? Components { get; set; }
    public JiraCommentContainerResponse? Comment { get; set; }
    public List<JiraAttachmentResponse>? Attachments { get; set; }
}

internal class JiraIssueTypeResponse
{
    public string? Name { get; set; }
}

internal class JiraPriorityResponse
{
    public string? Name { get; set; }
}

internal class JiraStatusResponse
{
    public string? Name { get; set; }
}

internal class JiraUserResponse
{
    public string? DisplayName { get; set; }
    public string? EmailAddress { get; set; }
}

internal class JiraComponentResponse
{
    public string? Name { get; set; }
}

internal class JiraCommentContainerResponse
{
    public List<JiraCommentResponse>? Comments { get; set; }
}

internal class JiraCommentResponse
{
    public string? Id { get; set; }
    public JiraUserResponse? Author { get; set; }
    public object? Body { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Updated { get; set; }
}

internal class JiraAttachmentResponse
{
    public string? Id { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? Size { get; set; }
    public string? Content { get; set; }
    public DateTime? Created { get; set; }
    public JiraUserResponse? Author { get; set; }
}

internal class JiraSearchResponse
{
    public List<JiraIssueResponse>? Issues { get; set; }
    public int Total { get; set; }
}

internal class JiraTransitionsResponse
{
    public List<JiraTransitionResponse>? Transitions { get; set; }
}

internal class JiraTransitionResponse
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public JiraStatusResponse? To { get; set; }
}

#endregion

#region Custom Converters

/// <summary>
/// Converter para datas do JIRA que podem vir como string ou DateTime
/// </summary>
internal class JiraDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var dateString = reader.GetString();
            if (string.IsNullOrEmpty(dateString))
                return null;

            if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.None, out var date))
                return date;

            // JIRA pode retornar no formato: "2024-01-15T10:30:00.000+0000"
            if (DateTime.TryParseExact(dateString, "yyyy-MM-ddTHH:mm:ss.fffzzz", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                return parsedDate;
        }
        else if (reader.TokenType == JsonTokenType.String && reader.TryGetDateTime(out var dateTime))
        {
            return dateTime;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"));
        else
            writer.WriteNullValue();
    }
}

#endregion
