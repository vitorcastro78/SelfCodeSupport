using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SelfCodeSupport.API.Hubs;
using SelfCodeSupport.Infrastructure;
using SelfCodeSupport.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "SelfCodeSupport API",
        Description = "API para automação de desenvolvimento integrado com JIRA, Git e Anthropic AI",
        Contact = new OpenApiContact
        {
            Name = "SelfCodeSupport",
            Url = new Uri("https://github.com/vitorcastro78/SelfCodeSupport")
        },
        License = new OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add SignalR
builder.Services.AddSignalR();

// Add WorkflowProgressNotifier (depends on SignalR)
builder.Services.AddSingleton<SelfCodeSupport.API.Services.WorkflowProgressNotifier>();

// Add Infrastructure services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add CORS - Permite qualquer origem, método e header
// Configurado para funcionar com ngrok e evitar erro 307 em preflight requests
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()      // Aceita qualquer origem
              .AllowAnyMethod()      // Aceita qualquer método (GET, POST, PUT, DELETE, OPTIONS, etc.)
              .AllowAnyHeader()      // Aceita qualquer header
              .WithExposedHeaders("*") // Expõe todos os headers na resposta
              .SetPreflightMaxAge(TimeSpan.FromHours(24)); // Cache preflight por 24h
    });
    
    // Política adicional para desenvolvimento com credenciais (se necessário)
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("*")
              .SetPreflightMaxAge(TimeSpan.FromHours(24));
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Swagger habilitado em todos os ambientes
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SelfCodeSupport API v1");
    options.RoutePrefix = string.Empty; // Swagger na raiz (página inicial)
    options.DocumentTitle = "SelfCodeSupport - API Documentation";
    options.DefaultModelsExpandDepth(-1); // Esconde schemas por padrão
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    options.EnableDeepLinking();
    options.DisplayRequestDuration();
});

// Ordem correta do middleware pipeline para ngrok e CORS
app.UseRouting();

// Tratar requisições OPTIONS (preflight CORS) antes de qualquer redirecionamento
// Isso evita o erro 307 quando o ngrok faz proxy HTTP->HTTPS
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        // Responder diretamente para OPTIONS sem passar pelo pipeline completo
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "*";
        context.Response.Headers["Access-Control-Allow-Headers"] = "*";
        context.Response.Headers["Access-Control-Max-Age"] = "86400";
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync(string.Empty);
        return;
    }
    await next();
});

app.UseCors(); // CORS para outras requisições

// Desabilitar HTTPS redirection em desenvolvimento quando usar ngrok
// O ngrok já fornece HTTPS, então não precisamos redirecionar HTTP para HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

// Map SignalR Hub
app.MapHub<WorkflowHub>("/hubs/workflow");

// Inicializar banco de dados
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate(); // Aplica migrations automaticamente
}

app.Run();
