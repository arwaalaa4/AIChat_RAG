namespace AIChat.Settings.MongoDB;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string FilesCollectionName { get; set; } = "files";
    public string ChunksCollectionName { get; set; } = "chunks";
}