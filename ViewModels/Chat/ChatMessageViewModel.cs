using AIChat.ViewModels.File;
namespace AIChat.ViewModels.Chat
{

    public class ChatMessageViewModel
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<SourceReference> Sources { get; set; } = [];
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

}
