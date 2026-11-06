using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicRecommender.Data;
using MusicRecommender.Models;

namespace MusicRecommender.Controllers;

public class PlaylistsController : Controller
{
    private readonly AppDbContext _db;

    public PlaylistsController(AppDbContext db) => _db = db;

    // GET /Playlists
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _db.Playlists
            .OrderByDescending(p => p.CreatedAt)
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
            .ToListAsync(ct);
        return View(items);
    }

    // GET /Playlists/Create  - отделна страница за създаване
    [HttpGet]
    public IActionResult Create() => View(new PlaylistForm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlaylistForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var playlist = new Playlist
        {
            Name = form.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim()
        };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"Плейлистът „{playlist.Name}“ е създаден.";
        return RedirectToAction(nameof(Details), new { id = playlist.Id });
    }

    // GET /Playlists/Details/5
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var playlist = await _db.Playlists
            .Include(p => p.Tracks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist == null) return NotFound();

        playlist.Tracks = playlist.Tracks.OrderBy(t => t.AddedAt).ToList();
        return View(playlist);
    }

    // GET /Playlists/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var playlist = await _db.Playlists.FindAsync(new object[] { id }, ct);
        if (playlist == null) return NotFound();

        ViewData["PlaylistId"] = id;
        return View(new PlaylistForm { Name = playlist.Name, Description = playlist.Description });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PlaylistForm form, CancellationToken ct)
    {
        var playlist = await _db.Playlists.FindAsync(new object[] { id }, ct);
        if (playlist == null) return NotFound();

        ViewData["PlaylistId"] = id;
        if (!ModelState.IsValid) return View(form);

        playlist.Name = form.Name.Trim();
        playlist.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = "Промените са запазени.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var playlist = await _db.Playlists
            .Include(p => p.Tracks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist != null)
        {
            _db.Playlists.Remove(playlist);
            await _db.SaveChangesAsync(ct);
            TempData["Message"] = $"Плейлистът „{playlist.Name}“ е изтрит.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTrack(int playlistId, int trackId, CancellationToken ct)
    {
        var track = await _db.PlaylistTracks
            .FirstOrDefaultAsync(t => t.Id == trackId && t.PlaylistId == playlistId, ct);
        if (track != null)
        {
            _db.PlaylistTracks.Remove(track);
            await _db.SaveChangesAsync(ct);
            TempData["Message"] = "Песента е премахната от плейлиста.";
        }
        return RedirectToAction(nameof(Details), new { id = playlistId });
    }

    // ---------- JSON за прозореца "Добави в плейлист" ----------

    // GET /Playlists/List?deezerId=123  -> плейлисти + дали песента вече е в тях
    [HttpGet]
    public async Task<IActionResult> List(long deezerId, CancellationToken ct)
    {
        var items = await _db.Playlists
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                count = p.Tracks.Count,
                contains = p.Tracks.Any(t => t.DeezerId == deezerId)
            })
            .ToListAsync(ct);
        return Json(items);
    }

    // POST /Playlists/AddTrack  (playlistId = 0 + newName => създава нов плейлист)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTrack(int playlistId, string? newName, TrackDto track, CancellationToken ct)
    {
        if (track.DeezerId == 0 || string.IsNullOrWhiteSpace(track.Title))
            return BadRequest(new { error = "Невалидна песен." });

        Playlist? playlist;
        if (playlistId == 0)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName))
                return BadRequest(new { error = "Въведете име на плейлиста." });
            if (newName.Length > 80)
                return BadRequest(new { error = "Името може да е до 80 символа." });

            playlist = new Playlist { Name = newName };
            _db.Playlists.Add(playlist);
        }
        else
        {
            playlist = await _db.Playlists.FindAsync(new object[] { playlistId }, ct);
            if (playlist == null) return NotFound(new { error = "Плейлистът не е намерен." });
        }

        var exists = playlist.Id != 0
            && await _db.PlaylistTracks.AnyAsync(t => t.PlaylistId == playlist.Id && t.DeezerId == track.DeezerId, ct);
        if (!exists)
        {
            _db.PlaylistTracks.Add(new PlaylistTrack
            {
                Playlist = playlist,
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

        return Json(new { added = !exists, playlistId = playlist.Id, name = playlist.Name });
    }
}
