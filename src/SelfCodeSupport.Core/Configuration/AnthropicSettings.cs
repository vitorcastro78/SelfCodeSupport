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
    public string SystemPrompt { get; set; } = @"Você é um desenvolvedor sênior especialista em C# e .NET. 
Você analisa código com precisão, identifica problemas e sugere soluções seguindo as melhores práticas.
Sempre considere: Clean Code, SOLID, padrões de projeto, segurança e performance.";

    /// <summary>
    /// Template para análise de ticket
    /// </summary>
    public string AnalysisPromptTemplate { get; set; } = @"Analise o seguinte ticket JIRA e o código relacionado:

## Ticket
ID: {ticketId}
Título: {title}
Descrição: {description}
Tipo: {type}

## Código Atual
{codeContext}

## Tarefa
1. Identifique todos os arquivos que precisam ser modificados
2. Liste as mudanças necessárias em cada arquivo
3. Identifique riscos e impactos técnicos
4. Sugira oportunidades de melhoria
5. Crie um plano de implementação passo a passo
6. Defina critérios de validação

Forneça a análise em formato JSON estruturado.";

    /// <summary>
    /// Template para geração de código
    /// </summary>
    public string CodeGenerationPromptTemplate { get; set; } = @"Implemente a seguinte solução em C#:

## Contexto
{context}

## Requisitos
{requirements}

## Código Existente
{existingCode}

## Instruções
- Siga os padrões do código existente
- Implemente Clean Code e SOLID
- Adicione tratamento de erros apropriado
- Inclua logging onde necessário
- Adicione XML documentation
- Não inclua código desnecessário

Forneça apenas o código implementado, sem explicações adicionais.";

    /// <summary>
    /// Template para geração de testes
    /// </summary>
    public string TestGenerationPromptTemplate { get; set; } = @"Crie testes unitários para o seguinte código:

## Código a Testar
{code}

## Framework de Testes
- xUnit
- Moq para mocks
- FluentAssertions para assertions

## Instruções
- Cubra casos de sucesso e falha
- Teste edge cases
- Use nomenclatura: MethodName_Scenario_ExpectedResult
- Organize com Arrange/Act/Assert

Forneça apenas o código dos testes.";

    /// <summary>
    /// Template para revisão de código
    /// </summary>
    public string CodeReviewPromptTemplate { get; set; } = @"Revise o seguinte código e identifique:

## Código
{code}

## Checklist de Revisão
1. Bugs ou erros lógicos
2. Vulnerabilidades de segurança
3. Problemas de performance
4. Violações de Clean Code/SOLID
5. Falta de tratamento de erros
6. Código duplicado
7. Problemas de nomenclatura
8. Falta de documentação

Forneça feedback estruturado com severidade (crítico, alto, médio, baixo) e sugestões de correção.";
}
