
using System;
using System.Globalization;
using System.Text;
using FxMovieAlert.Pages;
using Microsoft.AspNetCore.Components;

namespace FxMovieAlert.Components
{
    [Flags]
    public enum Cert
    {
        all = 0,
        none = 1,
        other = 2,
        g = 4,
        pg = 8,
        pg13 = 16,
        r = 32,
        nc17 = 64,
        all2 = 127,
    }

    public partial class FilterBar
    {        
        [Parameter]
        public IFilterBarParentModel ParentModel { get; set; }

        public const decimal NO_IMDB_ID = -1.0m;
        public const decimal NO_IMDB_RATING = -2.0m;

        protected bool? FilterOnlyHighlights => ParentModel.FilterOnlyHighlights;
        protected bool FilterOnlyHighlightsDefault => ParentModel.FilterOnlyHighlightsDefault;
        protected int FilterTypeMaskDefault => ParentModel.FilterTypeMaskDefault;
        protected int FilterTypeMask => ParentModel.FilterTypeMask;
        protected decimal? FilterMinRating => ParentModel.FilterMinRating;
        protected bool? FilterNotYetRated => ParentModel.FilterNotYetRated;
        protected Cert FilterCert => ParentModel.FilterCert;
        protected int FilterMaxDaysDefault => ParentModel.FilterMaxDaysDefault;
        protected int FilterMaxDays => ParentModel.FilterMaxDays;

        protected int Count => ParentModel.Count;
        protected int CountTypeFilm => ParentModel.CountTypeFilm;
        protected int CountTypeShort => ParentModel.CountTypeShort;
        protected int CountTypeSerie => ParentModel.CountTypeSerie;
        protected int CountMinRating5 => ParentModel.CountMinRating5;
        protected int CountMinRating6 => ParentModel.CountMinRating6;
        protected int CountMinRating65 => ParentModel.CountMinRating65;
        protected int CountMinRating7 => ParentModel.CountMinRating7;
        protected int CountMinRating8 => ParentModel.CountMinRating8;
        protected int CountMinRating9 => ParentModel.CountMinRating9;
        protected int CountNotOnImdb => ParentModel.CountNotOnImdb;
        protected int CountNotRatedOnImdb => ParentModel.CountNotRatedOnImdb;
        protected int CountNotYetRated => ParentModel.CountNotYetRated;
        protected int CountRated => ParentModel.CountRated;
        protected int CountCertNone => ParentModel.CountCertNone;
        protected int CountCertG => ParentModel.CountCertG;
        protected int CountCertPG => ParentModel.CountCertPG;
        protected int CountCertPG13 => ParentModel.CountCertPG13;
        protected int CountCertR => ParentModel.CountCertR;
        protected int CountCertNC17 => ParentModel.CountCertNC17;
        protected int CountCertOther => ParentModel.CountCertOther;
        protected int Count3days => ParentModel.Count3days;
        protected int Count5days => ParentModel.Count5days;
        protected int Count8days => ParentModel.Count8days;

        protected string ImdbUserId => ParentModel.ImdbUserId;
        protected DateTime? RefreshRequestTime => ParentModel.RefreshRequestTime;
        protected DateTime? LastRefreshRatingsTime => ParentModel.LastRefreshRatingsTime;

        public string FormatQueryString(bool? onlyHighlights, int typeMask, decimal? minrating, bool? notyetrated, Cert cert, int maxdays)
        {
            StringBuilder sb = null;
            if (onlyHighlights.HasValue)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append('&');
                sb.Append("onlyHighlights=");
                sb.Append(onlyHighlights.Value ? "true" : "false");
            }
            if (typeMask != FilterTypeMaskDefault)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append('&');
                sb.Append("typemask=");
                sb.Append(typeMask);
            }
            if (minrating.HasValue)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append('&');
                sb.Append("minrating=");
                sb.Append(minrating.Value.ToString(CultureInfo.InvariantCulture.NumberFormat));
            }
            if (notyetrated.HasValue)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append('&');
                if (notyetrated.Value)
                    sb.Append("notyetrated=true");
                else
                    sb.Append("notyetrated=false");
            }
            if (cert != Cert.all)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append('&');
                sb.Append("cert=");
                sb.Append(cert.ToString("g"));
            }
            if (maxdays != FilterMaxDaysDefault)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append('&');
                sb.Append("maxdays=");
                sb.Append(maxdays);
            }
            return sb?.ToString() ?? "";
        }

        public string FormatQueryStringWithOnlyHighlights(bool onlyHighlights)
        {
            //if (typeMask != 7 && FilterTypeMask != 7)
            //    typeMask = FilterTypeMask ^ typeMask;
            return FormatQueryString(onlyHighlights, FilterTypeMask, FilterMinRating, FilterNotYetRated, FilterCert, FilterMaxDays);
        }

        public string FormatQueryStringWithTypeMask(int typeMask)
        {
            //if (typeMask != 7 && FilterTypeMask != 7)
            //    typeMask = FilterTypeMask ^ typeMask;
            return FormatQueryString(FilterOnlyHighlights, typeMask, FilterMinRating, FilterNotYetRated, FilterCert, FilterMaxDays);
        }

        public string FormatQueryStringWithMinRating(decimal? minrating)
        {
            return FormatQueryString(FilterOnlyHighlights, FilterTypeMask, minrating, FilterNotYetRated, FilterCert, FilterMaxDays);
        }

        public string FormatQueryStringWithNotYetRated(bool? notyetrated)
        {
            return FormatQueryString(FilterOnlyHighlights, FilterTypeMask, FilterMinRating, notyetrated, FilterCert, FilterMaxDays);
        }

        public string FormatQueryStringWithToggleCert(Cert cert)
        {
            if (cert != Cert.all)
                cert = FilterCert ^ cert;
            return FormatQueryString(FilterOnlyHighlights, FilterTypeMask, FilterMinRating, FilterNotYetRated, cert, FilterMaxDays);
        }

        public string FormatQueryStringWithMaxDays(int maxdays)
        {
            return FormatQueryString(FilterOnlyHighlights, FilterTypeMask, FilterMinRating, FilterNotYetRated, FilterCert, maxdays);
        }

        public string GetFilterStyle(bool hasValue)
        {
            if (hasValue)
                return "primary";
            else
                return "default";
        }

        public string FormatCert(Cert cert)
        {
            StringBuilder sb = null;
            if ((cert & Cert.none) != 0)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(",");
                sb.Append("Zonder");
            }
            if ((cert & Cert.g) != 0)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(",");
                sb.Append("G");
            }
            if ((cert & Cert.pg) != 0)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(",");
                sb.Append("PG");
            }
            if ((cert & Cert.pg13) != 0)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(",");
                sb.Append("PG-13");
            }
            if ((cert & Cert.r) != 0)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(",");
                sb.Append("R");
            }
            if ((cert & Cert.nc17) != 0)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(",");
                sb.Append("NC-17");
            }
            if ((cert & Cert.other) != 0)
            {
                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(",");
                sb.Append("Overige");
            }

            return sb?.ToString() ?? "";
        }
    }
}