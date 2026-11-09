using InvoiceAI.Api.Data;
using InvoiceAI.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using Pgvector;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Konfiqurasiya ---
var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI:ApiKey tapılmadı");
var model = builder.Configuration["OpenAI:Model"]
    ?? throw new InvalidOperationException("OpenAI:Model tapılmadı");
var embeddingModel = builder.Configuration["OpenAI:EmbeddingModel"]
    ?? throw new InvalidOperationException("OpenAI:EmbeddingModel tapılmadı");

// --- AI servisləri ---
var openAi = new OpenAIClient(apiKey);

builder.Services.AddChatClient(openAi.GetChatClient(model).AsIChatClient())
    .UseFunctionInvocation(configure: c =>
        c.IncludeDetailedErrors = builder.Environment.IsDevelopment())
    .Use((inner, sp) => new UsageLoggingChatClient(
        inner, sp.GetRequiredService<ILogger<UsageLoggingChatClient>>()));

builder.Services.AddEmbeddingGenerator(
    openAi.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator());

// --- Baza ---
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default"), x => x.UseVector()));

// --- Öz servislərimiz ---
builder.Services.AddScoped<InvoiceExtractor>();
builder.Services.AddScoped<InvoiceIndexer>();
builder.Services.AddScoped<InvoiceQaService>();
builder.Services.AddScoped<InvoiceTools>();
builder.Services.AddScoped<InvoiceAgent>();

builder.Services.AddOpenApi();

var app = builder.Build();
app.MapOpenApi();

// --- Endpointlər ---

app.MapPost("/api/ai/ask", async (AskRequest req, IChatClient ai, CancellationToken ct) =>
{
    List<ChatMessage> messages =
    [
        new(ChatRole.System, "You are an assistant for a factoring company. Answer briefly. If you don't know, say so."),
        new(ChatRole.User, req.Question)
    ];

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
    IFormFile file, InvoiceExtractor extractor, InvoiceIndexer indexer,
    AppDbContext db, CancellationToken ct) =>
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

    var chunkCount = await indexer.IndexAsync(invoice, ct);

    return Results.Created($"/api/invoices/{invoice.Id}", new
    {
        invoice.Id,
        invoice.Status,
        invoice.Warnings,
        ChunkCount = chunkCount,
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

app.MapPost("/api/invoices/reindex", async (
    AppDbContext db, InvoiceIndexer indexer, CancellationToken ct) =>
{
    var invoices = await db.Invoices.ToListAsync(ct);
    var total = 0;
    foreach (var inv in invoices)
        total += await indexer.IndexAsync(inv, ct);

    return Results.Ok(new { invoices = invoices.Count, chunks = total });
});

app.MapGet("/api/search", async (
    string q, int? top,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    AppDbContext db, CancellationToken ct) =>
{
    var queryVector = new Vector(await embedder.GenerateVectorAsync(q, cancellationToken: ct));

    var results = await db.InvoiceChunks.AsNoTracking()
        .OrderBy(c => c.Embedding.CosineDistance(queryVector))
        .Take(top ?? 3)
        .Select(c => new
        {
            c.Invoice.InvoiceNumber,
            c.Index,
            Distance = c.Embedding.CosineDistance(queryVector),
            c.Content
        })
        .ToListAsync(ct);

    return Results.Ok(results);
});

app.MapPost("/api/qa", async (QaRequest req, InvoiceQaService qa, CancellationToken ct) =>
    Results.Ok(await qa.AskAsync(req.Question, ct)));

app.MapPost("/api/agent", async (QaRequest req, InvoiceAgent agent, CancellationToken ct) =>
    Results.Ok(await agent.AskAsync(req.Question, ct)));

app.Run();

record AskRequest(string Question, float? Temperature = null);
record QaRequest(string Question);

public partial class Program;