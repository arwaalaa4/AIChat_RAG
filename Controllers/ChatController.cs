using Microsoft.AspNetCore.Mvc;
using AIChat.Models;
using AIChat.ViewModels.File;
using AIChat.ViewModels.Chat;
using AIChat.Services.Interfaces;


namespace AIChat.Controllers
{
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly IFilesService _fileService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            IFilesService fileService,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _fileService = fileService;
            _logger = logger;
        }
        [HttpGet]
        public async Task<IActionResult> Index(string? sessionId = null)
        {
            var files = await _fileService.GetAllFilesAsync();

            var indexedFiles = files
                .Where(f => f.Status == FileIndexingStatus.Indexed)
                .ToList();

            var vm = new ChatViewModel
            {
                SessionId = sessionId ?? Guid.NewGuid().ToString(),
                AvailableFiles = indexedFiles.Select(f => new FileSelectionItem
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    ChunkCount = f.ChunkCount,
                    IsSelected = true // Select all by default
                }).ToList()
            };

            return View(vm);
        }

        // POST /Chat/Ask  (AJAX endpoint)
        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] Request request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { error = "Question cannot be empty" });

            try
            {
                _logger.LogInformation(
                    "Chat ask: session={Session}, fileIds={Files}, q={Q}",
                    request.SessionId,
                    string.Join(",", request.FileIds ?? []),
                    request.Question[..Math.Min(80, request.Question.Length)]);

                var result = await _chatService.AskAsync(
                    question: request.Question,
                    sessionId: request.SessionId,
                    fileIds: request.FileIds?.Count > 0 ? request.FileIds : null,
                    ct: ct);

                return Json(new
                {
                    success = true,
                    role = result.Role,
                    content = result.Content,
                    sources = result.Sources.Select(s => new
                    {
                        fileName = s.FileName,
                        preview = s.ChunkPreview,
                        score = Math.Round(s.SimilarityScore, 4)
                    }),
                    timestamp = result.Timestamp.ToString("O")
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new { error = "Request cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat Ask failed for session {Session}", request.SessionId);
                return StatusCode(500, new { error = "Failed to generate response. Please try again." });
            }
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
