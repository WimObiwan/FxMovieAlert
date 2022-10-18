using System;
using FxMovies.Core.Queries;

namespace FxMovies.Site.Components;

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

    BroadcastQueryResult Data { get; }

    string ImdbUserId { get; }

    DateTime? RefreshRequestTime { get; }
    DateTime? LastRefreshRatingsTime { get; }
}