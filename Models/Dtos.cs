using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MusicRecommender.Models;

// ---------- Deezer API ----------
public class DeezerListResponse
{
    [JsonPropertyName("data")] public List<DeezerTrack> Data { get; set; } = new();
}

public class DeezerTrack
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("preview")] public string? Preview { get; set; }
    [JsonPropertyName("link")] public string? Link { get; set; }
    [JsonPropertyName("artist")] public DeezerArtist? Artist { get; set; }
    [JsonPropertyName("album")] public DeezerAlbum? Album { get; set; }
}

public class DeezerArtist
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class DeezerAlbum
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("cover_medium")] public string? CoverMedium { get; set; }
}

// ---------- Общ модел за песен във визуализацията ----------
public class TrackDto
{
    public long DeezerId { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string? Album { get; set; }
    public string? CoverUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string? Link { get; set; }
}

// ---------- ИИ ----------
public class AiSuggestion
{
    public string Artist { get; set; } = "";
    public string Title { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class RecommendationItem
{
    public AiSuggestion Suggestion { get; set; } = new();
    public TrackDto? Track { get; set; }
}

// ---------- Формуляри / ViewModels ----------
public class RecommendationRequest
{
    [Required(ErrorMessage = "Въведете настроение или повод.")]
    [StringLength(100)]
    [Display(Name = "Настроение / повод")]
    public string Mood { get; set; } = "";

    [StringLength(100)]
    [Display(Name = "Жанр (по избор)")]
    public string? Genre { get; set; }

    [StringLength(500)]
    [Display(Name = "Допълнителни предпочитания")]
    public string? Details { get; set; }

    [Range(3, 10, ErrorMessage = "Изберете между 3 и 10 песни.")]
    [Display(Name = "Брой песни")]
    public int Count { get; set; } = 5;

    [Display(Name = "Вземи предвид любимите ми песни")]
    public bool UseFavorites { get; set; } = true;
}

public class RecommendationViewModel
{
    public RecommendationRequest Request { get; set; } = new();
    public List<RecommendationItem>? Results { get; set; }
    public string? Error { get; set; }
}

public class TrackListViewModel
{
    public string? Query { get; set; }
    public List<TrackDto> Tracks { get; set; } = new();
    public string? Error { get; set; }
}

public class HistoryItem
{
    public RecommendationRecord Record { get; set; } = new();
    public List<AiSuggestion> Suggestions { get; set; } = new();
}

// ---------- Плейлисти ----------
public class PlaylistForm
{
    [Required(ErrorMessage = "Въведете име на плейлиста.")]
    [StringLength(80, ErrorMessage = "Името може да е до 80 символа.")]
    [Display(Name = "Име")]
    public string Name { get; set; } = "";

    [StringLength(300, ErrorMessage = "Описанието може да е до 300 символа.")]
    [Display(Name = "Описание (по избор)")]
    public string? Description { get; set; }
}

public class PlaylistSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TrackCount { get; set; }
    public List<string> Covers { get; set; } = new();   // до 4 обложки за мозайка
}

public class HomeViewModel
{
    public List<TrackDto> TopTracks { get; set; } = new();
    public string? Error { get; set; }
    public int FavoritesCount { get; set; }
    public int PlaylistsCount { get; set; }
    public int RecommendationsCount { get; set; }
    public List<PlaylistSummary> RecentPlaylists { get; set; } = new();
}
