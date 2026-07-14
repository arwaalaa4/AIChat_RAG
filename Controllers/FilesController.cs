using AIChat.Models;
using AIChat.Services.Implementations;
using AIChat.Services.Interfaces;
using AIChat.Settings.Extensions;
using AIChat.ViewModels.Chat;
using AIChat.ViewModels.File;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIChat.Controllers
{
    public class FileController : Controller
    {
        private readonly IFilesService _fileService;
        private readonly FileUploadSettings _uploadSettings;
        private readonly ILogger<FileController> _logger;

        public FileController(
            IFilesService fileService,
            IOptions<FileUploadSettings> uploadSettings,
            ILogger<FileController> logger)
        {
            _fileService = fileService;
            _uploadSettings = uploadSettings.Value;
            _logger = logger;
        }

        // GET /File
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var files = await _fileService.GetAllFilesAsync();

            var vm = new FileListViewModel
            {
                Files = files,
                TotalFiles = files.Count,
                TotalChunks = files.Sum(f => f.ChunkCount),
                SuccessMessage = TempData["SuccessMessage"] as string,
                ErrorMessage = TempData["ErrorMessage"] as string
            };

            return View(vm);
        }

        // GET /File/Upload
        [HttpGet]
        public IActionResult Upload()
        {
            var vm = new FileUploadViewModel
            {
                ChunkingStrategy = "FixedSize",
                ChunkSize = 1000,
                ChunkOverlap = 100
            };
            return View(vm);
        }

        // POST /File/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(100_000_000)] // 100MB max request size
        public async Task<IActionResult> Upload(FileUploadViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.File is null || model.File.Length == 0)
            {
                ModelState.AddModelError("File", "Please select a file to upload.");
                return View(model);
            }

            try
            {
                _logger.LogInformation(
                    "Upload request: {File}, strategy={Strategy}, chunkSize={Size}, overlap={Overlap}",
                    model.File.FileName, model.ChunkingStrategy, model.ChunkSize, model.ChunkOverlap);

                var result = await _fileService.UploadAndIndexAsync(
                    file: model.File,
                    chunkingStrategy: model.ChunkingStrategy,
                    chunkSize: model.ChunkSize,
                    chunkOverlap: model.ChunkOverlap,
                    cancellationToken: cancellationToken);

                if (result.Success)
                {
                    TempData["SuccessMessage"] =
                        $"✅ '{result.FileName}' uploaded and indexed successfully! " +
                        $"{result.ChunksCreated} chunks created using {result.Strategy} strategy " +
                        $"in {result.ProcessingTime.TotalSeconds:F1}s.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, $"Indexing failed: {result.ErrorMessage}");
                    return View(model);
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file upload");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return View(model);
            }
        }

        // GET /File/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("File ID is required");

            var file = await _fileService.GetFileByIdAsync(id);
            if (file is null)
                return NotFound($"File with ID '{id}' not found.");

            return View(file);
        }

        // POST /File/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("File ID is required");

            var deleted = await _fileService.DeleteFileAsync(id);

            if (deleted)
                TempData["SuccessMessage"] = "🗑️ File and all associated chunks deleted successfully.";
            else
                TempData["ErrorMessage"] = "❌ File not found or could not be deleted.";

            return RedirectToAction(nameof(Index));
        }

        // GET /File/ReIndex/{id}
        [HttpGet]
        public async Task<IActionResult> ReIndex(string id)
        {
            var file = await _fileService.GetFileByIdAsync(id);
            if (file is null) return NotFound();

            var vm = new FileUploadViewModel
            {
                ChunkingStrategy = file.ChunkingStrategy,
                ChunkSize = file.ChunkSize,
                ChunkOverlap = file.ChunkOverlap
            };

            ViewBag.FileId = id;
            ViewBag.FileName = file.FileName;
            return View(vm);
        }

        // POST /File/ReIndex/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReIndex(string id, FileUploadViewModel model)
        {
            try
            {
                var result = await _fileService.ReIndexFileAsync(
                    id,
                    model.ChunkingStrategy,
                    model.ChunkSize,
                    model.ChunkOverlap);

                if (result.Success)
                {
                    TempData["SuccessMessage"] =
                        $"♻️ '{result.FileName}' re-indexed successfully! " +
                        $"{result.ChunksCreated} chunks using {result.Strategy} strategy.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Re-indexing failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReIndex failed for file {Id}", id);
                TempData["ErrorMessage"] = $"Re-indexing error: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // AJAX: POST /File/GetStatus/{id}
        [HttpGet]
        public async Task<IActionResult> GetStatus(string id)
        {
            var file = await _fileService.GetFileByIdAsync(id);
            if (file is null) return NotFound();

            return Json(new
            {
                id = file.Id,
                status = file.Status.ToString(),
                chunkCount = file.ChunkCount,
                indexedAt = file.IndexedAt?.ToString("O")
            });
        }

        //public IActionResult Index()
        //{
        //    return View();
        //}
    }
}
