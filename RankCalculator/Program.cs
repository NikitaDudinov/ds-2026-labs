using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace RankCalculator;

class Program
{
    private const string ExchangeName = "calculate.text.rank";
    private const string QueueName = "valuator.processing.rank";
    private const string RankCalculatedExchange = "rank.calculated";
    private static readonly HttpClient HttpClient = new();

    public static async Task Main(string[] args)
    {
        Console.WriteLine("RankCalculator Worker started");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("apikey", "my_super_secret_api_key");

        string mainPassword = Environment.GetEnvironmentVariable("REDIS_MAIN_PASSWORD") ?? "";
        string ruPassword = Environment.GetEnvironmentVariable("REDIS_RU_PASSWORD") ?? "";
        string euPassword = Environment.GetEnvironmentVariable("REDIS_EU_PASSWORD") ?? "";
        string asiaPassword = Environment.GetEnvironmentVariable("REDIS_ASIA_PASSWORD") ?? "";

        string mainUrl = Environment.GetEnvironmentVariable("DB_MAIN") ?? "localhost:6379";
        string ruUrl = Environment.GetEnvironmentVariable("DB_RU") ?? "localhost:6379";
        string euUrl = Environment.GetEnvironmentVariable("DB_EU") ?? "localhost:6379";
        string asiaUrl = Environment.GetEnvironmentVariable("DB_ASIA") ?? "localhost:6379";

        var mainDb = await ConnectionMultiplexer.ConnectAsync($"{mainUrl},password={mainPassword}");
        var ruDb = await ConnectionMultiplexer.ConnectAsync($"{ruUrl},password={ruPassword}");
        var euDb = await ConnectionMultiplexer.ConnectAsync($"{euUrl},password={euPassword}");
        var asiaDb = await ConnectionMultiplexer.ConnectAsync($"{asiaUrl},password={asiaPassword}");

        var dbMain = mainDb.GetDatabase();
        var dbs = new Dictionary<string, IDatabase>
        {
            { "RU", ruDb.GetDatabase() },
            { "EU", euDb.GetDatabase() },
            { "ASIA", asiaDb.GetDatabase() }
        };

        var factory = new ConnectionFactory
        {
             HostName = Environment.GetEnvironmentVariable("RABBIT_HOST") ?? "valuator-rabbitmq",
             UserName = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "guest",
             Password = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest"
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: ExchangeName, type: ExchangeType.Fanout, durable: true);
        await channel.ExchangeDeclareAsync(exchange: RankCalculatedExchange, type: ExchangeType.Fanout, durable: true);

        await channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: "");
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                string id = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                Console.WriteLine($"Received task for ID: {id}");

                var delay = TimeSpan.FromSeconds(new Random().Next(3, 15));
                await Task.Delay(delay);

                var regionRedisVal = await dbMain.StringGetAsync($"SHARD-{id}");
                string region = regionRedisVal.ToString();

                Console.WriteLine($"LOOKUP: {id}, {region}");

                if (!dbs.TryGetValue(region, out var shardDb))
                {
                    throw new Exception($"Unknown shard region: {region}");
                }

                string textKey = $"TEXT-{id}";
                string? text = await shardDb.StringGetAsync(textKey);

                if (!string.IsNullOrEmpty(text))
                {
                    double rank = CalculateRank(text);
                    string rankKey = $"RANK-{id}";
                    await shardDb.StringSetAsync(rankKey, rank);
                    Console.WriteLine($"Calculated and saved Rank: {rank} for ID: {id} in shard {region}");

                    var rankEvent = new RankCalculatedEvent(id, rank);
                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rankEvent));
                    await channel.BasicPublishAsync(exchange: RankCalculatedExchange, routingKey: "", body: body);

                    await PublishToCentrifugoAsync(id, rank);
                }

                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer);
        await Task.Delay(Timeout.Infinite);
    }

    private static double CalculateRank(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0.0;
        double nonLetters = text.Count(c => !char.IsLetter(c));
        return (double)nonLetters / text.Length;
    }

    private static async Task PublishToCentrifugoAsync(string id, double rank)
    {
        try
        {
            var payload = new
            {
                method = "publish",
                @params = new
                {
                    channel = $"summary-{id}",
                    data = new { id, rank }
                }
            };

            var response = await HttpClient.PostAsJsonAsync("http://centrifugo:8000/api", payload);
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"Successfully pushed update to browser for ID: {id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to push update: {ex.Message}");
        }
    }
}

public record RankCalculatedEvent(string Id, double Rank);