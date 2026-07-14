namespace AIChat.ViewModels.File
{
    public class SourceReference
    {
        public string FileName { get; set; } = string.Empty;
        public string ChunkPreview { get; set; } = string.Empty;
        public double SimilarityScore { get; set; }
    }
}
