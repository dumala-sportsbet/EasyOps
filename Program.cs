using EasyOps.Models;
using EasyOps.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
