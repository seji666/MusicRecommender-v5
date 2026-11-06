using Microsoft.EntityFrameworkCore;
using MusicRecommender.Models;

namespace MusicRecommender.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FavoriteTrack> Favorites => Set<FavoriteTrack>();
    public DbSet<RecommendationRecord> Recommendations => Set<RecommendationRecord>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<FavoriteTrack>().HasIndex(f => f.DeezerId).IsUnique();

        b.Entity<Playlist>()
            .HasMany(p => p.Tracks)
            .WithOne(t => t.Playlist)
            .HasForeignKey(t => t.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
        // една песен - най-много веднъж в даден плейлист
        b.Entity<PlaylistTrack>().HasIndex(t => new { t.PlaylistId, t.DeezerId }).IsUnique();
    }
}
