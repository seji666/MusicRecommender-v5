using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicRecommender.Data;
using MusicRecommender.Models;
using MusicRecommender.Services;

namespace MusicRecommender.Controllers;

public class TracksController : Controller
{
    private readonly IDeezerService _deezer;
    private readonly AppDbContext _db;

    public TracksController(IDeezerService deezer, AppDbContext db)
    {
        _deezer = deezer;
        _db = db;
    }

    // GET /Tracks/Search?q=...
    public async Task<IActionResult> Search(string? q, CancellationToken ct)
    {
        var vm = new TrackListViewModel { Query = q };
        if (!string.IsNullOrWhiteSpace(q))
        {
            try
            {
                vm.Tracks = await _deezer.SearchAsync(q.Trim(), 12, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                vm.Error = "Грешка при връзка с Deezer. Опитайте отново.";
            }
        }
        return View(vm);
    }

    // GET /Tracks/Favorites
    public async Task<IActionResult> Favorites(CancellationToken ct)
    {
        var items = await _db.Favorites.OrderByDescending(f => f.AddedAt).ToListAsync(ct);
        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(TrackDto track, CancellationToken ct)
    {
        if (track.DeezerId != 0 && !string.IsNullOrWhiteSpace(track.Title)
            && !await _db.Favorites.AnyAsync(f => f.DeezerId == track.DeezerId, ct))
        {
            _db.Favorites.Add(new FavoriteTrack
            {
                DeezerId = track.DeezerId,
                Title = track.Title,
                Artist = track.Artist,
                Album = track.Album,
                CoverUrl = track.CoverUrl,
                PreviewUrl = track.PreviewUrl,
                Link = track.Link
            });
            await _db.SaveChangesAsync(ct);
        }

        // AJAX заявка (бутон "Добави") -> само 200 OK
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Ok();

        TempData["Message"] = "Песента е добавена към любимите.";
        return RedirectToAction(nameof(Favorites));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var fav = await _db.Favorites.FindAsync(new object[] { id }, ct);
        if (fav != null)
        {
            _db.Favorites.Remove(fav);
            await _db.SaveChangesAsync(ct);
            TempData["Message"] = "Песента е премахната.";
        }
        return RedirectToAction(nameof(Favorites));
    }
}
