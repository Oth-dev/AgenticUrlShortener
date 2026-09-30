using System.Text.Json.Serialization;
using AgenticUrlShortener.Api.Orchestration;
using AgenticUrlShortener.Api.Repositories;
using AgenticUrlShortener.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// URL shortener domain.
builder.Services.AddSingleton<IUrlRepository, InMemoryUrlRepository>();
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

// Agentic orchestration.
// In-memory stores keep this take-home prototype easy to run.
// Interfaces make them replaceable by SQL/Redis later.
builder.Services.AddSingleton<IWorkflowStore, InMemoryWorkflowStore>();
builder.Services.AddSingleton<IMetricsStore, InMemoryMetricsStore>();
builder.Services.AddSingleton<IPolicyGuardrail, PolicyGuardrail>();
builder.Services.AddSingleton<IWorkflowGraphFactory, WorkflowGraphFactory>();
builder.Services.AddScoped<IAgenticOrchestrator, AgenticOrchestrator>();

// Optional local Ollama model. Enabled=false keeps unit/integration tests offline.
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));
builder.Services.AddHttpClient<IAgentExecutor, LlmAgentExecutor>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlmOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    utc = DateTimeOffset.UtcNow
}));

app.Run();

// Exposed for WebApplicationFactory integration tests.
public partial class Program { }
