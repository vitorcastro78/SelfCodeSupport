namespace SelfCodeSupport.Core.Configuration;

/// <summary>
/// Configurações da API Anthropic Claude
/// </summary>
public class AnthropicSettings
{
    public const string SectionName = "Anthropic";

    /// <summary>
    /// API Key da Anthropic
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Modelo a ser utilizado
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Número máximo de tokens na resposta
    /// </summary>
    public int MaxTokens { get; set; } = 8000;

    /// <summary>
    /// Temperatura para geração (0.0 - 1.0)
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// URL base da API (para casos de proxy)
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>
    /// Versão da API
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Timeout em segundos para requisições
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Número de retentativas em caso de falha
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Modelo alternativo mais barato para análises simples (opcional)
    /// </summary>
    public string? CheaperModel { get; set; }

    /// <summary>
    /// Habilitar cache de análises para economizar créditos
    /// </summary>
    public bool EnableAnalysisCache { get; set; } = true;

    /// <summary>
    /// Tamanho máximo do contexto em caracteres (para otimização)
    /// </summary>
    public int MaxContextSize { get; set; } = 30000;

    /// <summary>
    /// Prompts customizados para diferentes operações
    /// </summary>
    public AnthropicPrompts Prompts { get; set; } = new();
}

/// <summary>
/// Prompts customizados para operações com Claude
/// </summary>
public class AnthropicPrompts
{
    /// <summary>
    /// System prompt base para análise de código
    /// </summary>
    public string SystemPrompt { get; set; } = @"You are a senior developer expert in C# and .NET. 
You analyze code accurately, identify issues and suggest solutions following best practices.
Always consider: Clean Code, SOLID principles, design patterns, security and performance.";

    /// <summary>
    /// Template para análise de ticket
    /// </summary>
    public string AnalysisPromptTemplate { get; set; } = @"Analyze the following JIRA ticket and related code:

## Ticket
ID: {ticketId}
Title: {title}
Description: {description}
Type: {type}

## Current Code
{codeContext}

## Task
1. Identify all files that need to be modified
2. List the necessary changes in each file
3. Identify risks and technical impacts
4. Suggest improvement opportunities
5. Create a step-by-step implementation plan
6. Define validation criteria

Provide the analysis in structured JSON format.";

    /// <summary>
    /// Template para geração de código
    /// </summary>
    public string CodeGenerationPromptTemplate { get; set; } = @"Implement the following solution in C#:

## Context
{context}

## Requirements
{requirements}

## Existing Code
{existingCode}

## Instructions
- Follow existing code patterns
- Implement Clean Code and SOLID principles
- Add appropriate error handling
- Include logging where necessary
- Add XML documentation
- Do not include unnecessary code

Provide only the implemented code, without additional explanations.";

    /// <summary>
    /// Template para geração de testes
    /// </summary>
    public string TestGenerationPromptTemplate { get; set; } = @"Create unit tests for the following code:

## Code to Test
{code}

## Test Framework
- xUnit
- Moq for mocks
- FluentAssertions for assertions

## Instructions
- Cover success and failure cases
- Test edge cases
- Use naming: MethodName_Scenario_ExpectedResult
- Organize with Arrange/Act/Assert

Provide only the test code.";

    /// <summary>
    /// Template para revisão de código
    /// </summary>
    public string CodeReviewPromptTemplate { get; set; } = @"Review the following code and identify:

## Code
{code}

## Review Checklist
1. Bugs or logical errors
2. Security vulnerabilities
3. Performance issues
4. Clean Code/SOLID violations
5. Missing error handling
6. Duplicate code
7. Naming issues
8. Missing documentation

Provide structured feedback with severity (Critical, High, Medium, Low) and correction suggestions.";
}
