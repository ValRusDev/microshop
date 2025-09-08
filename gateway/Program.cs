using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddOpenTelemetry()
  .ConfigureResource(r => r.AddService("gateway"))
  .WithTracing(t => t.AddAspNetCoreInstrumentation()
                     .AddHttpClientInstrumentation()
                     .AddOtlpExporter());

var app = builder.Build();
app.MapReverseProxy();
app.Run();
