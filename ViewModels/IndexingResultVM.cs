namespace AIChat.ViewModels
{
    public class IndexingResultVM
    {
        public bool Success { get; set; }
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int ChunksCreated { get; set; }
        public string Strategy { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}
