namespace AIChat.ViewModels
{
    public class SearchResultVM
    {
        public string ChunkId { get; set; } = string.Empty;
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public double SimilarityScore { get; set; }
    }
}
