using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace RankCalculator;

class Program
{
    private const string ExchangeName = "text.events";
    private const string QueueName = "valuator.processing.rank";
    private const string RankCalculatedExchange = "rank.calculated";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("RankCalculator Worker started");
        
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

                string textKey = $"TEXT-{id}";
                string? text = await db.StringGetAsync(textKey);

                if (!string.IsNullOrEmpty(text))
                {
                    double rank = CalculateRank(text);
                    string rankKey = $"RANK-{id}";
                    await db.StringSetAsync(rankKey, rank);
                    
                    Console.WriteLine($"Calculated and saved Rank: {rank} for ID: {id}");

                    var rankEvent = new RankCalculatedEvent(id, rank);
          
                    var jsonMessage = JsonSerializer.Serialize(rankEvent);
                    var body = Encoding.UTF8.GetBytes(jsonMessage);

                    await channel.BasicPublishAsync(
                        exchange: RankCalculatedExchange, 
                        routingKey: "", 
                        body: body);
                    
                    Console.WriteLine($"[!] Event RankCalculated published for ID: {id}");
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
}

public record RankCalculatedEvent(string Id, double Rank);