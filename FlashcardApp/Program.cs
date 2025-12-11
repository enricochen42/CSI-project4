using FlashcardApp.Application.Interfaces;
using FlashcardApp.Application.Services;
using FlashcardApp.Components;
using FlashcardApp.Infrastructure.Data;
using FlashcardApp.Infrastructure.Repositories;
using FlashcardApp.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Memory;
using System.Collections.Generic;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Load User Secrets - try multiple methods to ensure it works cross-platform
// Try standard User Secrets first
try
{
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}
catch
{
    // Silently fail, will try manual fallback
}

// Manual fallback: If API key not loaded, read secrets.json directly in Development mode
// This ensures the API key is available even if User Secrets isn't loaded properly
// This works on both Windows and Mac/Linux
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(builder.Configuration["Groq:ApiKey"]))
{
    try
    {
        // UserSecretsId from .csproj file (FlashcardApp-Secrets)
        // This matches the UserSecretsId property in FlashcardApp.csproj
        const string userSecretsId = "FlashcardApp-Secrets";
        
        // Construct path to secrets.json based on standard User Secrets location (cross-platform)
        // Windows: %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json
        // Mac/Linux: ~/.microsoft/usersecrets/<UserSecretsId>/secrets.json
        string secretsPath;
        
        if (OperatingSystem.IsWindows())
        {
            // Windows path: %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            secretsPath = Path.Combine(appDataPath, "Microsoft", "UserSecrets", userSecretsId, "secrets.json");
        }
        else
        {
            // Mac/Linux path: ~/.microsoft/usersecrets/<UserSecretsId>/secrets.json
            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            secretsPath = Path.Combine(homePath, ".microsoft", "usersecrets", userSecretsId, "secrets.json");
        }
        
        if (File.Exists(secretsPath))
        {
            var secretsJson = File.ReadAllText(secretsPath);
            var secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(secretsJson);
            
            if (secrets != null && secrets.Count > 0)
            {
                var memoryConfig = new Dictionary<string, string?>();
                foreach (var secret in secrets)
                {
                    memoryConfig[secret.Key] = secret.Value;
                }
                builder.Configuration.AddInMemoryCollection(memoryConfig);
            }
        }
    }
    catch (Exception ex)
    {
        // Log the error but don't fail - user will get error when trying to use API
        // This allows the app to start even if secrets can't be loaded
        System.Diagnostics.Debug.WriteLine($"Failed to load User Secrets fallback: {ex.Message}");
    }
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure form options to allow larger file uploads (default is 512KB, increase to 50MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB in bytes
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
});

// Configure Kestrel server limits to allow larger request body sizes
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB in bytes
});

// Add API controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add memory cache for temporary text storage
builder.Services.AddMemoryCache();

// Add database
builder.Services.AddDbContext<FlashcardDbContext>(options =>
    options.UseSqlite("Data Source=flashcards.db"));

// Register repositories
builder.Services.AddScoped<IDeckRepository, DeckRepository>();
builder.Services.AddScoped<IFlashcardRepository, FlashcardRepository>();

// Register application services
builder.Services.AddScoped<IFlashcardService, FlashcardService>();

// Register infrastructure services
builder.Services.AddScoped<IFileProcessingService, FileProcessingService>();
builder.Services.AddHttpClient<IAIGenerationService, AIGenerationService>();
builder.Services.AddScoped<IAIGenerationService, AIGenerationService>();

// Configure HttpClient for Blazor components to use relative URLs
builder.Services.AddHttpClient();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FlashcardDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Only redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


app.UseAntiforgery();

// Configure API pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers(); // Map API controllers

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
