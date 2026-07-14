using AIChat.Models;

namespace AIChat.ViewModels.File
{
    public class FileListViewModel
    {
        public List<FileDocument> Files { get; set; } = [];
        public int TotalFiles { get; set; }
        public int TotalChunks { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
