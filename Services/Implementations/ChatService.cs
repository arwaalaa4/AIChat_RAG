using AIChat.Services.Interfaces;
using AIChat.ViewModels;
using AIChat.ViewModels.Chat;
using AIChat.ViewModels.File;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
namespace AIChat.Services.Implementations
{
    public class ChatService:IChatService
    {
        private readonly IEmbeddingsService _embeddingService;
        private readonly IVectorsService _vectorSearchService;
        private readonly Kernel _kernel;
        private readonly ILogger<ChatService> _logger;

        private const int MaxContextChunks = 5;
        private const int MaxContextCharacters = 6000;

        public ChatService(
            IEmbeddingsService embeddingService,
            IVectorsService vectorSearchService,
            Kernel kernel,
            ILogger<ChatService> logger)
        {
            _embeddingService = embeddingService;
            _vectorSearchService = vectorSearchService;
            _kernel = kernel;
            _logger = logger;
        }

        public async Task<ChatMessageViewModel> AskAsync(
            string question,
            string sessionId,
            List<string>? fileIds = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new ArgumentException("Question cannot be empty", nameof(question));

            _logger.LogInformation(
                "Processing question for session {Session}: '{Question}'",
                sessionId, question[..Math.Min(100, question.Length)]);

            // ── Step 1: Generate question embedding ───────────────────────────
            var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question, ct);

            // ── Step 2: Retrieve relevant chunks ──────────────────────────────
            var relevantChunks = await _vectorSearchService.SearchAsync(
                queryEmbedding: questionEmbedding,
                topK: MaxContextChunks,
                filterFileIds: fileIds,
                minSimilarityScore: 0.3);

            if (relevantChunks.Count == 0)
            {
                return new ChatMessageViewModel
                {
                    Role = "assistant",
                    Content = "I couldn't find any relevant information in the uploaded files to answer your question. " +
                              "Please make sure files have been uploaded and indexed, or try rephrasing your question.",
                    Sources = [],
                    Timestamp = DateTime.UtcNow
                };
            }

            // ── Step 3: Build RAG context ─────────────────────────────────────
            var contextBlock = BuildContextBlock(relevantChunks);

            // ── Step 4: Construct prompts ─────────────────────────────────────
            var systemPrompt = BuildSystemPrompt(contextBlock);

            _logger.LogDebug(
                "RAG context: {ChunkCount} chunks, {CharCount} chars, files: {Files}",
                relevantChunks.Count,
                contextBlock.Length,
                string.Join(", ", relevantChunks.Select(c => c.FileName).Distinct()));

            // ── Step 5: Call LLM via Semantic Kernel ──────────────────────────
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(question);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["temperature"] = 0.1,   // Low temperature for factual grounding
                    ["max_tokens"] = 1024
                }
            };

            ChatMessageContent response;
            try
            {
                response = await chatService.GetChatMessageContentAsync(
                    chatHistory, settings, _kernel, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM call failed for session {Session}", sessionId);
                var current = ex;
                while (current != null)
                {
                    _logger.LogError("{Type}: {Message}",
                        current.GetType().FullName,
                        current.Message);

                    current = current.InnerException;
                }

                
                throw;
            }

            // ── Step 6: Build response with citations ─────────────────────────
            var sources = relevantChunks.Select(c => new SourceReference
            {
                FileName = c.FileName,
                ChunkPreview = c.Text[..Math.Min(150, c.Text.Length)] + "…",
                SimilarityScore = c.SimilarityScore
            }).ToList();

            return new ChatMessageViewModel
            {
                Role = "assistant",
                Content = response.Content ?? "No response generated.",
                Sources = sources,
                Timestamp = DateTime.UtcNow
            };
        }

        // ─── Prompt Engineering ────────────────────────────────────────────────

        /// <summary>
        /// System prompt with strict grounding and anti-hallucination instructions.
        ///
        /// Key design decisions:
        /// 1. Explicit persona: document-focused assistant
        /// 2. Hard constraint: ONLY use provided context
        /// 3. Honest fallback: say "I don't know" vs. hallucinating
        /// 4. Citation instruction: attribute answers to sources
        /// 5. Structured context block clearly delimited
        /// </summary>
        private static string BuildSystemPrompt(string contextBlock)
        {
            return $"""
                You are an expert document analyst assistant. Your job is to answer questions 
                based EXCLUSIVELY on the document excerpts provided below. 

                STRICT RULES:
                1. ONLY use information from the provided context to answer the question.
                2. If the context does not contain enough information to answer, say:
                   "I don't have enough information in the uploaded documents to answer this."
                3. Do NOT invent, hallucinate, or use prior knowledge outside the context.
                4. When citing information, reference the source document name.
                5. Be concise, accurate, and helpful. Use markdown formatting in responses.
                6. If multiple documents are relevant, synthesize information across them.

                ═══════════════════════════════════════════════════════
                DOCUMENT CONTEXT (Retrieved by semantic similarity):
                ═══════════════════════════════════════════════════════
                {contextBlock}
                ═══════════════════════════════════════════════════════

                Now answer the user's question based solely on the context above.
                """;
        }

        private static string BuildContextBlock(List<SearchResultVM> chunks)
        {
            var sb = new System.Text.StringBuilder();
            var totalChars = 0;

            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var chunkText = chunk.Text;

                // Guard against exceeding context window
                if (totalChars + chunkText.Length > MaxContextCharacters)
                {
                    var remaining = MaxContextCharacters - totalChars;
                    if (remaining < 100) break;
                    chunkText = chunkText[..remaining] + "...";
                }

                sb.AppendLine($"[Source {i + 1}: {chunk.FileName} | Score: {chunk.SimilarityScore:F3}]");
                sb.AppendLine(chunkText);
                sb.AppendLine();

                totalChars += chunkText.Length;
            }

            return sb.ToString();
        }
    }
}
