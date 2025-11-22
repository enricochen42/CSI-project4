using FlashcardApp.Application.Interfaces;
using FlashcardApp.Application.Services;
using FlashcardApp.Components;
using FlashcardApp.Infrastructure.Data;
using FlashcardApp.Infrastructure.Repositories;
using FlashcardApp.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
