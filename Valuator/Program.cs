using StackExchange.Redis;
using Valuator.Services;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        string redisConnection = "valuator-redis:6379";
        var redis = ConnectionMultiplexer.Connect(redisConnection);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

        builder.Services.AddScoped<IEvaluationStorage, RedisEvaluationStorage>();
        builder.Services.AddScoped<ITextMetricsCalculator, TextMetricsCalculator>();
        builder.Services.AddScoped<ITextValuationService, TextValuationService>();

        builder.Services.AddRazorPages();

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