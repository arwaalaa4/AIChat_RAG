using AIChat.Models;
using AIChat.ViewModels;

namespace AIChat.Services.Interfaces
{
    public interface IFilesService
    {
        Task<IndexingResultVM> UploadAndIndexAsync(
       IFormFile file,
       string chunkingStrategy,
       int chunkSize,
       int chunkOverlap,
       CancellationToken cancellationToken = default);

        Task<List<FileDocument>> GetAllFilesAsync();
        Task<FileDocument?> GetFileByIdAsync(string fileId);
        Task<bool> DeleteFileAsync(string fileId);
        Task<IndexingResultVM> ReIndexFileAsync(string fileId, string chunkingStrategy, int chunkSize, int chunkOverlap);
        Task<string> ExtractTextFromFileAsync(IFormFile file);
    }
}
