using AIChat.ViewModels.File;
namespace AIChat.ViewModels.Chat
  
{
  
    public class ChatViewModel
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public List<ChatMessageViewModel> Messages { get; set; } = [];
        public List<FileSelectionItem> AvailableFiles { get; set; } = [];
        public List<string> SelectedFileIds { get; set; } = [];
    }
}
