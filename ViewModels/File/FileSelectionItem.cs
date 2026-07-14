namespace AIChat.ViewModels.File
{
    public class FileSelectionItem
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int ChunkCount { get; set; }
        public bool IsSelected { get; set; }
    }
}
