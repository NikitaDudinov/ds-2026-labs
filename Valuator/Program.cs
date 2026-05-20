using RabbitMQ.Client;
using StackExchange.Redis;
using Valuator.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;

namespace Valuator;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        string mainPassword = Environment.GetEnvironmentVariable("REDIS_MAIN_PASSWORD") ?? "";
        string ruPassword = Environment.GetEnvironmentVariable("REDIS_RU_PASSWORD") ?? "";
        string euPassword = Environment.GetEnvironmentVariable("REDIS_EU_PASSWORD") ?? "";
        string asiaPassword = Environment.GetEnvironmentVariable("REDIS_ASIA_PASSWORD") ?? "";

        string mainUrl = Environment.GetEnvironmentVariable("DB_MAIN") ?? "localhost:6379";
        string ruUrl = Environment.GetEnvironmentVariable("DB_RU") ?? "localhost:6379";
        string euUrl = Environment.GetEnvironmentVariable("DB_EU") ?? "localhost:6379";
        string asiaUrl = Environment.GetEnvironmentVariable("DB_ASIA") ?? "localhost:6379";

        var mainConn = await ConnectionMultiplexer.ConnectAsync($"{mainUrl},password={mainPassword}");
        var ruConn = await ConnectionMultiplexer.ConnectAsync($"{ruUrl},password={ruPassword}");
        var euConn = await ConnectionMultiplexer.ConnectAsync($"{euUrl},password={euPassword}");
        var asiaConn = await ConnectionMultiplexer.ConnectAsync($"{asiaUrl},password={asiaPassword}");

        builder.Services.AddSingleton<IConnectionMultiplexer>(mainConn);

        builder.Services.AddSingleton<IEvaluationStorage>(sp =>
            new RedisEvaluationStorage(mainConn, ruConn, euConn, asiaConn));

        var rabbitFactory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RABBIT_HOST") ?? "valuator-rabbitmq",
            UserName = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "guest",
            Password = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest"
        };
        var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
        
        using (var initChannel = await rabbitConnection.CreateChannelAsync())
        {
            await initChannel.ExchangeDeclareAsync("calculate.text.rank", ExchangeType.Fanout, true);
            await initChannel.ExchangeDeclareAsync("similarity.calculated", ExchangeType.Fanout, true);
        }

        builder.Services.AddSingleton<IConnection>(rabbitConnection);

        builder.Services.AddScoped<IEvaluationStorage, RedisEvaluationStorage>();
        builder.Services.AddScoped<ITextValuationService, TextValuationService>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        builder.Services.AddDataProtection()
         .SetApplicationName("ValuatorApp");

        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
        });

        var app = builder.Build();

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();

        app.Run();
    }
}