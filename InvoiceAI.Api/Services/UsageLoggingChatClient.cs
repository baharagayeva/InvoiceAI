using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace InvoiceAI.Api.Services;

// Hər LLM çağırışını loglayır: tokenlər, vaxt, təxmini xərc
public class UsageLoggingChatClient(IChatClient inner, ILogger<UsageLoggingChatClient> logger)
    : DelegatingChatClient(inner)
{
    // gpt-6-luna qiymətləri, 1M token üçün dollar
    private const decimal InputPricePerMillion = 0.10m;
    private const decimal OutputPricePerMillion = 0.50m;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        sw.Stop();

        var input = response.Usage?.InputTokenCount ?? 0;
        var output = response.Usage?.OutputTokenCount ?? 0;
        var cost = input * InputPricePerMillion / 1_000_000m + output * OutputPricePerMillion / 1_000_000m;

        logger.LogInformation(
            "LLM call: model={Model} input={InputTokens} output={OutputTokens} ms={ElapsedMs} cost=${Cost:F6}",
            response.ModelId, input, output, sw.ElapsedMilliseconds, cost);

        return response;
    }
}