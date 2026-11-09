using InvoiceAI.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceAI.Tests;

// Qeyd: bazada 3 test fakturası olmalıdır.
// "Vaxtı keçib" nəticələri bugünkü tarixdən asılıdır: 14 yanvar 2027-dən sonra AQ-0917 də gecikəcək.
[Trait("Category", "Eval")]
public class AgentEvals(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<AgentResult> Ask(string question)
    {
        using var scope = factory.Services.CreateScope();
        var agent = scope.ServiceProvider.GetRequiredService<InvoiceAgent>();
        return await agent.AskAsync(question, CancellationToken.None);
    }

    [Fact]
    public async Task Overdue_question_uses_structured_query()
    {
        var r = await Ask("Hansı fakturaların ödəniş tarixi keçib?");

        Assert.Contains(r.ToolCalls, c => c.StartsWith("find_invoices"));
        Assert.Contains("INV-2026-0451", r.Answer);
        Assert.Contains("NTS/26/1183", r.Answer);
        Assert.DoesNotContain("AQ-0917", r.Answer);
    }

    [Fact]
    public async Task Risk_question_uses_exposure_tool()
    {
        var r = await Ask("Hansı alıcı üzrə riskimiz ən böyükdür?");

        Assert.Contains(r.ToolCalls, c => c.StartsWith("get_buyer_exposure"));
        Assert.Contains("Gunesh", r.Answer);
    }

    [Fact]
    public async Task Overdue_penalties_are_found_in_two_steps()
    {
        var r = await Ask("Vaxtı keçmiş fakturalar üzrə gecikmə cəriməsi şərtləri nədir?");

        // Yol: əvvəl status, sonra yalnız həmin fakturalarda axtarış
        Assert.StartsWith("find_invoices", r.ToolCalls.First());
        Assert.Contains(r.ToolCalls, c => c.StartsWith("search_documents") && c.Contains("invoiceNumbers"));

        // Nəticə
        Assert.Contains("INV-2026-0451", r.Answer);
        Assert.Contains("NTS/26/1183", r.Answer);
        Assert.DoesNotContain("AQ-0917", r.Answer);
    }

    [Fact]
    public async Task Off_topic_question_is_declined_without_inventing()
    {
        const string question = "Dünənki futbol matçının nəticəsi nədir?";
        var r = await Ask(question);

        // Xərc nəzarəti: ən çox bir yoxlama çağırışı qəbul edilir
        Assert.True(r.ToolCalls.Count <= 1,
            $"Too many tool calls: {string.Join(", ", r.ToolCalls)}");

        // Davranış: məlumatın olmadığını deyir və nəticə uydurmur
        using var scope = factory.Services.CreateScope();
        var ai = scope.ServiceProvider.GetRequiredService<IChatClient>();

        var verdict = await EvalJudge.JudgeAsync(ai, question, r.Answer,
            "The answer says this information is not available in the data " +
            "and does not invent any match result or score.");

        Assert.True(verdict.Pass, $"Judge: {verdict.Reason}. Answer: {r.Answer}");
    }
}