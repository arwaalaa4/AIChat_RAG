namespace AIChat.Models
{
    public class Request
    {
        public string Question { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public List<string>? FileIds { get; set; }
    }
}
