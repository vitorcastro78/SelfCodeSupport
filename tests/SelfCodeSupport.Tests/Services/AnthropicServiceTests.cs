using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Models;
using SelfCodeSupport.Infrastructure.Services;

namespace SelfCodeSupport.Tests.Services;

public class AnthropicServiceTests
{
    private readonly Mock<ILogger<AnthropicService>> _loggerMock;
    private readonly AnthropicSettings _settings;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;

    public AnthropicServiceTests()
    {
        _loggerMock = new Mock<ILogger<AnthropicService>>();
        _settings = new AnthropicSettings
        {
            ApiKey = "test-api-key",
            Model = "claude-sonnet-4-20250514",
            MaxTokens = 4000,
            Temperature = 0.3,
            BaseUrl = "https://api.anthropic.com",
            ApiVersion = "2023-06-01",
            TimeoutSeconds = 30,
            MaxRetries = 1,
            Prompts = new AnthropicPrompts()
        };
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
    }

    private AnthropicService CreateService()
    {
        return new AnthropicService(
            _httpClient,
            Options.Create(_settings),
            _loggerMock.Object);
    }

    [Fact]
    public async Task SendMessageAsync_ValidMessage_ReturnsResponse()
    {
        // Arrange
        var message = "Hello, Claude!";
        var expectedResponse = "Hello! How can I help you today?";
        
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[]
            {
                new { type = "text", text = expectedResponse }
            },
            model = "claude-sonnet-4-20250514",
            stop_reason = "end_turn",
            usage = new { input_tokens = 10, output_tokens = 20 }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);
        var service = CreateService();

        // Act
        var result = await service.SendMessageAsync(message);

        // Assert
        result.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task SendMessageAsync_ApiError_ThrowsException()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.BadRequest, "Invalid request");
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => 
            service.SendMessageAsync("test"));
    }

    [Fact]
    public async Task TestConnectionAsync_ValidApiKey_ReturnsTrue()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[]
            {
                new { type = "text", text = "OK" }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);
        var service = CreateService();

        // Act
        var result = await service.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidApiKey_ReturnsFalse()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.Unauthorized, "Invalid API key");
        var service = CreateService();

        // Act
        var result = await service.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeTicketAsync_ValidTicket_ReturnsAnalysis()
    {
        // Arrange
        var ticket = new JiraTicket
        {
            Id = "TEST-123",
            Title = "Add user search endpoint",
            Description = "Create a new endpoint to search users by name",
            Type = TicketType.Story
        };

        // Use snake_case for JSON as Anthropic service uses SnakeCaseLower policy
        var analysisJson = @"{
            ""affected_files"": [
                { ""path"": ""Controllers/UserController.cs"", ""change_type"": ""Modify"", ""description"": ""Add search endpoint"", ""methods_affected"": [] }
            ],
            ""required_changes"": [
                { ""component"": ""UserController"", ""description"": ""Add SearchUsers method"", ""category"": ""Controller"" }
            ],
            ""technical_impact"": {
                ""has_breaking_changes"": false,
                ""requires_migration"": false,
                ""affects_performance"": false,
                ""has_security_implications"": false,
                ""new_dependencies"": [],
                ""affected_endpoints"": [""GET /api/users/search""],
                ""affected_services"": [""UserService""]
            },
            ""risks"": [],
            ""opportunities"": [],
            ""implementation_plan"": [
                { ""order"": 1, ""description"": ""Create search method"", ""files"": [""UserController.cs""], ""estimated_minutes"": 30 }
            ],
            ""validation_criteria"": [
                { ""description"": ""Search returns correct results"", ""type"": ""UnitTest"", ""is_automatable"": true }
            ],
            ""estimated_effort_hours"": 2,
            ""complexity"": ""Simple""
        }";

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[]
            {
                new { type = "text", text = analysisJson }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);
        var service = CreateService();

        // Act
        var result = await service.AnalyzeTicketAsync(ticket, "// existing code");

        // Assert
        result.Should().NotBeNull();
        result.TicketId.Should().Be("TEST-123");
        result.AffectedFiles.Should().HaveCount(1);
        result.RequiredChanges.Should().HaveCount(1);
        result.Complexity.Should().Be(Complexity.Simple);
    }

    [Fact]
    public async Task GenerateTestsAsync_ValidCode_ReturnsTests()
    {
        // Arrange
        var code = @"
public class Calculator
{
    public int Add(int a, int b) => a + b;
}";

        var expectedTests = @"
public class CalculatorTests
{
    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        var calc = new Calculator();
        var result = calc.Add(2, 3);
        result.Should().Be(5);
    }
}";

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[]
            {
                new { type = "text", text = $"```csharp\n{expectedTests}\n```" }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);
        var service = CreateService();

        // Act
        var result = await service.GenerateTestsAsync(code);

        // Assert
        result.Should().Contain("CalculatorTests");
        result.Should().Contain("[Fact]");
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }
}
