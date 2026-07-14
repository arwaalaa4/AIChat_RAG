using AIChat.Services.Interfaces;
#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace AIChat.Services.Implementations
{
    public class EmbeddingsService:IEmbeddingsService
    {
        private readonly Kernel _kernel;
        private readonly ILogger<EmbeddingsService> _logger;
        private const int BatchSize = 20; // OpenAI rate-limit friendly batch size

        public EmbeddingsService(Kernel kernel, ILogger<EmbeddingsService> logger)
        {
            _kernel = kernel;
            _logger = logger;
        }
        /// <summary>
        /// Generates a single embedding vector for the given text.
        /// Uses OpenAI's text-embedding-3-small (1536 dimensions).
        /// </summary>
        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be empty for embedding generation", nameof(text));

            try
            {
                var textEmbeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                var embeddings = await textEmbeddingService.GenerateEmbeddingsAsync([text], cancellationToken: ct);
                return embeddings[0].ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for text of length {Length}", text.Length);
                throw;
            }
        }

        /// <summary>
        /// Generates embeddings for a list of texts in controlled batches.
        /// Respects OpenAI rate limits by processing in chunks of {BatchSize}.
        /// </summary>
        public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(
            List<string> texts,
            CancellationToken ct = default)
        {
            if (texts.Count == 0)
                return [];

            var allEmbeddings = new List<float[]>(texts.Count);
            var textEmbeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();

            _logger.LogInformation(
                "Generating embeddings for {Count} texts in batches of {BatchSize}",
                texts.Count, BatchSize);

            for (var i = 0; i < texts.Count; i += BatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = texts.Skip(i).Take(BatchSize).ToList();

                try
                {
                    var batchEmbeddings = await textEmbeddingService
                        .GenerateEmbeddingsAsync(batch, cancellationToken: ct);

                    allEmbeddings.AddRange(batchEmbeddings.Select(e => e.ToArray()));

                    _logger.LogDebug(
                        "Embedded batch {BatchNum}/{Total} ({Count} items)",
                        (i / BatchSize) + 1,
                        (int)Math.Ceiling((double)texts.Count / BatchSize),
                        batch.Count);

                    // Polite delay between batches to avoid rate limiting
                    if (i + BatchSize < texts.Count)
                        await Task.Delay(200, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed embedding batch starting at index {Index}", i);
                    throw;
                }
            }

            return allEmbeddings;
        }
    }
}
