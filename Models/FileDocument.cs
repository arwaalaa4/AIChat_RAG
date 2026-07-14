using static System.Runtime.InteropServices.JavaScript.JSType;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AIChat.Models
{
    public class FileDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("fileName")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("originalFileName")]
        public string OriginalFileName { get; set; } = string.Empty;

        [BsonElement("fileType")]
        public string FileType { get; set; } = string.Empty;

        [BsonElement("fileSizeBytes")]
        public long FileSizeBytes { get; set; }

        [BsonElement("contentHash")]
        public string ContentHash { get; set; } = string.Empty;

        [BsonElement("chunkCount")]
        public int ChunkCount { get; set; }

        [BsonElement("status")]
        public FileIndexingStatus Status { get; set; } = FileIndexingStatus.Pending;

        [BsonElement("statusMessage")]
        public string? StatusMessage { get; set; }

        [BsonElement("chunkingStrategy")]
        public string ChunkingStrategy { get; set; } = string.Empty;

        [BsonElement("chunkSize")]
        public int ChunkSize { get; set; }

        [BsonElement("chunkOverlap")]
        public int ChunkOverlap { get; set; }

        [BsonElement("uploadedAt")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("indexedAt")]
        public DateTime? IndexedAt { get; set; }

        [BsonElement("storagePath")]
        public string StoragePath { get; set; } = string.Empty;
    }

    public enum FileIndexingStatus
    {
        Pending = 0,
        Processing = 1,
        Indexed = 2,
        Failed = 3,
        ReIndexing = 4
    }

}
