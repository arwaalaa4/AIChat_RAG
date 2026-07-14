using AIChat.Services.Interfaces;
using Microsoft.Extensions.Logging;


namespace AIChat.Services.Implementations
{
    /// <summary>
    /// Provides two core chunking strategies for splitting document text.
    ///
    /// Strategy Comparison:
    /// ┌─────────────────────┬──────────────────────┬────────────────────────┐
    /// │ Feature             │ FixedSize            │ Overlapping            │
    /// ├─────────────────────┼──────────────────────┼────────────────────────┤
    /// │ Context Continuity  │ Low (hard cuts)      │ High (sliding window)  │
    /// │ Storage Cost        │ Low                  │ Higher (repeated text) │
    /// │ Processing Speed    │ Fast                 │ Moderate               │
    /// │ Best For            │ News, logs, uniform  │ Legal, technical docs  │
    /// │                     │ structured content   │ Q&A, research papers   │
    /// │ Chunk Count         │ n = len/chunkSize    │ Higher than FixedSize  │
    /// └─────────────────────┴──────────────────────┴────────────────────────┘
    /// </summary>
    public class ChunkService:IChunkService
    {
        private readonly ILogger<ChunkService> _logger;

        public ChunkService(ILogger<ChunkService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// FixedSize Strategy: splits every N characters, no overlap.
        /// Fast and memory-efficient. May cut sentences mid-way.
        /// </summary>
        public List<string> ChunkFixed(string text, int chunkSize)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            if (chunkSize <= 0)
                throw new ArgumentException("Chunk size must be greater than 0", nameof(chunkSize));

            var chunks = new List<string>();
            var cleanText = NormalizeText(text);
            var position = 0;

            while (position < cleanText.Length)
            {
                var end = Math.Min(position + chunkSize, cleanText.Length);

                // Try to end at a sentence boundary for cleaner chunks
                if (end < cleanText.Length)
                {
                    var sentenceEnd = FindNearestSentenceBoundary(cleanText, position, end);
                    if (sentenceEnd > position)
                        end = sentenceEnd;
                }

                var chunk = cleanText[position..end].Trim();

                if (!string.IsNullOrWhiteSpace(chunk))
                    chunks.Add(chunk);

                position = end;
            }

            _logger.LogDebug(
                "FixedSize chunking produced {Count} chunks from {Length} characters (chunkSize={Size})",
                chunks.Count, cleanText.Length, chunkSize);

            return chunks;
        }

        /// <summary>
        /// Overlapping Strategy: sliding window with configurable overlap.
        /// Preserves context across chunk boundaries — critical for Q&A accuracy.
        ///
        /// Example with chunkSize=100, overlap=20:
        ///   Chunk 1: chars  0-100
        ///   Chunk 2: chars 80-180   ← 20 char overlap
        ///   Chunk 3: chars 160-260
        /// </summary>
        public List<string> ChunkWithOverlap(string text, int chunkSize, int overlapSize)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            if (chunkSize <= 0)
                throw new ArgumentException("Chunk size must be > 0", nameof(chunkSize));

            if (overlapSize < 0)
                throw new ArgumentException("Overlap must be >= 0", nameof(overlapSize));

            if (overlapSize >= chunkSize)
                throw new ArgumentException("Overlap must be less than chunk size", nameof(overlapSize));

            var chunks = new List<string>();
            var cleanText = NormalizeText(text);
            var step = chunkSize - overlapSize;
            var position = 0;

            while (position < cleanText.Length)
            {
                var end = Math.Min(position + chunkSize, cleanText.Length);
                var chunk = cleanText[position..end].Trim();

                if (!string.IsNullOrWhiteSpace(chunk))
                    chunks.Add(chunk);

                if (end >= cleanText.Length)
                    break;

                position += step;
            }

            _logger.LogDebug(
                "Overlapping chunking produced {Count} chunks (chunkSize={Size}, overlap={Overlap})",
                chunks.Count, chunkSize, overlapSize);

            return chunks;
        }

        public List<string> Chunk(string text, string strategy, int chunkSize, int overlapSize)
        {
            return strategy.ToLowerInvariant() switch
            {
                "fixedsize" or "fixed" => ChunkFixed(text, chunkSize),
                "overlapping" or "overlap" => ChunkWithOverlap(text, chunkSize, overlapSize),
                _ => throw new ArgumentException($"Unknown chunking strategy: {strategy}. Use 'FixedSize' or 'Overlapping'.")
            };
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static string NormalizeText(string text)
        {
            // Collapse multiple whitespace/newlines into single spaces
            var normalized = System.Text.RegularExpressions.Regex
                .Replace(text, @"\s+", " ");
            return normalized.Trim();
        }

        private static int FindNearestSentenceBoundary(string text, int start, int preferredEnd)
        {
            // Look backward from preferredEnd for '.', '!', '?', '\n'
            var sentenceEnders = new[] { '.', '!', '?', '\n' };
            var searchFrom = Math.Max(start, preferredEnd - 100);

            for (var i = preferredEnd; i > searchFrom; i--)
            {
                if (sentenceEnders.Contains(text[i - 1]))
                    return i;
            }

            return preferredEnd; // No boundary found — use hard cut
        }
    }
}
