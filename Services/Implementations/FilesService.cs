using AIChat.Services.Interfaces;
using AIChat.ViewModels;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using AIChat.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using AIChat.Settings.MongoDB;
using AIChat.Settings.Extensions;

namespace AIChat.Services.Implementations
{
    public class FilesService:IFilesService
    {
        private readonly MongoDbContext _db;
        private readonly IChunkService _chunkingService;
        private readonly IEmbeddingsService _embeddingService;
        private readonly IWebHostEnvironment _env;
        private readonly FileUploadSettings _uploadSettings;
        private readonly ILogger<FilesService> _logger;

        public FilesService(
            MongoDbContext db,
            IChunkService chunkingService,
            IEmbeddingsService embeddingService,
            IWebHostEnvironment env,
            IOptions<FileUploadSettings> uploadSettings,
            ILogger<FilesService> logger)
        {
            _db = db;
            _chunkingService = chunkingService;
            _embeddingService = embeddingService;
            _env = env;
            _uploadSettings = uploadSettings.Value;
            _logger = logger;
        }
    


        public async Task<IndexingResultVM> UploadAndIndexAsync(
            IFormFile file,
            string chunkingStrategy,
            int chunkSize,
            int chunkOverlap,
           CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // ── Validate ──────────────────────────────────────────────────────
            ValidateFile(file);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var safeFileName = $"{Guid.NewGuid()}{ext}";
            var uploadDir = Path.Combine(_env.WebRootPath, _uploadSettings.UploadPath);
            Directory.CreateDirectory(uploadDir);
            var storagePath = Path.Combine(uploadDir, safeFileName);

            // ── Save file to disk ─────────────────────────────────────────────
            await using (var stream = new FileStream(storagePath, FileMode.Create))
                await file.CopyToAsync(stream, cancellationToken);

            // ── Extract text ──────────────────────────────────────────────────
            string extractedText;
            try
            {
                extractedText = await ExtractTextFromPathAsync(storagePath, ext);
            }
            catch (Exception ex)
            {
                File.Delete(storagePath);
                _logger.LogError(ex, "Text extraction failed for {File}", file.FileName);
                return new IndexingResultVM
                {
                    Success = false,
                    FileName = file.FileName,
                    ErrorMessage = $"Failed to extract text: {ex.Message}"
                };
            }

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new IndexingResultVM
                {
                    Success = false,
                    FileName = file.FileName,
                    ErrorMessage = "No text content found in file"
                };
            }

            // ── Create FileDocument ───────────────────────────────────────────
            var contentHash = ComputeHash(extractedText);
            var fileDoc = new FileDocument
                
            {
                FileName = file.FileName,
                OriginalFileName = file.FileName,
                FileType = ext,
                FileSizeBytes = file.Length,
                ContentHash = contentHash,
                Status = FileIndexingStatus.Processing,
                ChunkingStrategy = chunkingStrategy,
                ChunkSize = chunkSize,
                ChunkOverlap = chunkOverlap,
                StoragePath = storagePath
               
            };

            await _db.Files.InsertOneAsync(fileDoc, cancellationToken: cancellationToken);

            try
            {
                // ── Chunk the text ────────────────────────────────────────────
                var chunks = _chunkingService.Chunk(extractedText, chunkingStrategy, chunkSize, chunkOverlap);

                if (chunks.Count == 0)
                    throw new InvalidOperationException("Chunking produced zero chunks");

                _logger.LogInformation(
                    "Created {Count} chunks for file {Name} using {Strategy}",
                    chunks.Count, file.FileName, chunkingStrategy);

                // ── Generate embeddings ───────────────────────────────────────
                var embeddings = await _embeddingService
                    .GenerateEmbeddingsBatchAsync(chunks, cancellationToken);

                // ── Build chunk documents ─────────────────────────────────────
                var position = 0;
                var chunkDocs = chunks.Select((text, idx) =>
                {
                    var start = position;
                    position += text.Length;
                    return new ChunkDocument
                    {
                        FileId = fileDoc.Id,
                        FileName = file.FileName,
                        Text = text,
                        ChunkIndex = idx,
                        StartPosition = start,
                        EndPosition = position,
                        TokenCount = EstimateTokenCount(text),
                        Embedding = embeddings[idx],
                        ChunkingStrategy = chunkingStrategy,
                        Metadata = new Dictionary<string, string>
                        {
                            ["fileType"] = ext,
                            ["strategy"] = chunkingStrategy
                        }
                    };
                }).ToList();

                await _db.Chunks.InsertManyAsync(chunkDocs, cancellationToken: cancellationToken);

                // ── Update file status ────────────────────────────────────────
                var update = Builders<AIChat.Models.FileDocument>.Update
                    .Set(f => f.Status, FileIndexingStatus.Indexed)
                    .Set(f => f.ChunkCount, chunks.Count)
                    .Set(f => f.IndexedAt, DateTime.UtcNow);

                await _db.Files.UpdateOneAsync(
                    f => f.Id == fileDoc.Id, update,
                    cancellationToken: cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Successfully indexed {File}: {Chunks} chunks in {Time}ms",
                    file.FileName, chunks.Count, stopwatch.ElapsedMilliseconds);

                return new IndexingResultVM
                {
                    Success = true,
                    FileId = fileDoc.Id,
                    FileName = file.FileName,
                    ChunksCreated = chunks.Count,
                    Strategy = chunkingStrategy,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Indexing failed for {File}", file.FileName);

                var failUpdate = Builders<FileDocument>.Update
                    .Set(f => f.Status, FileIndexingStatus.Failed)
                    .Set(f => f.StatusMessage, ex.Message);
                await _db.Files.UpdateOneAsync(f => f.Id == fileDoc.Id, failUpdate);

                return new IndexingResultVM
                {
                    Success = false,
                    FileId = fileDoc.Id,
                    FileName = file.FileName,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        public async Task<List<FileDocument>> GetAllFilesAsync()
        {
            return await _db.Files
                .Find(_ => true)
                .SortByDescending(f => f.UploadedAt)
                .ToListAsync();
        }

        public async Task<FileDocument?> GetFileByIdAsync(string fileId)
        {
            return await _db.Files.Find(f => f.Id == fileId).FirstOrDefaultAsync();
        }

        public async Task<bool> DeleteFileAsync(string fileId)
        {
            var file = await GetFileByIdAsync(fileId);
            if (file is null) return false;

            // Delete storage file
            if (File.Exists(file.StoragePath))
                File.Delete(file.StoragePath);

            // Delete all chunks
            await _db.Chunks.DeleteManyAsync(c => c.FileId == fileId);

            // Delete file document
            var result = await _db.Files.DeleteOneAsync(f => f.Id == fileId);
            return result.DeletedCount > 0;
        }

        public async Task<IndexingResultVM> ReIndexFileAsync(
            string fileId, string chunkingStrategy, int chunkSize, int chunkOverlap)
        {
            var file = await GetFileByIdAsync(fileId);
            if (file is null)
                return new IndexingResultVM { Success = false, ErrorMessage = "File not found" };

            // Delete existing chunks
            await _db.Chunks.DeleteManyAsync(c => c.FileId == fileId);

            // Update status
            var update = Builders<FileDocument>.Update
                .Set(f => f.Status, FileIndexingStatus.ReIndexing)
                .Set(f => f.ChunkingStrategy, chunkingStrategy)
                .Set(f => f.ChunkSize, chunkSize)
                .Set(f => f.ChunkOverlap, chunkOverlap);
            await _db.Files.UpdateOneAsync(f => f.Id == fileId, update);

            // Re-extract and re-index
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var text = await ExtractTextFromPathAsync(file.StoragePath, ext);
            var chunks = _chunkingService.Chunk(text, chunkingStrategy, chunkSize, chunkOverlap);
            var embeddings = await _embeddingService.GenerateEmbeddingsBatchAsync(chunks);

            var position = 0;
            var chunkDocs = chunks.Select((t, idx) =>
            {
                var start = position;
                position += t.Length;
                return new ChunkDocument
                {
                    FileId = fileId,
                    FileName = file.FileName,
                    Text = t,
                    ChunkIndex = idx,
                    StartPosition = start,
                    EndPosition = position,
                    TokenCount = EstimateTokenCount(t),
                    Embedding = embeddings[idx],
                    ChunkingStrategy = chunkingStrategy
                };
            }).ToList();

            await _db.Chunks.InsertManyAsync(chunkDocs);

            var finalUpdate = Builders<FileDocument>.Update
                .Set(f => f.Status, FileIndexingStatus.Indexed)
                .Set(f => f.ChunkCount, chunks.Count)
                .Set(f => f.IndexedAt, DateTime.UtcNow);
            await _db.Files.UpdateOneAsync(f => f.Id == fileId, finalUpdate);

            return new IndexingResultVM
            {
                Success = true,
                FileId = fileId,
                FileName = file.FileName,
                ChunksCreated = chunks.Count,
                Strategy = chunkingStrategy
            };
        }

        public async Task<string> ExtractTextFromFileAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var tempPath = Path.GetTempFileName() + ext;

            try
            {
                await using var stream = new FileStream(tempPath, FileMode.Create);
                await file.CopyToAsync(stream);
                return await ExtractTextFromPathAsync(tempPath, ext);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        // ─── Private Helpers ───────────────────────────────────────────────────

        private void ValidateFile(IFormFile file)
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("No file provided");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!_uploadSettings.AllowedExtensions.Contains(ext))
                throw new InvalidOperationException(
                    $"File type '{ext}' not allowed. Supported: {string.Join(", ", _uploadSettings.AllowedExtensions)}");

            var maxBytes = _uploadSettings.MaxFileSizeMb * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new InvalidOperationException(
                    $"File size {file.Length / 1024 / 1024}MB exceeds maximum {_uploadSettings.MaxFileSizeMb}MB");
        }

        private static async Task<string> ExtractTextFromPathAsync(string path, string ext)
        {
            return ext switch
            {
                ".pdf" => ExtractPdf(path),
                ".txt" => await File.ReadAllTextAsync(path, Encoding.UTF8),
                ".docx" => ExtractDocx(path),
                _ => throw new NotSupportedException($"Unsupported file type: {ext}")
            };
        }

        private static string ExtractPdf(string path)
        {
            var sb = new StringBuilder();
            using var doc = PdfDocument.Open(path);

            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);

            return sb.ToString();
        }

        private static string ExtractDocx(string path)
        {
            var sb = new StringBuilder();

            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart?.Document?.Body;

            if (body is null) return string.Empty;

            foreach (var para in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                sb.AppendLine(para.InnerText);

            return sb.ToString();
        }

        private static string ComputeHash(string text)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes);
        }

        private static int EstimateTokenCount(string text)
        {
            // Rough estimate: 1 token ≈ 4 characters (OpenAI approximation)
            return text.Length / 4;
        }

    
    }
}
