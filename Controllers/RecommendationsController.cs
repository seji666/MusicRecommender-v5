using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusicRecommender.Data;
using MusicRecommender.Models;
using MusicRecommender.Services;

namespace MusicRecommender.Controllers;

public class RecommendationsController : Controller
{
    private readonly ILlmService _llm;
    private readonly IDeezerService _deezer;
    private readonly AppDbContext _db;
    private readonly LlmOptions _llmOptions;

    public RecommendationsController(
        ILlmService llm, IDeezerService deezer, AppDbContext db, IOptions<LlmOptions> llmOptions)
    {
        _llm = llm;
        _deezer = deezer;
        _db = db;
        _llmOptions = llmOptions.Value;
    }

    [HttpGet]
    public IActionResult Index() => View(new RecommendationViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        [Bind(Prefix = "Request")] RecommendationRequest request, CancellationToken ct)
    {
        var vm = new RecommendationViewModel { Request = request };
        if (!ModelState.IsValid) return View(vm);

        try
        {
            var favorites = new List<string>();
            if (request.UseFavorites)
            {
                favorites = await _db.Favorites
                    .OrderByDescending(f => f.AddedAt)
                    .Take(10)
                    .Select(f => f.Artist + " - " + f.Title)
                    .ToListAsync(ct);
            }

            var suggestions = await _llm.GetSuggestionsAsync(request, favorites, ct);
            if (suggestions.Count == 0)
            {
                vm.Error = "Моделът не върна препоръки. Опитайте с друго описание.";
                return View(vm);
            }

            var tasks = suggestions.Select(async s => new RecommendationItem
            {
                Suggestion = s,
                Track = await _deezer.FindTrackAsync(s.Artist, s.Title, ct)
            });
            vm.Results = (await Task.WhenAll(tasks)).ToList();

            _db.Recommendations.Add(new RecommendationRecord
            {
                Mood = request.Mood,
                Genre = request.Genre,
                Details = request.Details,
                ResultJson = JsonSerializer.Serialize(suggestions)
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            vm.Error = DescribeHttpError(ex);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            vm.Error = "Езиковият модел отговаря твърде бавно (изтече времето за заявката). " +
                       "Опитайте отново или изберете по-малък/по-бърз модел.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or KeyNotFoundException)
        {
            vm.Error = "Моделът върна отговор в неочакван формат. Опитайте отново.";
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var records = await _db.Recommendations
            .OrderByDescending(r => r.CreatedAt)
            .Take(30)
            .ToListAsync(ct);

        var items = records.Select(r => new HistoryItem
        {
            Record = r,
            Suggestions = JsonSerializer.Deserialize<List<AiSuggestion>>(r.ResultJson)
                          ?? new List<AiSuggestion>()
        }).ToList();

        return View(items);
    }

    private string DescribeHttpError(HttpRequestException ex) => ex.StatusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "Езиковият модел отказа достъп. Проверете стойността на Llm:ApiKey.",
        HttpStatusCode.NotFound =>
            $"Моделът „{_llmOptions.Model}“ или адресът {_llmOptions.BaseUrl} не е намерен. " +
            $"При Ollama изтеглете модела с: ollama pull {_llmOptions.Model}",
        HttpStatusCode.TooManyRequests =>
            "Достигнат е лимитът на заявките към езиковия модел. Опитайте след малко.",
        null =>
            $"Няма връзка с езиковия модел на {_llmOptions.BaseUrl}. " +
            "Стартирайте Ollama (или друг локален сървър) или задайте отдалечена услуга в " +
            "appsettings.json → Llm (BaseUrl, Model, ApiKey).",
        _ => $"Езиковият модел върна грешка ({(int)ex.StatusCode}). Проверете настройките в раздел Llm."
    };
}
