using InvoiceAI.Api.Services;
using Microsoft.Extensions.AI;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["OpenAI:ApiKey"]
             ?? throw new InvalidOperationException("OpenAI:ApiKey tapılmadı");
var model = builder.Configuration["OpenAI:Model"]
            ?? throw new InvalidOperationException("OpenAI:Model tapılmadı");

builder.Services.AddChatClient(
    new OpenAIClient(apiKey).GetChatClient(model).AsIChatClient());

builder.Services.AddOpenApi();
builder.Services.AddScoped<InvoiceExtractor>();

var app = builder.Build();
app.MapOpenApi();

app.MapPost("/api/ai/ask", async (AskRequest req, IChatClient ai, CancellationToken ct) =>
{
    List<ChatMessage> messages =
    [
        new(ChatRole.System, "You are an assistant for a factoring company. Answer briefly. If you don't know, say so. Always answer in Azerbaijani, max 2 sentences."),
        new(ChatRole.User, req.Question)
    ];

    // Temperature null olanda API-yə ümumiyyətlə göndərilmir
    var options = new ChatOptions { Temperature = req.Temperature };

    var response = await ai.GetResponseAsync(messages, options, ct);

    return Results.Ok(new
    {
        answer = response.Text,
        inputTokens = response.Usage?.InputTokenCount,
        outputTokens = response.Usage?.OutputTokenCount
    });
});

app.MapPost("/api/invoices/extract", async (IFormFile file, InvoiceExtractor extractor, CancellationToken ct) =>
    {
        if (file.Length == 0 || !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("PDF file is required.");

        await using var stream = file.OpenReadStream();
        var text = PdfTextExtractor.Extract(stream);

        if (string.IsNullOrWhiteSpace(text))
            return Results.BadRequest("No text found. The PDF may be scanned.");

        var result = await extractor.ExtractAsync(text, ct);
        return Results.Ok(result);
    })
    .DisableAntiforgery();

app.Run();

record AskRequest(string Question, float? Temperature = null);