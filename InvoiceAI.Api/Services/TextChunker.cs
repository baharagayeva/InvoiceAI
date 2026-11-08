namespace InvoiceAI.Api.Services;

public static class TextChunker
{
    // Mətni sətirlərə görə bölür: sətir heç vaxt ortadan kəsilmir.
    // overlapLines: əvvəlki chunk-ın son neçə sətri növbəti chunk-a da düşsün.
    public static List<string> Split(string text, int maxChars = 300, int overlapLines = 1)
    {
        var lines = text.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<string>();
        var current = new List<string>();
        var length = 0;

        foreach (var line in lines)
        {
            if (length + line.Length > maxChars && current.Count > 0)
            {
                chunks.Add(string.Join('\n', current));
                current = current.TakeLast(overlapLines).ToList();
                length = current.Sum(l => l.Length + 1);
            }

            current.Add(line);
            length += line.Length + 1;
        }

        if (current.Count > 0)
            chunks.Add(string.Join('\n', current));

        return chunks;
    }
}