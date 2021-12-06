using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public interface IHumoService
{
    Task<IList<MovieEvent>> GetGuide(DateTime date);
}

public class HumoService : IHumoService
{
    private static readonly string[] Channels =
    {
        "een",
        "canvas",
        "vtm",
        "play4",
        "vtm2",
        "play5",
        "play6",
        "play7",
        "vtm3",
        "vtm4",
        "npo-1",
        "npo-2",
        "npo-3",
        "mtv-vlaanderen",
        "viceland",
        "ketnet",
        "vtm-kids",
        "studio-100-tv",
        "disney-channel",
        "nickelodeon-spike",
        "cartoon24"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HumoService> _logger;

    public HumoService(
        ILogger<HumoService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IList<MovieEvent>> GetGuide(DateTime date)
    {
        var humo = await GetHumoDataWithRetry(date);

        FilterBroadcasters(date, humo);

        FilterMovies(humo);

        var movieEvents = new List<MovieEvent>();
        movieEvents.AddRange(MovieAdapter(humo));
        return movieEvents;
    }

    private async Task<Humo> GetHumoData(DateTime date)
    {
        var dateString = date.ToString("yyyy-MM-dd");
        var url = $"/tv-gids/api/v2/broadcasts/{dateString}";

        _logger.LogInformation("Retrieving from Humo: {Url}", url);

        var client = _httpClientFactory.CreateClient("humo");
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var humo = await response.Content.ReadFromJsonAsync<Humo>();

        return humo;
    }

    private async Task<Humo> GetHumoDataWithRetry(DateTime date)
    {
        try
        {
            return await GetHumoData(date);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "First try failed, {Date}", date);
        }

        await Task.Delay(5000);

        try
        {
            return await GetHumoData(date);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Second try failed, {Date}", date);

            throw;
        }
    }

    private void FilterBroadcasters(DateTime date, Humo humo)
    {
        if (humo == null || humo.channels == null) return;
        foreach (var channel in Channels)
            if (!humo.channels.Any(b => b != null && b.seoKey == channel))
                _logger.LogWarning("No broadcasts found for Channel {channel} on {Date}",
                    channel, date.ToShortDateString());

        humo.channels.RemoveAll(b => b == null || !Channels.Contains(b.seoKey));
    }

    private void FilterMovies(Humo humo)
    {
        if (humo == null || humo.channels == null) return;

        foreach (var channel in humo.channels)
            //channel.broadcasts.RemoveAll(b => !b.IsMovie() && ! b.IsFirstOfSerieSeason());
            channel.broadcasts.RemoveAll(b => !b.IsMovie());

        humo.channels.RemoveAll(c => c.broadcasts == null || c.broadcasts.Count == 0);
    }

    private IList<MovieEvent> MovieAdapter(Humo humo)
    {
        if (humo == null || humo.channels == null) return new List<MovieEvent>();

        var movies = new List<MovieEvent>();
        foreach (var humoChannel in humo.channels)
        {
            var channel = new Channel
            {
                Code = humoChannel.seoKey,
                Name = humoChannel.name,
                LogoS = humoChannel.channelLogoUrl
            };

            foreach (var broadcast in humoChannel.broadcasts)
            {
                var description = broadcast.synopsis;
                int? year = null;
                // int year = broadcast.program.year;

                // description = description.Replace($" ({year})", "");

                // if (broadcast.program.episodenumber != 0 && broadcast.program.episodeseason != 0)
                // {
                //     description += $" (SERIE: begin van seizoen {broadcast.program.episodeseason})";
                // }

                var genre = broadcast.genre?.Trim() ?? "";
                if (genre != "")
                    genre += " - ";
                genre += string.Join(' ', broadcast.subGenres);

                int type;
                if (broadcast.IsMovie())
                {
                    if (broadcast.IsShort())
                        type = 2; // short
                    else
                        type = 1; // movie
                }
                else
                {
                    type = 3; // serie
                }

                string opinion = null;
                // string opinion = broadcast.program.opinion;
                // if (!string.IsNullOrEmpty(broadcast.program.appreciation) 
                //     && int.TryParse(broadcast.program.appreciation, out int appreciation)
                //     && appreciation > 0 && appreciation <= 50)
                // {
                //     string stars = new string('★', appreciation / 10);
                //     if (appreciation % 10 > 0)
                //         stars += '½';
                //     if (string.IsNullOrEmpty(opinion))
                //         opinion = stars;
                //     else
                //         opinion = stars + " " + opinion;
                // }
                if (broadcast.rating.HasValue)
                {
                    var rating = broadcast.rating.Value;
                    if (rating > 0 && rating <= 100)
                    {
                        var stars = new string('★', rating / 20);
                        if (rating % 20 > 0)
                            stars += '½';
                        opinion = stars;
                    }
                }

                var movie = new MovieEvent
                {
                    ExternalId = broadcast.uuid.ToString(),
                    Channel = channel,
                    Title = broadcast.title,
                    Year = year,
                    Vod = false,
                    Feed = MovieEvent.FeedType.Broadcast,
                    StartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(broadcast.from / 1000)
                        .ToLocalTime(),
                    EndTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(broadcast.to / 1000)
                        .ToLocalTime(),
                    Duration = broadcast.duration.HasValue ? broadcast.duration.Value / 60 : null,
                    PosterS = broadcast.imageUrl,
                    PosterM = broadcast.imageUrl,
                    Content = broadcast.synopsis,
                    Opinion = opinion,
                    Genre = genre,
                    Type = type
                };

                movies.Add(movie);
            }
        }

        return movies;
    }

    #region JsonModel

    // Resharper disable All

    [DebuggerDisplay("title = {title}")]
    private class HumoBroadcast
    {
        public Guid uuid { get; set; }
        public long from { get; set; }
        public long to { get; set; }
        public int? duration { get; set; }
        public string playableType { get; set; }
        public string title { get; set; }
        public string genre { get; set; }
        public string[] subGenres { get; set; }
        public string synopsis { get; set; }
        public string imageUrl { get; set; }
        public int? rating { get; set; }


        public bool IsMovie()
        {
            return playableType.Equals("movies", StringComparison.InvariantCultureIgnoreCase);
        }
        //public bool IsFirstOfSerieSeason() => program.genres != null && program.genres.Any(g => g.StartsWith("serie-")) && program.episodenumber == 1;

        public bool IsShort()
        {
            return duration < 3600;
        }
    }

    [DebuggerDisplay("name = {name}")]
    private class HumoChannel
    {
        public string seoKey { get; set; }
        public string name { get; set; }
        public string channelLogoUrl { get; set; }
        public List<HumoBroadcast> broadcasts { get; set; }
    }

    private class Humo
    {
        public List<HumoChannel> channels { get; set; }
    }

    // Resharper restore All

    #endregion
}