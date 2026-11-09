using InvoiceAI.Api.Services;

namespace InvoiceAI.Tests;

public class TextChunkerTests
{
    [Fact]
    public void Short_text_stays_in_one_chunk()
    {
        var chunks = TextChunker.Split("Line 1\nLine 2");
        Assert.Single(chunks);
    }

    [Fact]
    public void Lines_are_never_cut_in_the_middle()
    {
        var lines = Enumerable.Range(1, 30)
            .Select(i => $"Invoice line number {i} with some text")
            .ToArray();

        var chunks = TextChunker.Split(string.Join('\n', lines), maxChars: 100);

        Assert.True(chunks.Count > 1);
        // Hər chunk-dakı hər sətir orijinal sətirlərdən biri olmalıdır, yarımçıq olmamalıdır
        Assert.All(chunks.SelectMany(c => c.Split('\n')), line => Assert.Contains(line, lines));
    }
}