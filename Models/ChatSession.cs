using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AIChat.Models
{
    public class ChatSession
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [BsonElement("messages")]
        public List<ChatMessage> Messages { get; set; } = [];

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ChatMessage
    {
        [BsonElement("role")]
        public string Role { get; set; } = string.Empty; // "user" or "assistant"

        [BsonElement("content")]
        public string Content { get; set; } = string.Empty;

        [BsonElement("sourceChunks")]
        public List<string> SourceChunks { get; set; } = [];

        [BsonElement("sourceFiles")]
        public List<string> SourceFiles { get; set; } = [];

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
