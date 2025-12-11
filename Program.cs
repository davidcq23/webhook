using System.Text.Json;
using DidiWebhookReceiver.Models;
using DidiWebhookReceiver.Services;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddSingleton<WebhookLogService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();

var app = builder.Build();

// Swagger solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Archivos estáticos (monitor)
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoint de salud
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// Endpoint para obtener logs
app.MapGet("/api/logs", (WebhookLogService logService) =>
{
    var logs = logService.GetAll();
    return Results.Ok(logs);
});

//limpia todo los logs de la cache
app.MapDelete("/api/logs", (WebhookLogService logService) =>
{
    logService.Clear();
    return Results.Ok(new { status = "logs cleared" });
});
// Endpoint del webhook DIDI
app.MapPost("/webhook-didi", async (HttpContext context, WebhookLogService logService, ILoggerFactory loggerFactory, IConfiguration configuration) =>
{
    var logger = loggerFactory.CreateLogger("Webhook");

    // 1. Validar apiKey en header
    if (!context.Request.Headers.TryGetValue("apiKey", out var apiKeyHeader))
    {
        logger.LogWarning("Solicitud sin apiKey");
        return Results.Unauthorized();
    }

    var expectedApiKey = configuration["DidiWebhook:ApiKey"];
    if (string.IsNullOrWhiteSpace(expectedApiKey))
    {
        logger.LogError("DidiWebhook:ApiKey no está configurado.");
        return Results.Problem("Server misconfiguration", statusCode: StatusCodes.Status500InternalServerError);
    }

    if (!string.Equals(apiKeyHeader.ToString(), expectedApiKey, StringComparison.Ordinal))
    {
        logger.LogWarning("apiKey inválida: {Provided}", apiKeyHeader.ToString());
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // 2. Leer el body JSON
    string body;
    using (var reader = new StreamReader(context.Request.Body))
    {
        body = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrWhiteSpace(body))
    {
        logger.LogWarning("Body vacío en webhook");
        return Results.BadRequest("Empty body");
    }

    WebhookNotification? payload = null;

    try
    {
        payload = JsonSerializer.Deserialize<WebhookNotification>(
            body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deserializando el payload: {Body}", body);
        // Seguimos guardando el raw para inspección
    }

    // 3. Guardar en el monitor (memoria)
    logService.Add(new WebhookLogEntry
    {
        ReceivedAt = DateTime.UtcNow,
        RawBody = body,
        Parsed = payload
    });

    logger.LogInformation("Webhook recibido y almacenado. IdTransaction: {IdTransaction}", payload?.IdTransaction);

    // 4. Responder 200 OK
    return Results.Ok(new { status = "ok" });
});

app.Run();
