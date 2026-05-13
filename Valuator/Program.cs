using RabbitMQ.Client;
using StackExchange.Redis;
using Valuator.Services;

namespace Valuator;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

       builder.Services.AddSingleton<IEvaluationStorage, RedisEvaluationStorage>();

        var rabbitFactory = new ConnectionFactory { HostName = "valuator-rabbitmq" };
        var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
        
        using (var initChannel = await rabbitConnection.CreateChannelAsync())
        {
            await initChannel.ExchangeDeclareAsync("calculate.text.rank", ExchangeType.Fanout, true);
            await initChannel.ExchangeDeclareAsync("similarity.calculated", ExchangeType.Fanout, true);
        }

        builder.Services.AddSingleton<IConnection>(rabbitConnection);

        builder.Services.AddScoped<IEvaluationStorage, RedisEvaluationStorage>();
        builder.Services.AddScoped<ITextValuationService, TextValuationService>();

        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();

        app.Run();
    }
}