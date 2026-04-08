using StackExchange.Redis;

namespace Valuator.Services;

public interface IEvaluationStorage
{
    Task SaveTextAsync(string id, string text);
    Task SaveRankAsync(string id, double rank);
    Task SaveSimilarityAsync(string id, double similarity);
    Task<string?> GetTextAsync(string id);
    Task<double?> GetRankAsync(string id);
    Task<double?> GetSimilarityAsync(string id);
    Task<bool> IsTextUniqueAsync(string text);
    Task AddToUniqueSetAsync(string text);
}

public class RedisEvaluationStorage : IEvaluationStorage
{
    private readonly IDatabase _redis;
    private const string AllTextsKey = "ALL_TEXTS";

    public RedisEvaluationStorage(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    private string GetKey(string prefix, string id) => $"{prefix}-{id}";

    public Task SaveTextAsync(string id, string text) => 
        _redis.StringSetAsync(GetKey("TEXT", id), text);

    public Task SaveRankAsync(string id, double rank) => 
        _redis.StringSetAsync(GetKey("RANK", id), rank);

    public Task SaveSimilarityAsync(string id, double similarity) => 
        _redis.StringSetAsync(GetKey("SIMILARITY", id), similarity);

    public async Task<string?> GetTextAsync(string id) => 
        await _redis.StringGetAsync(GetKey("TEXT", id));

    public async Task<double?> GetRankAsync(string id)
    {
        var value = await _redis.StringGetAsync(GetKey("RANK", id));
        return value.HasValue ? (double)value : null;
    }

    public async Task<double?> GetSimilarityAsync(string id)
    {
        var value = await _redis.StringGetAsync(GetKey("SIMILARITY", id));
        return value.HasValue ? (double)value : null;
    }

    public async Task<bool> IsTextUniqueAsync(string text) => 
        !await _redis.SetContainsAsync(AllTextsKey, text);

    public Task AddToUniqueSetAsync(string text) => 
        _redis.SetAddAsync(AllTextsKey, text);
}