using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace InvoiceAI.Api.Services;

public record AgentResult(
    string Answer, List<string> ToolCalls, List<string> ToolResults,
    long? InputTokens, long? OutputTokens, long ElapsedMs);

public class InvoiceAgent(IChatClient ai, InvoiceTools tools)
{
    public async Task<AgentResult> AskAsync(string question, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var systemPrompt = $"""
                            You are an assistant for a factoring company. Today's date is {today:yyyy-MM-dd}.
                            Rules:
                            - Answer only from tool results. Never guess numbers, dates or terms.
                            - Dates, amounts, overdue status, risk: use find_invoices or get_buyer_exposure.
                            - Contract terms (penalties, interest, conditions): use search_documents.
                            - You may call several tools if needed.
                            - If a question combines status or dates (overdue, due soon, amounts) with contract terms,
                            first call find_invoices, then call search_documents with invoiceNumbers from that result.
                            - Always mention invoice numbers. A company can be a seller or a buyer: say which.
                            - If the question is clearly unrelated to invoices, companies or factoring,
                            do not call any tools. Say the data does not contain this information.
                            - If tools return nothing relevant, say the data does not contain this information.
                            - Answer in the same language as the question. Be brief.
                            """;

#pragma warning disable OPENAI001 // Eksperimental API: reasoning_effort yalnız OpenAI-a aiddir
        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(tools.FindInvoices, "find_invoices"),
                AIFunctionFactory.Create(tools.GetBuyerExposure, "get_buyer_exposure"),
                AIFunctionFactory.Create(tools.SearchDocuments, "search_documents")
            ],
            // Luna Chat Completions API-də tool-ları yalnız reasoning söndürüləndə qəbul edir
            RawRepresentationFactory = _ => new ChatCompletionOptions
            {
                ReasoningEffortLevel = new ChatReasoningEffortLevel("none")
            }
        };
#pragma warning restore OPENAI001

        List<ChatMessage> messages =
        [
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, question)
        ];

        var response = await ai.GetResponseAsync(messages, options, ct);
        sw.Stop();

        // Model hansı alətləri hansı parametrlərlə çağırdı: şəffaflıq və debug üçün
        var toolCalls = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Select(c => $"{c.Name}({JsonSerializer.Serialize(c.Arguments)})")
            .ToList();
        
        // Alətlərin modelə qaytardığı nəticələr (xəta olubsa, xəta mətni)
        var toolResults = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Select(r => r.Exception?.Message ?? JsonSerializer.Serialize(r.Result))
            .ToList();

        return new AgentResult(
            response.Text, toolCalls, toolResults,
            response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount,
            sw.ElapsedMilliseconds);
    }
}