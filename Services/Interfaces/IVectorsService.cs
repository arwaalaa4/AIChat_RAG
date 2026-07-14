using AIChat.ViewModels;

namespace AIChat.Services.Interfaces
{
    public interface IVectorsService
    {
        Task<List<SearchResultVM>> SearchAsync(
              float[] queryEmbedding,
              int topK = 5,
              List<string>? filterFileIds = null,
              double minSimilarityScore = 0.0);
    }
}
