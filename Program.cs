using Amazon.ECS.Model;
using Confluent.Kafka;
using EasyOps.Models;
using EasyOps.Services;
using EasyOps.Services.Kafka;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;
using Smf.KafkaLib.Producer;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Default";
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

Log.Logger = new LoggerConfiguration()
      .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
     .Enrich.WithProperty("Environment", environment)
   .Enrich.WithProperty("Version", version)
   .WriteTo.Async(l => l.Console(new CompactJsonFormatter()))
     .CreateLogger();

builder.Configuration
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Default"}.json", true)
    .AddEnvironmentVariables();

builder.Services
    .AddOptions()
    .Configure<ProducerConfig>(builder.Configuration.GetSection("Kafka:Producer"));

// Configure IIS integration
builder.WebHost.UseIISIntegration();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers()
  .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddHttpClient();

// Add session support for storing user credentials
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure Jenkins settings
builder.Services.Configure<JenkinsConfiguration>(
    builder.Configuration.GetSection("Jenkins"));

// Configure AWS settings
builder.Services.Configure<AwsConfiguration>(
    builder.Configuration.GetSection("Aws"));

// Register authentication service
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// Register AWS services
builder.Services.AddScoped<IAwsAuthenticationService, AwsAuthenticationService>();
builder.Services.AddScoped<IAwsEcsService, AwsEcsService>();

// Register database context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

// Register database services
builder.Services.AddScoped<IDatabaseService, DatabaseService>();

builder.Services.AddScoped<IIdentityManagementService, IdentityManagementService>();
builder.Services.AddHttpClient<IIdentityManagementService, IdentityManagementService>(client =>
{
    var url = builder.Configuration["IdentityManagementUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(url);
});


builder.Services.AddKafkaProducer<string, IMessage>(builder.Configuration, producer =>
{
    producer.WithEmptyLeadInProtobufSerializerAndHeaders()
        .AddTracingHeaderGenerator();
});

builder.Services.AddSingleton<IPublisherService, PublisherService>();
builder.Services.AddScoped<IReplayService, ReplayService>();

Console.WriteLine("✅ Kafka producer and replay services registered successfully");


// NOW build the app - AFTER all services are registered
var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
    await dbService.InitializeDatabaseAsync();

    // Initialize AWS service
    var awsService = scope.ServiceProvider.GetRequiredService<IAwsAuthenticationService>();
    await awsService.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseSession(); // Enable session middleware

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
// Register Kafka producer and PublisherService with error handling
// Kafka requires AWS IAM credentials when using AwsSaslSsl security
bool kafkaRegistered = false;
try
{
    // Check if we're in development and if AWS credentials are available
    var isDevelopment = builder.Environment.IsDevelopment();
    var kafkaConfig = builder.Configuration.GetSection("Kafka:Producer");
    var securityType = kafkaConfig.GetValue<string>("Security:SecurityProviderType");

    if (securityType == "AwsSaslSsl")
    {
        Console.WriteLine("⚠️  Kafka is configured with AWS IAM authentication (AwsSaslSsl)");
        Console.WriteLine("   Checking AWS credentials availability...");

        // Try to get AWS credentials from environment or profile
        var hasAwsCredentials = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")) ||
                               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_PROFILE")) ||
                               System.IO.File.Exists(Path.Combine(
                                   Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                   ".aws", "credentials"));

        if (!hasAwsCredentials && isDevelopment)
        {
            Console.WriteLine("⚠️  No AWS credentials found. Kafka functionality will be disabled.");
            Console.WriteLine("   To enable Kafka:");
            Console.WriteLine("   1. Set AWS_PROFILE environment variable (e.g., set AWS_PROFILE=dev)");
            Console.WriteLine("   2. Or run: aws configure --profile dev");
            Console.WriteLine("   3. Or use AWS SSO: aws sso login --profile dev");
            throw new InvalidOperationException("AWS credentials not available for Kafka IAM authentication");
        }
    }

    // Attempt to register Kafka producer
    builder.Services.AddKafkaProducer<string, IMessage>(builder.Configuration, producer =>
    {
        producer.WithEmptyLeadInProtobufSerializerAndHeaders()
            .AddTracingHeaderGenerator();
    });

    builder.Services.AddSingleton<IPublisherService, PublisherService>();
    kafkaRegistered = true;

    Console.WriteLine("✅ Kafka producer and PublisherService registered successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Failed to register Kafka producer: {ex.Message}");
    Console.WriteLine("   Replay execution functionality will not be available");
    Console.WriteLine("   The application will continue running with limited functionality");
}

// Always register ReplayService (even without Kafka, GET operations will work)
builder.Services.AddScoped<IReplayService, ReplayService>();
Console.WriteLine(kafkaRegistered
    ? "✅ ReplayService registered with full Kafka support"
    : "⚠️  ReplayService registered without Kafka (execution disabled)");
