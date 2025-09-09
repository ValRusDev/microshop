using Microsoft.Extensions.Diagnostics.HealthChecks;

public class RabbitMqHealthCheck(IConfiguration cfg) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var host = cfg["RabbitMQ:Host"] ?? "rabbitmq";
            var user = cfg["RabbitMQ:Username"] ?? "guest";
            var pass = cfg["RabbitMQ:Password"] ?? "guest";

            var factory = new global::RabbitMQ.Client.ConnectionFactory
            {
                HostName = host,
                UserName = user,
                Password = pass,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(2)
            };

            using var conn = await factory.CreateConnectionAsync($"{Environment.MachineName}-hc", ct);
            using var ch = await conn.CreateChannelAsync();

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ unreachable", ex);
        }
    }
}
