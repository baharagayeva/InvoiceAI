namespace InvoiceAI.Api.Services;

public static class TextChunker
{
    public static List<string> Split(string text, int maxChars = 300, int overlap = 50)
    {
        var clean = text.Replace("\r\n", "\n").Trim();
        if (clean.Length <= maxChars) return [clean];

        var chunks = new List<string>();
        var start = 0;

        while (start < clean.Length)
        {
            var end = Math.Min(start + maxChars, clean.Length);

            // Mümkünsə sətrin sonunda kəs ki, cümlə yarıda qalmasın
            if (end < clean.Length)
            {
                var lastBreak = clean.LastIndexOf('\n', end - 1, end - start);
                if (lastBreak > start + maxChars / 2) end = lastBreak;
            }

            chunks.Add(clean[start..end].Trim());
            if (end == clean.Length) break;

            start = Math.Max(end - overlap, start + 1);
        }

        return chunks;
    }
}