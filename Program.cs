using AIChat.Settings.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;
using Microsoft.SemanticKernel;
//using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(opts =>
    {
        opts.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
    });
var configuration = builder.Configuration;

builder.Services.AddKernel()
    .AddOpenAIChatCompletion(
        modelId: builder.Configuration["SemanticKernelSettings:ChatModelId"]!,
        apiKey: builder.Configuration["SemanticKernelSettings:GroqApiKey"]!,
        endpoint: new Uri(builder.Configuration["SemanticKernelSettings:GroqEndpoint"]!))
    .AddOpenAITextEmbeddingGeneration(
        modelId: builder.Configuration["SemanticKernelSettings:EmbeddingModelId"]!,
        apiKey: builder.Configuration["SemanticKernelSettings:OpenAiApiKey"]!);

// ── Application Services (MongoDB + SK + all domain services) ─────────────
builder.Services.AddApplicationServices(builder.Configuration);

// ── Session (for chat history in-memory, optional) ─────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddDataProtection()
    .SetApplicationName("ChatWithYourFiles");

// ── Logging ────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ── File Upload limits ─────────────────────────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600; // 100MB
});

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── Ensure uploads directory exists ───────────────────────────────────────
var uploadPath = Path.Combine(app.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadPath);

app.Run();
