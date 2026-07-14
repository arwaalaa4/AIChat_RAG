using AIChat.Models;
using AIChat.Services.Interfaces;
using AIChat.Settings.MongoDB;
using AIChat.ViewModels;
using MongoDB.Driver;

namespace AIChat.Services.Implementations
{
    public class VectorsService:IVectorsService
    {
        private readonly MongoDbContext _db;
        private readonly ILogger<VectorsService> _logger;

        public VectorsService(MongoDbContext db, ILogger<VectorsService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<SearchResultVM>> SearchAsync(
            float[] queryEmbedding,
            int topK = 5,
            List<string>? filterFileIds = null,
            double minSimilarityScore = 0.0)
        {
            _logger.LogInformation(
                "Vector search: topK={TopK}, fileFilter={Filter}",
                topK, filterFileIds?.Count > 0 ? string.Join(",", filterFileIds) : "none");

            // Build filter
            var filterBuilder = Builders<ChunkDocument>.Filter;
            var filter = filterBuilder.Empty;

            if (filterFileIds?.Count > 0)
                filter = filterBuilder.In(c => c.FileId, filterFileIds);

            // Retrieve candidate chunks (only fields we need)
            var projection = Builders<ChunkDocument>.Projection
                .Include(c => c.Id)
                .Include(c => c.FileId)
                .Include(c => c.FileName)
                .Include(c => c.Text)
                .Include(c => c.ChunkIndex)
                .Include(c => c.Embedding);

            var chunks = await _db.Chunks
                .Find(filter)
                .Project<ChunkDocument>(projection)
                .ToListAsync();

            if (chunks.Count == 0)
            {
                _logger.LogWarning("No chunks found for search (filterFileIds={Ids})", filterFileIds);
                return [];
            }

            // Compute cosine similarity for all candidates
            var scoredChunks = chunks
                .Where(c => c.Embedding.Length > 0)
                .Select(c => new SearchResultVM
                {
                    ChunkId = c.Id,
                    FileId = c.FileId,
                    FileName = c.FileName,
                    Text = c.Text,
                    ChunkIndex = c.ChunkIndex,
                    SimilarityScore = CosineSimilarity(queryEmbedding, c.Embedding)
                })
                .Where(r => r.SimilarityScore >= minSimilarityScore)
                .OrderByDescending(r => r.SimilarityScore)
                .Take(topK)
                .ToList();

            _logger.LogInformation(
                "Found {Count} relevant chunks (top score: {Score:F4})",
                scoredChunks.Count,
                scoredChunks.FirstOrDefault()?.SimilarityScore ?? 0);

            return scoredChunks;
        }

        /// <summary>
        /// Cosine Similarity = (A · B) / (|A| × |B|)
        /// Returns a value in [-1, 1], where 1 = identical direction.
        /// For embeddings, values > 0.75 indicate high semantic similarity.
        /// </summary>
        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Embedding dimension mismatch");

            double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;

            for (var i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            var denom = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
            return denom == 0 ? 0 : dotProduct / denom;
        }
    }
}
