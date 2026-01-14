using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SelfCodeSupport.Core.Configuration;
using SelfCodeSupport.Core.Interfaces;
using SelfCodeSupport.Infrastructure.Data;
using SelfCodeSupport.Infrastructure.Services;

namespace SelfCodeSupport.Infrastructure;

/// <summary>
/// Extensões para configuração de Dependency Injection
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adiciona os serviços de infraestrutura ao container de DI
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configurações
        services.Configure<JiraSettings>(configuration.GetSection(JiraSettings.SectionName));
        services.Configure<GitSettings>(configuration.GetSection(GitSettings.SectionName));
        services.Configure<AnthropicSettings>(configuration.GetSection(AnthropicSettings.SectionName));
        services.Configure<WorkflowSettings>(configuration.GetSection(WorkflowSettings.SectionName));

        // HttpClients com timeouts aumentados para evitar TaskCanceledException
        services.AddHttpClient<IJiraService, JiraService>()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120); // Aumentado de 30 para 120 segundos
            });

        services.AddHttpClient<IAnthropicService, AnthropicService>()
            .ConfigureHttpClient(client =>
            {
                // Timeout será configurado no serviço baseado nas settings (300s)
                // Não definir aqui para evitar conflito
            });

        services.AddHttpClient<IPullRequestService, GitHubPullRequestService>()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120); // Aumentado de 30 para 120 segundos
            });

        // Database - SQLite
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SelfCodeSupport",
            "settings.db");

        // Garantir que o diretório existe
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Serviços
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();

        return services;
    }
}
