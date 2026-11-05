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

app.Run();

record AskRequest(string Question, float? Temperature = null);