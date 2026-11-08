using System.Diagnostics;
using InvoiceAI.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace InvoiceAI.Api.Services;

public record SearchQueries(string English, string Azerbaijani);

public record QaModelAnswer(bool Found, string Answer, List<string> UsedSourceIds);

public record SourceRef(string Id, string? InvoiceNumber, int ChunkIndex, double Distance);

public record QaResult(
    string Question,
    List<string> SearchQueries,
    bool Found,
    string Answer,
    List<SourceRef> Sources,
    long? InputTokens,
    long? OutputTokens,
    long ElapsedMs);

public class InvoiceQaService(
    IChatClient ai,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    AppDbContext db)
{
    private const string RewritePrompt = """
        You turn a user's question about invoices into short search queries.
        Return one query in English and one in Azerbaijani.
        Keep company names unchanged. Do not answer the question.
        """;

    private const string AnswerPrompt = """
        You answer questions about invoices for a factoring company.
        Rules:
        - Use ONLY the sources below. Never use outside knowledge.
        - Each source has an id like S1. Put the ids you used in usedSourceIds.
        - If the sources do not answer the question, set found to false
          and say that the documents do not contain this information.
        - A company can be a seller in one invoice and a buyer in another. Say which role it has.
        - Answer in the same language as the question. Be brief.
        """;

    private record ChunkHit(Guid Id, string? InvoiceNumber, int Index, string Content, double Distance);

    public async Task<QaResult> AskAsync(string question, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        long inputTokens = 0, outputTokens = 0;

        // 1. Sualı EN və AZ axtarış sorğularına çevir (multi-query)
        var rewrite = await ai.GetResponseAsync<SearchQueries>(
            [new(ChatRole.System, RewritePrompt), new(ChatRole.User, question)],
            cancellationToken: ct);
        inputTokens += rewrite.Usage?.InputTokenCount ?? 0;
        outputTokens += rewrite.Usage?.OutputTokenCount ?? 0;

        var queries = new List<string> { question };
        if (rewrite.TryGetResult(out var q))
        {
            queries.Add(q.English);
            queries.Add(q.Azerbaijani);
        }
        queries = queries.Distinct().ToList();

        // 2. Hər sorğu ilə axtar və nəticələri birləşdir (eyni chunk bir dəfə, ən yaxşı distance ilə)
        var vectors = await embedder.GenerateAsync(queries, cancellationToken: ct);
        var hits = new Dictionary<Guid, ChunkHit>();

        foreach (var v in vectors)
        {
            var qv = new Vector(v.Vector);
            var found = await db.InvoiceChunks.AsNoTracking()
                .OrderBy(c => c.Embedding.CosineDistance(qv))
                .Take(4)
                .Select(c => new ChunkHit(
                    c.Id, c.Invoice.InvoiceNumber, c.Index, c.Content,
                    c.Embedding.CosineDistance(qv)))
                .ToListAsync(ct);

            foreach (var h in found)
                if (!hits.TryGetValue(h.Id, out var existing) || h.Distance < existing.Distance)
                    hits[h.Id] = h;
        }

        var context = hits.Values.OrderBy(h => h.Distance).Take(6).ToList();

        // 3. Mənbələri nömrələ və modelə ver
        var sources = context
            .Select((h, i) => new SourceRef($"S{i + 1}", h.InvoiceNumber, h.Index, h.Distance))
            .ToList();

        var sourcesText = string.Join("\n\n", context.Select((h, i) => $"[S{i + 1}]\n{h.Content}"));

        var response = await ai.GetResponseAsync<QaModelAnswer>(
            [
                new(ChatRole.System, AnswerPrompt),
                new(ChatRole.User, $"Sources:\n\n{sourcesText}\n\nQuestion: {question}")
            ],
            cancellationToken: ct);
        inputTokens += response.Usage?.InputTokenCount ?? 0;
        outputTokens += response.Usage?.OutputTokenCount ?? 0;

        sw.Stop();

        if (!response.TryGetResult(out var answer))
            return new QaResult(question, queries, false, "Model did not return a valid answer.",
                [], inputTokens, outputTokens, sw.ElapsedMilliseconds);

        // Yalnız modelin həqiqətən istifadə etdiyi mənbələri qaytar
        var used = sources.Where(s => answer.UsedSourceIds.Contains(s.Id)).ToList();

        return new QaResult(question, queries, answer.Found, answer.Answer,
            used, inputTokens, outputTokens, sw.ElapsedMilliseconds);
    }
}