using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusicRecommender.Data;
using MusicRecommender.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Deezer API (не изисква API ключ)
builder.Services.AddHttpClient<IDeezerService, DeezerService>(c =>
{
    c.BaseAddress = new Uri("https://api.deezer.com/");
    c.Timeout = TimeSpan.FromSeconds(15);
});

// Езиков модел (OpenAI-съвместим API: Ollama, LM Studio, OpenAI и др.)
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));
builder.Services.AddHttpClient<ILlmService, LlmService>((sp, c) =>
{
    var opt = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
    c.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromMinutes(3);
    if (!string.IsNullOrWhiteSpace(opt.ApiKey))
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opt.ApiKey);
});

var app = builder.Build();

// Създава базата автоматично при първо стартиране
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // EnsureCreated не добавя нови таблици към вече съществуваща база (music.db от по-стара версия),
    // затова таблиците за плейлисти се създават и ръчно, ако липсват.
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Playlists"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Playlists"" PRIMARY KEY AUTOINCREMENT,
            ""Name"" TEXT NOT NULL,
            ""Description"" TEXT NULL,
            ""CreatedAt"" TEXT NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""PlaylistTracks"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PlaylistTracks"" PRIMARY KEY AUTOINCREMENT,
            ""PlaylistId"" INTEGER NOT NULL,
            ""DeezerId"" INTEGER NOT NULL,
            ""Title"" TEXT NOT NULL,
            ""Artist"" TEXT NOT NULL,
            ""Album"" TEXT NULL,
            ""CoverUrl"" TEXT NULL,
            ""PreviewUrl"" TEXT NULL,
            ""Link"" TEXT NULL,
            ""AddedAt"" TEXT NOT NULL,
            CONSTRAINT ""FK_PlaylistTracks_Playlists_PlaylistId"" FOREIGN KEY (""PlaylistId"") REFERENCES ""Playlists"" (""Id"") ON DELETE CASCADE
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PlaylistTracks_PlaylistId_DeezerId""
        ON ""PlaylistTracks"" (""PlaylistId"", ""DeezerId"");");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
