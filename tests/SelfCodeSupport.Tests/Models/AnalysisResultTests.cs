using FluentAssertions;
using SelfCodeSupport.Core.Models;

namespace SelfCodeSupport.Tests.Models;

public class AnalysisResultTests
{
    [Fact]
    public void FormattedJiraComment_WithCompleteAnalysis_GeneratesCorrectMarkdown()
    {
        // Arrange
        var analysis = new AnalysisResult
        {
            TicketId = "TEST-123",
            AnalyzedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Complexity = Complexity.Medium,
            EstimatedEffortHours = 4,
            AffectedFiles = new List<AffectedFile>
            {
                new() { Path = "Controllers/UserController.cs", ChangeType = FileChangeType.Modify },
                new() { Path = "Services/UserService.cs", ChangeType = FileChangeType.Modify }
            },
            RequiredChanges = new List<RequiredChange>
            {
                new() { Component = "UserController", Description = "Add search endpoint", Category = ChangeCategory.Controller },
                new() { Component = "UserService", Description = "Implement search logic", Category = ChangeCategory.Service }
            },
            TechnicalImpact = new TechnicalImpact
            {
                HasBreakingChanges = false,
                RequiresMigration = false,
                NewDependencies = new List<string>()
            },
            Risks = new List<Risk>
            {
                new() { Description = "Performance impact on large datasets", Severity = RiskSeverity.Medium, Mitigation = "Add pagination" }
            },
            Opportunities = new List<Opportunity>
            {
                new() { Description = "Add caching for search results", Type = OpportunityType.Performance }
            },
            ImplementationPlan = new List<ImplementationStep>
            {
                new() { Order = 1, Description = "Create search DTO" },
                new() { Order = 2, Description = "Implement service method" },
                new() { Order = 3, Description = "Add controller endpoint" }
            },
            ValidationCriteria = new List<ValidationCriteria>
            {
                new() { Description = "Unit tests pass", Type = ValidationType.UnitTest },
                new() { Description = "Manual API testing", Type = ValidationType.ManualTest }
            }
        };

        // Act
        var result = analysis.FormattedJiraComment;

        // Assert
        result.Should().Contain("## 🔍 ANÁLISE TÉCNICA");
        result.Should().Contain("**Complexidade:** Medium");
        result.Should().Contain("**Estimativa:** ~4h");
        result.Should().Contain("### 📁 Arquivos Identificados");
        result.Should().Contain("`Controllers/UserController.cs`");
        result.Should().Contain("### 🔧 Mudanças Necessárias");
        result.Should().Contain("**UserController**");
        result.Should().Contain("### ⚠️ Impactos e Riscos");
        result.Should().Contain("[Medium] Performance impact");
        result.Should().Contain("### ✨ Oportunidades de Melhoria");
        result.Should().Contain("Add caching");
        result.Should().Contain("### 📋 Plano de Implementação");
        result.Should().Contain("1. Create search DTO");
        result.Should().Contain("### ✅ Critérios de Validação");
        result.Should().Contain("- [ ] Unit tests pass");
        result.Should().Contain("⏸️ **Aguardando aprovação");
    }

    [Fact]
    public void FormattedJiraComment_WithBreakingChanges_ShowsWarning()
    {
        // Arrange
        var analysis = new AnalysisResult
        {
            TicketId = "TEST-456",
            TechnicalImpact = new TechnicalImpact
            {
                HasBreakingChanges = true,
                RequiresMigration = true,
                NewDependencies = new List<string> { "Newtonsoft.Json" }
            }
        };

        // Act
        var result = analysis.FormattedJiraComment;

        // Assert
        result.Should().Contain("⚠️ **BREAKING CHANGE**");
        result.Should().Contain("🗄️ Requer migration");
        result.Should().Contain("📦 Novas dependências: Newtonsoft.Json");
    }

    [Fact]
    public void FormattedJiraComment_EmptyAnalysis_GeneratesMinimalMarkdown()
    {
        // Arrange
        var analysis = new AnalysisResult
        {
            TicketId = "TEST-789"
        };

        // Act
        var result = analysis.FormattedJiraComment;

        // Assert
        result.Should().Contain("## 🔍 ANÁLISE TÉCNICA");
        result.Should().Contain("⏸️ **Aguardando aprovação");
    }
}
