using MongoDB.Driver;
using Microsoft.Extensions.Options;
using AIChat.Models;
using AIChat.ViewModels;
using System.Security.Authentication;

namespace AIChat.Settings.MongoDB;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var mongoSettings = MongoClientSettings
            .FromConnectionString(settings.Value.ConnectionString);

        // ← Add these lines to fix Windows TLS issues
        mongoSettings.SslSettings = new SslSettings
        {
            EnabledSslProtocols = SslProtocols.Tls12
        };
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

        var client = new MongoClient(mongoSettings);
        _database = client.GetDatabase(settings.Value.DatabaseName);

        EnsureIndexes(settings.Value);
    }

    public IMongoCollection<FileDocument> Files =>
        _database.GetCollection<FileDocument>("files");

    public IMongoCollection<ChunkDocument> Chunks =>
        _database.GetCollection<ChunkDocument>("chunks");

    private void EnsureIndexes(MongoDbSettings settings)
    {
        var fileIndexKeys = Builders<FileDocument>.IndexKeys
            .Ascending(f => f.FileName)
            .Ascending(f => f.Status);
        Files.Indexes.CreateOneAsync(
            new CreateIndexModel<FileDocument>(fileIndexKeys));

        var chunkFileIdIndex = Builders<ChunkDocument>.IndexKeys
            .Ascending(c => c.FileId);
        Chunks.Indexes.CreateOneAsync(
            new CreateIndexModel<ChunkDocument>(chunkFileIdIndex));

        var chunkTextIndex = Builders<ChunkDocument>.IndexKeys
            .Text(c => c.Text);
        Chunks.Indexes.CreateOneAsync(
            new CreateIndexModel<ChunkDocument>(chunkTextIndex));
    }
}