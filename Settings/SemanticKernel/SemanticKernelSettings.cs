namespace AIChat.Settings.SemanticKernel;

public class SemanticKernelSettings
{
    public string GroqApiKey { get; set; } = string.Empty;
    public string OpenAiApiKey { get; set; } = string.Empty;

    public string EmbeddingModelId { get; set; } = "gemini-embedding-001";
    public string ChatModelId { get; set; } = "gemini-2.5-flash";
    public int EmbeddingDimensions { get; set; } = 1536;
    public int MaxTokensPerChunk { get; set; } = 500;
    public int ChunkOverlapTokens { get; set; } = 50;
    public int TopKResults { get; set; } = 5;
}