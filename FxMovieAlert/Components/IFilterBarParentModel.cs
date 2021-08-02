using System;
using FxMovieAlert.Pages;

namespace FxMovieAlert.Components
{
    public interface IFilterBarParentModel
    {
        bool? FilterOnlyHighlights { get; }
        bool FilterOnlyHighlightsDefault { get; }
        int FilterTypeMaskDefault { get; }
        int FilterTypeMask { get; }
        decimal? FilterMinRating { get; }
        bool? FilterNotYetRated { get; }
        Cert FilterCert { get; }
        int FilterMaxDaysDefault { get; }
        int FilterMaxDays { get; }

        int Count { get; }
        int CountTypeFilm { get; }
        int CountTypeShort { get; }
        int CountTypeSerie { get; }
        int CountMinRating5 { get; }
        int CountMinRating6 { get; }
        int CountMinRating65 { get; }
        int CountMinRating7 { get; }
        int CountMinRating8 { get; }
        int CountMinRating9 { get; }
        int CountNotOnImdb { get; }
        int CountNotRatedOnImdb { get; }
        int CountNotYetRated { get; }
        int CountRated { get; }
        int CountCertNone { get; }
        int CountCertG { get; }
        int CountCertPG { get; }
        int CountCertPG13 { get; }
        int CountCertR { get; }
        int CountCertNC17 { get; }
        int CountCertOther { get; }
        int Count3days { get; }
        int Count5days { get; }
        int Count8days { get; }

        string ImdbUserId { get; }

        DateTime? RefreshRequestTime { get; }
        DateTime? LastRefreshRatingsTime { get; }
    }
}