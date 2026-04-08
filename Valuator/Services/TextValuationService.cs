using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Valuator.Services;

public record TextEvaluationResult(string Id, string Text, double? Rank, double Similarity);
public record SimilarityCalculatedEvent(string Id, double Similarity);

public interface ITextValuationService
{
    Task<string> EvaluateAsync(string text);
    Task<TextEvaluationResult?> GetResultAsync(string id);
}

public class TextValuationService : ITextValuationService
{
    private readonly IEvaluationStorage _storage;
    private readonly IConnection _rabbitConnection; 
    private const string TaskExchange = "text.events"; 
    private const string SimilarityEventExchange = "similarity.calculated";

    public TextValuationService(IEvaluationStorage storage, IConnection rabbitConnection)
    {
        _storage = storage;
        _rabbitConnection = rabbitConnection;
    }

    public async Task<string> EvaluateAsync(string text)
    {
        string id = Guid.NewGuid().ToString();

        bool isUnique = await _storage.IsTextUniqueAsync(text);
        double similarity = isUnique ? 0.0 : 1.0;

        await _storage.SaveTextAsync(id, text);
        await _storage.SaveSimilarityAsync(id, similarity);
        if (isUnique)
        {
            await _storage.AddToUniqueSetAsync(text);
        }

        await using var channel = await _rabbitConnection.CreateChannelAsync();

        var simEvent = new SimilarityCalculatedEvent(id, similarity);
        await channel.BasicPublishAsync(
            exchange: SimilarityEventExchange, 
            routingKey: "", 
            body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(simEvent)));
 
        await channel.BasicPublishAsync(
            exchange: TaskExchange, 
            routingKey: "", 
            body: Encoding.UTF8.GetBytes(id));

        return id;
    }

    public async Task<TextEvaluationResult?> GetResultAsync(string id)
    {
        var text = await _storage.GetTextAsync(id);
        var rank = await _storage.GetRankAsync(id);
        var similarity = await _storage.GetSimilarityAsync(id);

        if (text == null || similarity == null) return null;

        return new TextEvaluationResult(id, text, rank, similarity.Value);
    }
}