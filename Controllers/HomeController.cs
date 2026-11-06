using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicRecommender.Data;
using MusicRecommender.Models;
using MusicRecommender.Services;

namespace MusicRecommender.Controllers;

public class HomeController : Controller
{
    private readonly IDeezerService _deezer;
    private readonly AppDbContext _db;

    public HomeController(IDeezerService deezer, AppDbContext db)
    {
        _deezer = deezer;
        _db = db;
    }

    // Начална страница: статистика, възможности на приложението и кратък преглед на топ песните
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = new HomeViewModel
        {
            FavoritesCount = await _db.Favorites.CountAsync(ct),
            PlaylistsCount = await _db.Playlists.CountAsync(ct),
            RecommendationsCount = await _db.Recommendations.CountAsync(ct),
            RecentPlaylists = await _db.Playlists
                .OrderByDescending(p => p.CreatedAt)
                .Take(3)
                .Select(p => new PlaylistSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    TrackCount = p.Tracks.Count,
                    Covers = p.Tracks
                        .Where(t => t.CoverUrl != null)
                        .OrderBy(t => t.AddedAt)
                        .Select(t => t.CoverUrl!)
                        .Take(4)
                        .ToList()
                })
                .ToListAsync(ct)
        };

        try
        {
            vm.TopTracks = await _deezer.GetChartAsync(6, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            vm.Error = "Не може да се зареди класацията от Deezer. Опитайте отново по-късно.";
        }
        return View(vm);
    }

    // Пълна топ класация (преди това беше началната страница)
    public async Task<IActionResult> Top(CancellationToken ct)
    {
        var vm = new TrackListViewModel();
        try
        {
            vm.Tracks = await _deezer.GetChartAsync(12, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            vm.Error = "Не може да се зареди класацията от Deezer. Опитайте отново по-късно.";
        }
        return View(vm);
    }

    public IActionResult About() => View();
}
