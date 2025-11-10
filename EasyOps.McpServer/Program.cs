using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EasyOps.Shared.Models;
using EasyOps.Shared.Services;
using EasyOps.McpServer.Mcp;

// Determine the base path - use the directory where the executable is located
var basePath = AppContext.BaseDirectory;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(basePath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Setup dependency injection
var services = new ServiceCollection();

// Add logging - only to stderr to keep stdout clean for MCP protocol
services.AddLogging(builder =>
{
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });
});

// Add configuration
var jenkinsConfig = configuration.GetSection("Jenkins").Get<JenkinsConfiguration>();
var easyOpsApiConfig = configuration.GetSection("EasyOpsApi").Get<EasyOpsApiConfiguration>() 
    ?? new EasyOpsApiConfiguration { BaseUrl = "http://localhost:5000" };

// Determine which service to use based on configuration
bool useEasyOpsApi = !string.IsNullOrEmpty(easyOpsApiConfig.BaseUrl);

if (useEasyOpsApi)
{
    services.AddSingleton(easyOpsApiConfig);
    services.AddTransient<IJenkinsService, EasyOpsApiService>();
}
else if (jenkinsConfig != null)
{
    services.AddSingleton(jenkinsConfig);
    services.AddTransient<IJenkinsService, JenkinsService>();
}
else
{
    throw new InvalidOperationException("Either EasyOpsApi or Jenkins configuration must be provided");
}
services.AddTransient<JenkinsTools>();
services.AddTransient<McpServerHandler>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Run MCP server
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting EasyOps Jenkins MCP Server...");

var mcpServer = serviceProvider.GetRequiredService<McpServerHandler>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    logger.LogInformation("Shutdown signal received");
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await mcpServer.RunAsync(cts.Token);
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error in MCP server");
    return 1;
}

logger.LogInformation("EasyOps Jenkins MCP Server stopped");
return 0;
