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
/// Serviço de integração com Anthropic Claude API
/// </summary>
public class AnthropicService : IAnthropicService
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicSettings _settings;
    private readonly ILogger<AnthropicService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnthropicService(
        HttpClient httpClient,
        IOptions<AnthropicSettings> settings,
        ILogger<AnthropicService> logger)
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
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", _settings.ApiVersion);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<AnalysisResult> AnalyzeTicketAsync(
        JiraTicket ticket, 
        string codeContext, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing ticket {TicketId} with Claude", ticket.Id);

        var prompt = _settings.Prompts.AnalysisPromptTemplate
            .Replace("{ticketId}", ticket.Id)
            .Replace("{title}", ticket.Title)
            .Replace("{description}", ticket.Description)
            .Replace("{type}", ticket.Type.ToString())
            .Replace("{codeContext}", codeContext);

        var systemPrompt = _settings.Prompts.SystemPrompt + @"

Respond ONLY with a valid JSON in the following format:
{
    ""affectedFiles"": [{""path"": ""string"", ""changeType"": ""Create|Modify|Delete"", ""description"": ""string"", ""methodsAffected"": [""string""]}],
    ""requiredChanges"": [{""component"": ""string"", ""description"": ""string"", ""category"": ""Controller|Service|Repository|Model|DTO|Validator|Migration|Configuration|Test|Documentation""}],
    ""technicalImpact"": {""hasBreakingChanges"": false, ""requiresMigration"": false, ""affectsPerformance"": false, ""hasSecurityImplications"": false, ""newDependencies"": [], ""affectedEndpoints"": [], ""affectedServices"": []},
    ""risks"": [{""description"": ""string"", ""severity"": ""Low|Medium|High|Critical"", ""mitigation"": ""string""}],
    ""opportunities"": [{""description"": ""string"", ""type"": ""Refactoring|Performance|Security|CodeQuality|Pattern|Documentation"", ""estimatedEffortHours"": 0}],
    ""implementationPlan"": [{""order"": 1, ""description"": ""string"", ""files"": [""string""], ""estimatedMinutes"": 0}],
    ""validationCriteria"": [{""description"": ""string"", ""type"": ""UnitTest|IntegrationTest|ManualTest|CodeReview|PerformanceTest|SecurityScan"", ""isAutomatable"": true}],
    ""estimatedEffortHours"": 0,
    ""complexity"": ""Trivial|Simple|Medium|Complex|VeryComplex""
}";

        var response = await SendMessageAsync(prompt, systemPrompt, cancellationToken);
        
        try
        {
            // Extrair JSON da resposta
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response[jsonStart..jsonEnd];
                var analysisData = JsonSerializer.Deserialize<AnalysisResultDto>(jsonString, _jsonOptions);
                
                if (analysisData != null)
                {
                    return MapToAnalysisResult(ticket.Id, analysisData);
                }
            }

            throw new InvalidOperationException("Could not extract valid JSON from response");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing analysis response");
            throw new InvalidOperationException("Error processing analysis response", ex);
        }
    }

    public async Task<GeneratedCode> GenerateCodeAsync(
        string context, 
        string requirements, 
        string existingCode, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating code with Claude");

        var prompt = _settings.Prompts.CodeGenerationPromptTemplate
            .Replace("{context}", context)
            .Replace("{requirements}", requirements)
            .Replace("{existingCode}", existingCode);

        var systemPrompt = _settings.Prompts.SystemPrompt + @"

Respond with a JSON in the format:
{
    ""files"": [{""path"": ""string"", ""content"": ""string"", ""operation"": ""Create|Update|Delete"", ""description"": ""string""}],
    ""explanation"": ""string"",
    ""dependencies"": [""string""],
    ""additionalInstructions"": ""string""
}

File contents must be valid and complete C# code.";

        var response = await SendMessageAsync(prompt, systemPrompt, cancellationToken);

        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response[jsonStart..jsonEnd];
                var codeData = JsonSerializer.Deserialize<GeneratedCodeDto>(jsonString, _jsonOptions);

                if (codeData != null)
                {
                    return new GeneratedCode
                    {
                        Files = codeData.Files?.Select(f => new GeneratedFile
                        {
                            Path = f.Path ?? string.Empty,
                            Content = f.Content ?? string.Empty,
                            Operation = Enum.TryParse<FileOperation>(f.Operation, true, out var op) ? op : FileOperation.Create,
                            Description = f.Description ?? string.Empty
                        }).ToList() ?? [],
                        Explanation = codeData.Explanation ?? string.Empty,
                        Dependencies = codeData.Dependencies ?? [],
                        AdditionalInstructions = codeData.AdditionalInstructions
                    };
                }
            }

            throw new InvalidOperationException("Could not extract code from response");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing generated code");
            throw new InvalidOperationException("Error processing generated code", ex);
        }
    }

    public async Task<string> GenerateTestsAsync(string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating unit tests with Claude");

        var prompt = _settings.Prompts.TestGenerationPromptTemplate
            .Replace("{code}", code);

        var systemPrompt = _settings.Prompts.SystemPrompt + @"

Generate C# test code using xUnit, Moq and FluentAssertions.
Return ONLY the test code, without additional explanations.
The code must be valid and compilable.";

        var response = await SendMessageAsync(prompt, systemPrompt, cancellationToken);

        // Extrair código C# da resposta
        var codeStart = response.IndexOf("```csharp", StringComparison.OrdinalIgnoreCase);
        if (codeStart >= 0)
        {
            codeStart = response.IndexOf('\n', codeStart) + 1;
            var codeEnd = response.IndexOf("```", codeStart);
            if (codeEnd > codeStart)
            {
                return response[codeStart..codeEnd].Trim();
            }
        }

        // Se não encontrou bloco de código, retorna a resposta limpa
        return response.Trim();
    }

    public async Task<CodeReviewResult> ReviewCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reviewing code with Claude");

        var prompt = _settings.Prompts.CodeReviewPromptTemplate
            .Replace("{code}", code);

        var systemPrompt = _settings.Prompts.SystemPrompt + @"

Respond with a JSON in the format:
{
    ""overallScore"": 0,
    ""issues"": [{""description"": ""string"", ""severity"": ""Info|Low|Medium|High|Critical"", ""category"": ""Bug|Security|Performance|CodeStyle|BestPractice|Documentation|Maintainability"", ""filePath"": ""string"", ""lineNumber"": 0, ""suggestedFix"": ""string""}],
    ""suggestions"": [""string""],
    ""summary"": ""string""
}

The score must be from 0 to 100.";

        var response = await SendMessageAsync(prompt, systemPrompt, cancellationToken);

        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response[jsonStart..jsonEnd];
                var reviewData = JsonSerializer.Deserialize<CodeReviewResultDto>(jsonString, _jsonOptions);

                if (reviewData != null)
                {
                    return new CodeReviewResult
                    {
                        OverallScore = reviewData.OverallScore,
                        Issues = reviewData.Issues?.Select(i => new CodeIssue
                        {
                            Description = i.Description ?? string.Empty,
                            Severity = Enum.TryParse<IssueSeverity>(i.Severity, true, out var sev) ? sev : IssueSeverity.Low,
                            Category = Enum.TryParse<IssueCategory>(i.Category, true, out var cat) ? cat : IssueCategory.CodeStyle,
                            FilePath = i.FilePath,
                            LineNumber = i.LineNumber,
                            SuggestedFix = i.SuggestedFix
                        }).ToList() ?? [],
                        Suggestions = reviewData.Suggestions ?? [],
                        Summary = reviewData.Summary ?? string.Empty
                    };
                }
            }

            throw new InvalidOperationException("Could not extract review from response");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing code review");
            throw new InvalidOperationException("Error processing review", ex);
        }
    }

    public async Task<string> SendMessageAsync(
        string message, 
        string? systemPrompt = null, 
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ConversationMessage>
        {
            new() { Role = MessageRole.User, Content = message }
        };

        return await SendConversationAsync(messages, systemPrompt, cancellationToken);
    }

    public async Task<string> SendConversationAsync(
        IEnumerable<ConversationMessage> messages, 
        string? systemPrompt = null, 
        CancellationToken cancellationToken = default)
    {
        var request = new AnthropicRequest
        {
            Model = _settings.Model,
            MaxTokens = _settings.MaxTokens,
            Temperature = _settings.Temperature,
            System = systemPrompt ?? _settings.Prompts.SystemPrompt,
            Messages = messages.Select(m => new AnthropicMessage
            {
                Role = m.Role == MessageRole.User ? "user" : "assistant",
                Content = m.Content
            }).ToList()
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var retryCount = 0;
        while (retryCount < _settings.MaxRetries)
        {
            try
            {
                var response = await _httpClient.PostAsync("v1/messages", httpContent, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Error in Anthropic API: {StatusCode} - {Content}", 
                        response.StatusCode, errorContent);
                    
                    if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        retryCount++;
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
                        continue;
                    }
                    
                    throw new HttpRequestException($"API error: {response.StatusCode} - {errorContent}");
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(content, _jsonOptions);

                return anthropicResponse?.Content?.FirstOrDefault()?.Text ?? string.Empty;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                retryCount++;
                _logger.LogWarning("Timeout in Anthropic API, attempt {Retry}/{Max}", 
                    retryCount, _settings.MaxRetries);
                
                if (retryCount >= _settings.MaxRetries)
                    throw;
                
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
            }
        }

        throw new InvalidOperationException("Maximum number of retries exceeded");
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Usa um timeout menor para teste de conexão (10 segundos)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            // Requisição simples apenas para verificar se a API responde
            var testRequest = new AnthropicRequest
            {
                Model = _settings.Model,
                MaxTokens = 10, // Mínimo para teste
                Temperature = 0.1,
                System = "You are a helpful assistant.",
                Messages = new List<AnthropicMessage>
                {
                    new() { Role = "user", Content = "OK" }
                }
            };

            var json = JsonSerializer.Serialize(testRequest, _jsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("v1/messages", httpContent, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout testing connection with Anthropic (test cancelled after 10s)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection with Anthropic");
            return false;
        }
    }

    #region Private Mapping Methods

    private static AnalysisResult MapToAnalysisResult(string ticketId, AnalysisResultDto dto)
    {
        return new AnalysisResult
        {
            TicketId = ticketId,
            AnalyzedAt = DateTime.UtcNow,
            AffectedFiles = dto.AffectedFiles?.Select(f => new AffectedFile
            {
                Path = f.Path ?? string.Empty,
                ChangeType = Enum.TryParse<FileChangeType>(f.ChangeType, true, out var ct) ? ct : FileChangeType.Modify,
                Description = f.Description ?? string.Empty,
                MethodsAffected = f.MethodsAffected ?? []
            }).ToList() ?? [],
            RequiredChanges = dto.RequiredChanges?.Select(c => new RequiredChange
            {
                Component = c.Component ?? string.Empty,
                Description = c.Description ?? string.Empty,
                Category = Enum.TryParse<ChangeCategory>(c.Category, true, out var cat) ? cat : ChangeCategory.Service
            }).ToList() ?? [],
            TechnicalImpact = new TechnicalImpact
            {
                HasBreakingChanges = dto.TechnicalImpact?.HasBreakingChanges ?? false,
                RequiresMigration = dto.TechnicalImpact?.RequiresMigration ?? false,
                AffectsPerformance = dto.TechnicalImpact?.AffectsPerformance ?? false,
                HasSecurityImplications = dto.TechnicalImpact?.HasSecurityImplications ?? false,
                NewDependencies = dto.TechnicalImpact?.NewDependencies ?? [],
                AffectedEndpoints = dto.TechnicalImpact?.AffectedEndpoints ?? [],
                AffectedServices = dto.TechnicalImpact?.AffectedServices ?? []
            },
            Risks = dto.Risks?.Select(r => new Risk
            {
                Description = r.Description ?? string.Empty,
                Severity = Enum.TryParse<RiskSeverity>(r.Severity, true, out var sev) ? sev : RiskSeverity.Low,
                Mitigation = r.Mitigation ?? string.Empty
            }).ToList() ?? [],
            Opportunities = dto.Opportunities?.Select(o => new Opportunity
            {
                Description = o.Description ?? string.Empty,
                Type = Enum.TryParse<OpportunityType>(o.Type, true, out var ot) ? ot : OpportunityType.CodeQuality,
                EstimatedEffortHours = o.EstimatedEffortHours
            }).ToList() ?? [],
            ImplementationPlan = dto.ImplementationPlan?.Select(p => new ImplementationStep
            {
                Order = p.Order,
                Description = p.Description ?? string.Empty,
                Files = p.Files ?? [],
                EstimatedMinutes = p.EstimatedMinutes
            }).ToList() ?? [],
            ValidationCriteria = dto.ValidationCriteria?.Select(v => new ValidationCriteria
            {
                Description = v.Description ?? string.Empty,
                Type = Enum.TryParse<ValidationType>(v.Type, true, out var vt) ? vt : ValidationType.ManualTest,
                IsAutomatable = v.IsAutomatable
            }).ToList() ?? [],
            EstimatedEffortHours = dto.EstimatedEffortHours,
            Complexity = Enum.TryParse<Complexity>(dto.Complexity, true, out var comp) ? comp : Complexity.Medium,
            Status = AnalysisStatus.Completed
        };
    }

    #endregion
}

#region DTOs for JSON Deserialization

internal class AnalysisResultDto
{
    public List<AffectedFileDto>? AffectedFiles { get; set; }
    public List<RequiredChangeDto>? RequiredChanges { get; set; }
    public TechnicalImpactDto? TechnicalImpact { get; set; }
    public List<RiskDto>? Risks { get; set; }
    public List<OpportunityDto>? Opportunities { get; set; }
    public List<ImplementationStepDto>? ImplementationPlan { get; set; }
    public List<ValidationCriteriaDto>? ValidationCriteria { get; set; }
    public int EstimatedEffortHours { get; set; }
    public string? Complexity { get; set; }
}

internal class AffectedFileDto
{
    public string? Path { get; set; }
    public string? ChangeType { get; set; }
    public string? Description { get; set; }
    public List<string>? MethodsAffected { get; set; }
}

internal class RequiredChangeDto
{
    public string? Component { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

internal class TechnicalImpactDto
{
    public bool HasBreakingChanges { get; set; }
    public bool RequiresMigration { get; set; }
    public bool AffectsPerformance { get; set; }
    public bool HasSecurityImplications { get; set; }
    public List<string>? NewDependencies { get; set; }
    public List<string>? AffectedEndpoints { get; set; }
    public List<string>? AffectedServices { get; set; }
}

internal class RiskDto
{
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public string? Mitigation { get; set; }
}

internal class OpportunityDto
{
    public string? Description { get; set; }
    public string? Type { get; set; }
    public int EstimatedEffortHours { get; set; }
}

internal class ImplementationStepDto
{
    public int Order { get; set; }
    public string? Description { get; set; }
    public List<string>? Files { get; set; }
    public int EstimatedMinutes { get; set; }
}

internal class ValidationCriteriaDto
{
    public string? Description { get; set; }
    public string? Type { get; set; }
    public bool IsAutomatable { get; set; }
}

internal class GeneratedCodeDto
{
    public List<GeneratedFileDto>? Files { get; set; }
    public string? Explanation { get; set; }
    public List<string>? Dependencies { get; set; }
    public string? AdditionalInstructions { get; set; }
}

internal class GeneratedFileDto
{
    public string? Path { get; set; }
    public string? Content { get; set; }
    public string? Operation { get; set; }
    public string? Description { get; set; }
}

internal class CodeReviewResultDto
{
    public int OverallScore { get; set; }
    public List<CodeIssueDto>? Issues { get; set; }
    public List<string>? Suggestions { get; set; }
    public string? Summary { get; set; }
}

internal class CodeIssueDto
{
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public string? Category { get; set; }
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string? SuggestedFix { get; set; }
}

#endregion

#region Anthropic API Models

internal class AnthropicRequest
{
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public double Temperature { get; set; }
    public string? System { get; set; }
    public List<AnthropicMessage> Messages { get; set; } = [];
}

internal class AnthropicMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

internal class AnthropicResponse
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Role { get; set; }
    public List<AnthropicContent>? Content { get; set; }
    public string? Model { get; set; }
    public string? StopReason { get; set; }
    public AnthropicUsage? Usage { get; set; }
}

internal class AnthropicContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

internal class AnthropicUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

#endregion
