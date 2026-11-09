using Microsoft.Extensions.AI;

namespace InvoiceAI.Tests;

public record JudgeVerdict(bool Pass, string Reason);

public static class EvalJudge
{
    public static async Task<JudgeVerdict> JudgeAsync(
        IChatClient ai, string question, string answer, string criteria)
    {
        var prompt = $"""
                      You evaluate an AI assistant's answer.

                      Question: {question}
                      Answer: {answer}
                      Criteria: {criteria}

                      Set pass to true only if the answer fully meets the criteria.
                      Give a one-sentence reason.
                      """;

        var response = await ai.GetResponseAsync<JudgeVerdict>(prompt);
        return response.Result;
    }
}