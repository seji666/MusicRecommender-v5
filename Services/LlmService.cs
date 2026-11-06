using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MusicRecommender.Models;

namespace MusicRecommender.Services;

public class LlmOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string Model { get; set; } = "llama3.2";
    public string? ApiKey { get; set; }
}

public interface ILlmService
{
    Task<List<AiSuggestion>> GetSuggestionsAsync(
        RecommendationRequest request, IReadOnlyList<string> favorites, CancellationToken ct = default);
}

public class LlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;

    public LlmService(HttpClient http, IOptions<LlmOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<List<AiSuggestion>> GetSuggestionsAsync(
        RecommendationRequest req, IReadOnlyList<string> favorites, CancellationToken ct = default)
    {
        const string system =
            "Ти си експерт по музика. Препоръчваш само реални, съществуващи песни. " +
            "Отговаряй САМО с валиден JSON масив, без допълнителен текст и без markdown. " +
            "Всеки елемент е обект с полета \"artist\", \"title\" и \"reason\" " +
            "(\"reason\" е едно кратко изречение на български език).";

        var user = new StringBuilder();
        user.AppendLine($"Препоръчай ми точно {req.Count} песни.");
        user.AppendLine($"Настроение / повод: {req.Mood}");
        if (!string.IsNullOrWhiteSpace(req.Genre)) user.AppendLine($"Жанр: {req.Genre}");
        if (!string.IsNullOrWhiteSpace(req.Details)) user.AppendLine($"Допълнително: {req.Details}");
        if (favorites.Count > 0)
        {
            user.AppendLine("Моите любими песни: " + string.Join("; ", favorites));
            user.AppendLine("Препоръчай песни в същия дух, но не повтаряй тези.");
        }

        var body = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user.ToString() }
            },
            temperature = 0.7,
            stream = false
        };

        using var response = await _http.PostAsJsonAsync("chat/completions", body, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        return ParseSuggestions(content, req.Count);
    }

    // Моделите понякога добавят текст/```json около отговора – изрязваме само масива
    private static List<AiSuggestion> ParseSuggestions(string content, int max)
    {
        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("Езиковият модел върна отговор в неочакван формат.");

        var json = content.Substring(start, end - start + 1);
        var list = JsonSerializer.Deserialize<List<AiSuggestion>>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<AiSuggestion>();

        return list
            .Where(s => !string.IsNullOrWhiteSpace(s.Artist) && !string.IsNullOrWhiteSpace(s.Title))
            .Take(max)
            .ToList();
    }
}
