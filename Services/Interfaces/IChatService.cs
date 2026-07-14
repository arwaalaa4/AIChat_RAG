using AIChat.ViewModels.Chat;

namespace AIChat.Services.Interfaces
{
    public interface IChatService
    {
        Task<ChatMessageViewModel> AskAsync(
       string question,
       string sessionId,
       List<string>? fileIds = null,
       CancellationToken ct = default);
    }
}
