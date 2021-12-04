using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FileHelpers;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public interface IImdbWatchlistFromFileService
{
    IList<ImdbWatchlist> GetWatchlist(Stream stream, out List<Tuple<string, string, string>> lastImportErrors);
}

public class ImdbWatchlistFromFileService : IImdbWatchlistFromFileService
{
    private readonly ILogger<ImdbWatchlistFromFileService> logger;

    public ImdbWatchlistFromFileService(
        ILogger<ImdbWatchlistFromFileService> logger)
    {
        this.logger = logger;
    }

    public IList<ImdbWatchlist> GetWatchlist(Stream stream, out List<Tuple<string, string, string>> lastImportErrors)
    {
        List<Tuple<string, string, string>> lastImportErrors2 = null;
        var engine = new FileHelperAsyncEngine<ImdbUserWatchlistRecord>();

        var moreErrors = 0;
        using (var reader = new StreamReader(stream))
        using (engine.BeginReadStream(reader))
        {
            var result =
                engine.Select(record =>
                {
                    try
                    {
                        var _const = record.Const;

                        var date = DateTime.ParseExact(record.Created,
                            new string[] { "yyyy-MM-dd", "ddd MMM d HH:mm:ss yyyy", "ddd MMM dd HH:mm:ss yyyy" },
                            CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AllowWhiteSpaces);

                        return new ImdbWatchlist()
                        {
                            ImdbId = _const,
                            Title = record.Title,
                            Date = date
                        };
                    }
                    catch (Exception x)
                    {
                        if (lastImportErrors2 == null)
                            lastImportErrors2 = new List<Tuple<string, string, string>>();

                        if (lastImportErrors2.Count < 25)
                            lastImportErrors2.Add(
                                Tuple.Create(
                                    $"Lijn {engine.LineNumber - 1} kon niet verwerkt worden.",
                                    x.ToString(),
                                    "danger"));
                        else
                            moreErrors++;

                        return null;
                    }
                }).Where(i => i != null).ToList();

            if (moreErrors > 0)
                lastImportErrors2.Add(
                    Tuple.Create(
                        $"And {moreErrors} more errors...",
                        "",
                        "danger"));

            lastImportErrors = lastImportErrors2;
            return result;
        }
    }

#pragma warning disable CS0649
    [IgnoreFirst]
    [DelimitedRecord(",")]
    private class ImdbUserWatchlistRecord
    {
        // Position,Const,Created,Modified,Description,Title,URL,Title Type,IMDb Rating,Runtime (mins),Year,Genres,Num Votes,Release Date,Directors

        [FieldQuoted] public string Position;
        [FieldQuoted] public string Const;

        [FieldQuoted]
        //[FieldConverter(ConverterKind.DateMultiFormat, "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy")]
        // 'Wed Sep 20 00:00:00 2017'
        public string Created;

        //[FieldQuoted]
        //[FieldConverter(ConverterKind.DateMultiFormat, "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy")]
        // 'Wed Sep 20 00:00:00 2017'?
        public string Modified;
        [FieldQuoted] public string description;
        [FieldQuoted] public string Title;
        [FieldQuoted] public string Url;
        [FieldQuoted] public string TitleType;
        [FieldQuoted] public string IMDbRating;
        [FieldQuoted] public string Runtime;
        [FieldQuoted] public string Year;
        [FieldQuoted] public string Genres;
        [FieldQuoted] public string NumVotes;

        [FieldQuoted]
        //[FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
        public string ReleaseDate;

        [FieldQuoted] public string Directors;
        [FieldQuoted] public string YourRating;
        [FieldQuoted] public string Rated;
    }
}