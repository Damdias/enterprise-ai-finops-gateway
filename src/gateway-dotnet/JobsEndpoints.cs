
using System.Text.Json;
using Npgsql;
using RabbitMQ.Client;

public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jobs", CreateJob);
        app.MapGet("/api/jobs/{id:guid}", GetJob);

        return app;
    }

    private static async Task<IResult> CreateJob(
        CreateJobRequest request,
        IConfiguration config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Filename) ||
            request.Filename.Length > 255)
        {
            return Results.BadRequest(
                new { error = "A filename of 1-255 characters is required." });
        }

        var jobId = Guid.NewGuid();

        var connectionString =
            config.GetConnectionString("Postgres")!;

        await using var db = new NpgsqlConnection(
            connectionString);

        await db.OpenAsync(ct);

        await using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO document_jobs (id, filename, status)
            VALUES (@id, @filename, 'queued')
            """, db))
        {
            cmd.Parameters.AddWithValue("id", jobId);
            cmd.Parameters.AddWithValue(
                "filename", request.Filename);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"]!,
            UserName = config["RabbitMq:Username"]!,
            Password = config["RabbitMq:Password"]!
        };

        await using var rabbit =
            await factory.CreateConnectionAsync();

        await using var channel =
            await rabbit.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: "document_jobs",
            durable: true,
            exclusive: false,
            autoDelete: false);

        var message = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                id = jobId,
                filename = request.Filename
            });

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: "document_jobs",
            mandatory: true,
            basicProperties: properties,
            body: message);

        return Results.Accepted(
            $"/api/jobs/{jobId}",
            new
            {
                id = jobId,
                status = "queued"
            });
    }

    private static async Task<IResult> GetJob(
        Guid id,
        IConfiguration config,
        CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(
            config.GetConnectionString("Postgres")!);

        await db.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT status
            FROM document_jobs
            WHERE id = @id
            """, db);

        cmd.Parameters.AddWithValue("id", id);

        var status = await cmd.ExecuteScalarAsync(ct);

        if (status is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            id,
            status = status.ToString()
        });
    }
}

public record CreateJobRequest(string Filename);
