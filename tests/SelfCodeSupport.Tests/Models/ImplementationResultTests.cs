using FluentAssertions;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Tests.Models;

public class ImplementationResultTests
{
    [Fact]
    public void IsSuccess_CompletedWithNoErrors_ReturnsTrue()
    {
        // Arrange
        var result = new ImplementationResult
        {
            Status = ImplementationStatus.Completed,
            BuildResult = new BuildResult { IsSuccess = true },
            TestResult = new TestResult { TotalTests = 10, PassedTests = 10, FailedTests = 0 }
        };

        // Act & Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsSuccess_CompletedWithErrors_ReturnsFalse()
    {
        // Arrange
        var result = new ImplementationResult
        {
            Status = ImplementationStatus.Completed,
            Errors = new List<ImplementationError>
            {
                new() { Message = "Build failed" }
            }
        };

        // Act & Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsSuccess_BuildFailed_ReturnsFalse()
    {
        // Arrange
        var result = new ImplementationResult
        {
            Status = ImplementationStatus.Completed,
            BuildResult = new BuildResult { IsSuccess = false }
        };

        // Act & Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsSuccess_TestsFailed_ReturnsFalse()
    {
        // Arrange
        var result = new ImplementationResult
        {
            Status = ImplementationStatus.Completed,
            BuildResult = new BuildResult { IsSuccess = true },
            TestResult = new TestResult { TotalTests = 10, PassedTests = 8, FailedTests = 2 }
        };

        // Act & Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Duration_WithCompletedAt_ReturnsCorrectDuration()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 15, 10, 0, 0);
        var endTime = new DateTime(2024, 1, 15, 10, 30, 0);
        
        var result = new ImplementationResult
        {
            StartedAt = startTime,
            CompletedAt = endTime
        };

        // Act & Assert
        result.Duration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Duration_WithoutCompletedAt_ReturnsNull()
    {
        // Arrange
        var result = new ImplementationResult
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = null
        };

        // Act & Assert
        result.Duration.Should().BeNull();
    }

    [Fact]
    public void GetJiraSummary_CompletedImplementation_GeneratesCorrectMarkdown()
    {
        // Arrange
        var result = new ImplementationResult
        {
            TicketId = "TEST-123",
            BranchName = "feature/TEST-123-add-search",
            Status = ImplementationStatus.Completed,
            StartedAt = new DateTime(2024, 1, 15, 10, 0, 0),
            CompletedAt = new DateTime(2024, 1, 15, 10, 30, 0),
            CreatedFiles = new List<FileChange>
            {
                new() { Path = "DTOs/SearchDto.cs", LinesAdded = 20 }
            },
            ModifiedFiles = new List<FileChange>
            {
                new() { Path = "Controllers/UserController.cs", LinesAdded = 15, LinesRemoved = 2 }
            },
            BuildResult = new BuildResult { IsSuccess = true },
            TestResult = new TestResult
            {
                TotalTests = 25,
                PassedTests = 25,
                FailedTests = 0,
                CodeCoverage = 0.85
            },
            PullRequestUrl = "https://github.com/org/repo/pull/42",
            PullRequestNumber = 42
        };

        // Act
        var summary = result.GetJiraSummary();

        // Assert
        summary.Should().Contain("## 🚀 IMPLEMENTAÇÃO CONCLUÍDA");
        summary.Should().Contain("**Branch:** `feature/TEST-123-add-search`");
        summary.Should().Contain("**Status:** Completed");
        summary.Should().Contain("**Duração:** 30 minutos");
        summary.Should().Contain("Arquivos criados: 1");
        summary.Should().Contain("Arquivos modificados: 1");
        summary.Should().Contain("### 🔨 Build");
        summary.Should().Contain("✅ Sucesso");
        summary.Should().Contain("### 🧪 Testes");
        summary.Should().Contain("Total: 25");
        summary.Should().Contain("Passaram: 25");
        summary.Should().Contain("Cobertura: 85");
        summary.Should().Contain("### 🔗 Pull Request");
        summary.Should().Contain("[PR #42]");
    }

    [Fact]
    public void GetJiraSummary_FailedBuild_ShowsErrors()
    {
        // Arrange
        var result = new ImplementationResult
        {
            Status = ImplementationStatus.BuildFailed,
            BuildResult = new BuildResult
            {
                IsSuccess = false,
                Errors = new List<string>
                {
                    "CS0246: Type 'UserDto' could not be found",
                    "CS0103: Name 'logger' does not exist"
                }
            }
        };

        // Act
        var summary = result.GetJiraSummary();

        // Assert
        summary.Should().Contain("❌ Falhou");
        summary.Should().Contain("CS0246");
        summary.Should().Contain("CS0103");
    }
}
