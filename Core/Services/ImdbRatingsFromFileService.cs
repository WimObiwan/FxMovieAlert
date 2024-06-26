using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FileHelpers;
using FxMovies.Core.Entities;

namespace FxMovies.Core.Services;

public interface IImdbRatingsFromFileService
{
    IList<ImdbRating>? GetRatings(Stream stream, out List<Tuple<string, string, string>>? lastImportErrors);
}

public class ImdbRatingsFromFileService : IImdbRatingsFromFileService
{
    public IList<ImdbRating> GetRatings(Stream stream, out List<Tuple<string, string, string>>? lastImportErrors)
    {
        List<Tuple<string, string, string>>? lastImportErrors2 = null;
        var engine = new FileHelperAsyncEngine<ImdbUserRatingRecord>();

        var moreErrors = 0;
        using var reader = new StreamReader(stream);
        using (engine.BeginReadStream(reader))
        {
            var result =
                engine.Select(record =>
                {
                    try
                    {
                        var constId = record.Const;

                        if (string.IsNullOrEmpty(record.DateAdded))
                            throw new Exception("Column DateAdded is empty");

                        var date = DateTime.ParseExact(record.DateAdded,
                            new[] { "yyyy-MM-dd", "ddd MMM d HH:mm:ss yyyy", "ddd MMM dd HH:mm:ss yyyy" },
                            CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AllowWhiteSpaces);

                        if (string.IsNullOrEmpty(record.YourRating))
                            throw new Exception("Column YourRating is empty");

                        // Ratings
                        // Const,Your Rating,Date Added,Title,URL,Title Type,IMDb Rating,Runtime (mins),Year,Genres,Num Votes,Release Date,Directors
                        var rating = int.Parse(record.YourRating);

                        return new ImdbRating
                        {
                            ImdbId = constId,
                            Title = record.Title,
                            Date = date,
                            Rating = rating
                        };
                    }
                    catch (Exception x)
                    {
                        lastImportErrors2 ??= new List<Tuple<string, string, string>>();

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
                }).Where(i => i != null).Select(i => i!).ToList();

            if (lastImportErrors2 != null && moreErrors > 0)
                lastImportErrors2.Add(
                    Tuple.Create(
                        $"And {moreErrors} more errors...",
                        "",
                        "danger"));

            lastImportErrors = lastImportErrors2;
            return result;
        }
    }

    #region CsvModel

    // Resharper disable All

#pragma warning disable CS0649
    [IgnoreFirst]
    [DelimitedRecord(",")]
    private class ImdbUserRatingRecord
    {
        // Const,Your Rating,Date Added,Title,URL,Title Type,IMDb Rating,Runtime (mins),Year,Genres,Num Votes,Release Date,Directors
        [FieldQuoted] public string? Const;

        [FieldQuoted] public string? YourRating;
        [FieldQuoted]
        //[FieldConverter(ConverterKind.DateMultiFormat, "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy")]
        // 'Wed Sep 20 00:00:00 2017'
        public string? DateAdded;

        [FieldQuoted] public string? Title;
        [FieldQuoted] public string? Url;
        [FieldQuoted] public string? TitleType;
        [FieldQuoted] public string? IMDbRating;
        [FieldQuoted] public string? Runtime;
        [FieldQuoted] public string? Year;
        [FieldQuoted] public string? Genres;
        [FieldQuoted] public string? NumVotes;
        [FieldQuoted]
        //[FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
        public string? ReleaseDate;
        [FieldQuoted] public string? Directors;
 
 
    }

    // Resharper restore All

    #endregion
}