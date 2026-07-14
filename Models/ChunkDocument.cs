using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AIChat.Models
{
    public class ChunkDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("fileId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string FileId { get; set; } = string.Empty;

        [BsonElement("fileName")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("text")]
        public string Text { get; set; } = string.Empty;

        [BsonElement("chunkIndex")]
        public int ChunkIndex { get; set; }

        [BsonElement("startPosition")]
        public int StartPosition { get; set; }

        [BsonElement("endPosition")]
        public int EndPosition { get; set; }

        [BsonElement("tokenCount")]
        public int TokenCount { get; set; }

        [BsonElement("embedding")]
        public float[] Embedding { get; set; } = [];

        [BsonElement("chunkingStrategy")]
        public string ChunkingStrategy { get; set; } = string.Empty;

        [BsonElement("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
