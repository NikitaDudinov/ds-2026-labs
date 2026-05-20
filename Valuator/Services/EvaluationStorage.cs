using StackExchange.Redis;

namespace Valuator.Services;

public interface IEvaluationStorage
{
    Task SaveTextAsync(string id, string text, string country);
    Task SaveSimilarityAsync(string id, double similarity, string country);
    Task<bool> IsTextUniqueAsync(string text, string country);
    Task AddToUniqueSetAsync(string text, string country);

    Task<string?> GetTextAsync(string id);
    Task<double?> GetRankAsync(string id);
    Task<double?> GetSimilarityAsync(string id);

    Task SaveUserAsync(string username, string password);
    Task<string?> GetUserPasswordAsync(string username);
    Task SaveAuthorAsync(string textId, string username);
    Task<string?> GetAuthorAsync(string textId);
    Task<bool> UserExistsAsync(string username);
}

public class RedisEvaluationStorage : IEvaluationStorage
{
    private readonly IDatabase _mainDb;
    private readonly IDatabase _ruDb;
    private readonly IDatabase _euDb;
    private readonly IDatabase _asiaDb;
    private const string AllTextsKey = "ALL_TEXTS";

    public RedisEvaluationStorage(
        IConnectionMultiplexer mainConn,
        IConnectionMultiplexer ruConn,
        IConnectionMultiplexer euConn,
        IConnectionMultiplexer asiaConn)
    {
        _mainDb = mainConn.GetDatabase();
        _ruDb = ruConn.GetDatabase();
        _euDb = euConn.GetDatabase();
        _asiaDb = asiaConn.GetDatabase();
    }

    private string GetRegion(string country) => country switch
    {
        "Russia" => "RU",
        "France" or "Germany" => "EU",
        "UAE" or "India" => "ASIA",
        _ => "UNKNOWN"
    };

    private IDatabase GetDbByRegion(string region) => region switch
    {
        "RU" => _ruDb,
        "EU" => _euDb,
        "ASIA" => _asiaDb,
        _ => throw new Exception($"Unknown shard region: {region}")
    };

    private async Task<IDatabase> GetDbForIdAsync(string id)
    {
        var region = await _mainDb.StringGetAsync($"SHARD-{id}");
        Console.WriteLine($"LOOKUP: {id}, {region}");

        if (!region.HasValue) throw new Exception($"Shard mapping not found for ID: {id}");

        return GetDbByRegion(region.ToString());
    }

    private string GetKey(string prefix, string id) => $"{prefix}-{id}";

    public async Task SaveTextAsync(string id, string text, string country)
    {
        string region = GetRegion(country);

        await _mainDb.StringSetAsync($"SHARD-{id}", region);

        var shardDb = GetDbByRegion(region);
        await shardDb.StringSetAsync(GetKey("TEXT", id), text);
    }

    public async Task SaveSimilarityAsync(string id, double similarity, string country)
    {
        var shardDb = GetDbByRegion(GetRegion(country));
        await shardDb.StringSetAsync(GetKey("SIMILARITY", id), similarity);
    }

    public async Task<string?> GetTextAsync(string id)
    {
        var db = await GetDbForIdAsync(id);
        return await db.StringGetAsync(GetKey("TEXT", id));
    }

    public async Task<double?> GetRankAsync(string id)
    {
        var db = await GetDbForIdAsync(id);
        var value = await db.StringGetAsync(GetKey("RANK", id));
        return value.HasValue ? (double)value : null;
    }

    public async Task<double?> GetSimilarityAsync(string id)
    {
        var db = await GetDbForIdAsync(id);
        var value = await db.StringGetAsync(GetKey("SIMILARITY", id));
        return value.HasValue ? (double)value : null;
    }

    public async Task<bool> IsTextUniqueAsync(string text, string country)
    {
        var shardDb = GetDbByRegion(GetRegion(country));
        return !await shardDb.SetContainsAsync(AllTextsKey, text);
    }

    public async Task AddToUniqueSetAsync(string text, string country)
    {
        var shardDb = GetDbByRegion(GetRegion(country));
        await shardDb.SetAddAsync(AllTextsKey, text);
    }

    public async Task SaveUserAsync(string username, string password)
    {
        await _mainDb.StringSetAsync($"USER-{username}", password);
    }

    public async Task<string?> GetUserPasswordAsync(string username)
    {
        return await _mainDb.StringGetAsync($"USER-{username}");
    }

    public async Task SaveAuthorAsync(string textId, string username)
    {
        await _mainDb.StringSetAsync($"AUTHOR-{textId}", username);
    }

    public async Task<string?> GetAuthorAsync(string textId)
    {
        return await _mainDb.StringGetAsync($"AUTHOR-{textId}");
    }

    public async Task<bool> UserExistsAsync(string username)
    {
        return await _mainDb.KeyExistsAsync($"USER-{username}");
    }
}