using MusicRecommender.Models;

namespace MusicRecommender.Services;

public interface IDeezerService
{
    Task<List<TrackDto>> SearchAsync(string query, int limit = 12, CancellationToken ct = default);
    Task<List<TrackDto>> GetChartAsync(int limit = 12, CancellationToken ct = default);
    Task<TrackDto?> FindTrackAsync(string artist, string title, CancellationToken ct = default);
}

public class DeezerService : IDeezerService
{
    private readonly HttpClient _http;

    public DeezerService(HttpClient http) => _http = http;

    public Task<List<TrackDto>> SearchAsync(string query, int limit = 12, CancellationToken ct = default)
        => GetTracksAsync($"search?q={Uri.EscapeDataString(query)}&limit={limit}", ct);

    public Task<List<TrackDto>> GetChartAsync(int limit = 12, CancellationToken ct = default)
        => GetTracksAsync($"chart/0/tracks?limit={limit}", ct);

    // Търси конкретна песен (използва се за песните, предложени от ИИ)
    public async Task<TrackDto?> FindTrackAsync(string artist, string title, CancellationToken ct = default)
    {
        try
        {
            var strict = $"artist:\"{Clean(artist)}\" track:\"{Clean(title)}\"";
            var result = await SearchAsync(strict, 1, ct);
            if (result.Count == 0)
                result = await SearchAsync($"{artist} {title}", 1, ct);
            return result.FirstOrDefault();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<List<TrackDto>> GetTracksAsync(string url, CancellationToken ct)
    {
        var response = await _http.GetFromJsonAsync<DeezerListResponse>(url, ct);
        if (response == null) return new List<TrackDto>();
        return response.Data.Select(Map).ToList();
    }

    private static TrackDto Map(DeezerTrack t) => new()
    {
        DeezerId = t.Id,
        Title = t.Title,
        Artist = t.Artist?.Name ?? "",
        Album = t.Album?.Title,
        CoverUrl = t.Album?.CoverMedium,
        PreviewUrl = t.Preview,
        Link = t.Link
    };

    private static string Clean(string s) => s.Replace("\"", " ").Trim();
}
