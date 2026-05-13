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

        var redis = await ConnectionMultiplexer.ConnectAsync("valuator-redis:6379");
        var db = redis.GetDatabase();

        var factory = new ConnectionFactory { HostName = "valuator-rabbitmq" };
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
                Console.WriteLine($"Processing will take {delay.TotalSeconds} seconds...");
                await Task.Delay(delay);

                string textKey = $"TEXT-{id}";
                string? text = await db.StringGetAsync(textKey);

                if (!string.IsNullOrEmpty(text))
                {
                    double rank = CalculateRank(text);
                    string rankKey = $"RANK-{id}";
                    await db.StringSetAsync(rankKey, rank);
                    
                    Console.WriteLine($"Calculated and saved Rank: {rank} for ID: {id}");

                    var rankEvent = new RankCalculatedEvent(id, rank);
                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rankEvent));
                    await channel.BasicPublishAsync(
                        exchange: RankCalculatedExchange,
                        routingKey: "",
                        body: body);

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