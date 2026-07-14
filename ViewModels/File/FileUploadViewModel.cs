using System.ComponentModel.DataAnnotations;

namespace AIChat.ViewModels.File
{
   
    public class FileUploadViewModel
    {
        [Required(ErrorMessage = "Please select a file to upload")]
        public IFormFile? File { get; set; }

        [Required(ErrorMessage = "Please select a chunking strategy")]
        [Display(Name = "Chunking Strategy")]
        public string ChunkingStrategy { get; set; } = "FixedSize";

        [Range(100, 5000, ErrorMessage = "Chunk size must be between 100 and 5000 characters")]
        [Display(Name = "Chunk Size (characters)")]
        public int ChunkSize { get; set; } = 1000;

        [Range(0, 500, ErrorMessage = "Overlap must be between 0 and 500 characters")]
        [Display(Name = "Chunk Overlap (characters)")]
        public int ChunkOverlap { get; set; } = 100;
    }
}
