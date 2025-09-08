using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MicroShop.Services.Ordering.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<CheckoutRequestedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "rabbitmq";
        var user = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
        var pass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

        cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
        cfg.ConfigureEndpoints(ctx); // создаст очередь для консюмера
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ordering-service"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddOtlpExporter());

var app = builder.Build();
app.MapGet("/", () => "ordering up");
app.Run();
