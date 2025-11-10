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
