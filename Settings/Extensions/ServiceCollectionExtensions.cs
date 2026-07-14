#pragma warning disable SKEXP0010, SKEXP0001
using AIChat.Settings.MongoDB;
using AIChat.Settings.SemanticKernel;
using AIChat.Services;
using AIChat.Services.Interfaces;
using Microsoft.SemanticKernel;
using AIChat.Services.Implementations;

namespace AIChat.Settings.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Settings
        services.Configure<MongoDbSettings>(
            configuration.GetSection("MongoDbSettings"));
        services.Configure<SemanticKernelSettings>(
            configuration.GetSection("SemanticKernelSettings"));
        services.Configure<FileUploadSettings>(
            configuration.GetSection("FileUploadSettings"));

        // Infrastructure
        services.AddSingleton<MongoDbContext>();

        // Semantic Kernel
        var skSettings = configuration
            .GetSection("SemanticKernelSettings")
            .Get<SemanticKernelSettings>()!;

        services.AddSingleton<Kernel>(sp =>
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: skSettings.ChatModelId,
                apiKey: skSettings.GroqApiKey);
            builder.AddOpenAITextEmbeddingGeneration(
                modelId: skSettings.EmbeddingModelId,
                apiKey: skSettings.OpenAiApiKey);
            return builder.Build();
        });



        // Application Services
        services.AddScoped<IFilesService, FilesService>();
        services.AddScoped<IChunkService, ChunkService>();
        services.AddScoped<IEmbeddingsService, EmbeddingsService>();
        services.AddScoped<IVectorsService, VectorsService>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}

public class FileUploadSettings
{
    public int MaxFileSizeMb { get; set; } = 50;
    public List<string> AllowedExtensions { get; set; } = [".pdf", ".txt", ".docx"];
    public string UploadPath { get; set; } = "uploads";
}
