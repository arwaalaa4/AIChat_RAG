namespace AIChat.Services.Interfaces
{
    public interface IEmbeddingsService
    {
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
        Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts, CancellationToken ct = default);
    }
}
