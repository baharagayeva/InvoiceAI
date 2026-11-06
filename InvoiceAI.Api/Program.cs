using InvoiceAI.Api.Data;
using InvoiceAI.Api.Services;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

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

app.MapPost("/api/invoices", async (
        IFormFile file, InvoiceExtractor extractor, AppDbContext db, CancellationToken ct) =>
    {
        if (file.Length == 0 || !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("PDF file is required.");

        await using var stream = file.OpenReadStream();
        var text = PdfTextExtractor.Extract(stream);

        if (string.IsNullOrWhiteSpace(text))
            return Results.BadRequest("No text found. The PDF may be scanned.");

        var result = await extractor.ExtractAsync(text, ct);
        if (result.Invoice is null)
            return Results.UnprocessableEntity(result.Warnings);

        var invoice = Invoice.From(file.FileName, text, result);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/invoices/{invoice.Id}", new
        {
            invoice.Id,
            invoice.Status,
            invoice.Warnings,
            result.InputTokens,
            result.OutputTokens,
            result.ElapsedMs
        });
    })
    .DisableAntiforgery();

app.MapGet("/api/invoices", async (AppDbContext db, CancellationToken ct) =>
    await db.Invoices.AsNoTracking()
        .OrderByDescending(i => i.CreatedAt)
        .Select(i => new
        {
            i.Id, i.InvoiceNumber, i.SellerName, i.BuyerName,
            i.TotalAmount, i.Currency, i.IssueDate, i.DueDate, i.Status
        })
        .ToListAsync(ct));

app.Run();

record AskRequest(string Question, float? Temperature = null);