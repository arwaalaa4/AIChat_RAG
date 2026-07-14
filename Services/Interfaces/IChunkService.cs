namespace AIChat.Services.Interfaces
{
    public interface IChunkService
    {
       
        List<string> ChunkFixed(string text, int chunkSize);

        List<string> ChunkWithOverlap(string text, int chunkSize, int overlapSize);

        List<string> Chunk(string text, string strategy, int chunkSize, int overlapSize);
    }
}
