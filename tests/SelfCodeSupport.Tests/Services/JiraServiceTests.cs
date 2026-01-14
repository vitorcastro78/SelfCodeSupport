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

public class JiraServiceTests
{
    private readonly Mock<ILogger<JiraService>> _loggerMock;
    private readonly JiraSettings _settings;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;

    public JiraServiceTests()
    {
        _loggerMock = new Mock<ILogger<JiraService>>();
        _settings = new JiraSettings
        {
            BaseUrl = "https://test.atlassian.net",
            Email = "test@test.com",
            ApiToken = "test-token",
            DefaultProjectKey = "TEST"
        };
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
    }

    private JiraService CreateService()
    {
        return new JiraService(
            _httpClient,
            Options.Create(_settings),
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetTicketAsync_ValidTicketId_ReturnsTicket()
    {
        // Arrange
        var ticketId = "TEST-123";
        var responseJson = JsonSerializer.Serialize(new
        {
            key = ticketId,
            id = "12345",
            fields = new
            {
                summary = "Test Ticket",
                description = new
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
                                new { type = "text", text = "Test description" }
                            }
                        }
                    }
                },
                issuetype = new { name = "Bug" },
                priority = new { name = "High" },
                status = new { name = "To Do" },
                assignee = new { displayName = "John Doe" },
                reporter = new { displayName = "Jane Doe" },
                created = DateTime.UtcNow,
                updated = DateTime.UtcNow,
                labels = new[] { "backend", "api" },
                components = new[] { new { name = "API" } }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var service = CreateService();

        // Act
        var result = await service.GetTicketAsync(ticketId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ticketId);
        result.Title.Should().Be("Test Ticket");
        result.Type.Should().Be(TicketType.Bug);
        result.Priority.Should().Be(TicketPriority.High);
        result.Status.Should().Be("To Do");
    }

    [Fact]
    public async Task GetTicketAsync_InvalidTicketId_ThrowsException()
    {
        // Arrange
        var ticketId = "INVALID-999";
        SetupHttpResponse(HttpStatusCode.NotFound, "Not Found");

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            service.GetTicketAsync(ticketId));
    }

    [Fact]
    public async Task AddCommentAsync_ValidComment_Succeeds()
    {
        // Arrange
        var ticketId = "TEST-123";
        var comment = "Test comment";
        SetupHttpResponse(HttpStatusCode.Created, "{}");

        var service = CreateService();

        // Act
        var act = () => service.AddCommentAsync(ticketId, comment);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TestConnectionAsync_ValidCredentials_ReturnsTrue()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "{}");
        var service = CreateService();

        // Act
        var result = await service.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidCredentials_ReturnsFalse()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.Unauthorized, "Unauthorized");
        var service = CreateService();

        // Act
        var result = await service.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SearchTicketsAsync_ValidJql_ReturnsTickets()
    {
        // Arrange
        var jql = "project = TEST";
        var responseJson = JsonSerializer.Serialize(new
        {
            issues = new[]
            {
                new
                {
                    key = "TEST-1",
                    fields = new
                    {
                        summary = "Ticket 1",
                        issuetype = new { name = "Task" },
                        priority = new { name = "Medium" },
                        status = new { name = "Done" }
                    }
                },
                new
                {
                    key = "TEST-2",
                    fields = new
                    {
                        summary = "Ticket 2",
                        issuetype = new { name = "Bug" },
                        priority = new { name = "High" },
                        status = new { name = "In Progress" }
                    }
                }
            },
            total = 2
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);
        var service = CreateService();

        // Act
        var result = await service.SearchTicketsAsync(jql);

        // Assert
        result.Should().HaveCount(2);
        result.First().Id.Should().Be("TEST-1");
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
