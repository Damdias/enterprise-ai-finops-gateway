var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("ai-router", client =>
{
    var routerUrl = builder.Configuration["Router:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(routerUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.UseHttpsRedirection();
app.MapJobEndpoints();

app.MapGet("/health", () =>
{
    return Results.Ok(
      new
      {
          service = "gateway-dotnet",
          status = "healthy"
      }
    );
});

app.Map("/api/router/health", async (IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var client = httpClientFactory.CreateClient("ai-router");
    try
    {
        using var response = await client.GetAsync("/health", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Problem(
                title: "AI Router return an error",
                statusCode: 502);
        }
        var result = await response.Content.ReadFromJsonAsync<RouterHealth>(cancellationToken);
        return result is null ?
        Results.Problem(title: "AI Router returned an empty response",
            statusCode: 502)
            : Results.Ok(result);

    }
    catch (HttpRequestException)
    {
        return Results.Problem(title: "AI Router timed out",
        statusCode: 504);

    }

});
app.Run();


public record RouterHealth(string Service, string Status);

