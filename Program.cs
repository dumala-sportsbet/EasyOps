using EasyOps.Models;
using EasyOps.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure IIS integration
builder.WebHost.UseIISIntegration();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
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
builder.Services.AddSingleton<IAwsAuthenticationService, AwsAuthenticationService>();
builder.Services.AddScoped<IAwsEcsService, AwsEcsService>();

var app = builder.Build();

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
