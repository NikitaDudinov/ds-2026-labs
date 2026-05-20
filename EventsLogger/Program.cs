using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventsLogger;

class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("EventsLogger starting...");

        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RABBIT_HOST") ?? "valuator-rabbitmq",
            UserName = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "guest",
            Password = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var eventSubscriptions = new[] 
        { 
            "rank.calculated", 
            "similarity.calculated" 
        };

        foreach (var exchangeName in eventSubscriptions)
        {
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName, 
                type: ExchangeType.Fanout, 
                durable: true);

            var queueDeclareResult = await channel.QueueDeclareAsync(
                queue: "", 
                exclusive: true, 
                autoDelete: true);
            
            string queueName = queueDeclareResult.QueueName;

            await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: "");

            var consumer = new AsyncEventingBasicConsumer(channel);
            
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                if (exchangeName == "rank.calculated")
                {
                    var data = JsonSerializer.Deserialize<RankCalculatedEvent>(json);
                    Console.WriteLine($"[EVENT: Rank] ID: {data?.Id} | Value: {data?.Rank:F4}");
                }
                else if (exchangeName == "similarity.calculated")
                {
                    var data = JsonSerializer.Deserialize<SimilarityCalculatedEvent>(json);
                    Console.WriteLine($"[EVENT: Similarity] ID: {data?.Id} | Value: {data?.Similarity}");
                }

                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);
            Console.WriteLine($"[*] Subscribed to: {exchangeName}");
        }

        await Task.Delay(Timeout.Infinite);
    }
}

public record RankCalculatedEvent(string Id, double Rank);
public record SimilarityCalculatedEvent(string Id, double Similarity);