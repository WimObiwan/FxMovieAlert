using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    public class Record
    {
        public MovieEvent MovieEvent { get; set; }
        public UserRating UserRating { get; set; }
    }

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

    public class IndexModel : PageModel
    {
        public IList<Record> Records = new List<Record>();
        public string ImdbUserId = null;
        public DateTime? RefreshRequestTime = null;
        public DateTime? LastRefreshRatingsTime = null;
        public bool? LastRefreshSuccess = null;       

        public decimal? FilterMinRating = null;
        public bool FilterNotYetRated = false;
        public Cert FilterCert = Cert.all;

        public int Count = 0;
        public int CountMinRating5 = 0;
        public int CountMinRating6 = 0;
        public int CountMinRating7 = 0;
        public int CountMinRating8 = 0;
        public int CountMinRating9 = 0;
        public int CountNotYetRated = 0;
        public int CountCertNone = 0;
        public int CountCertG = 0;
        public int CountCertPG = 0;
        public int CountCertPG13 = 0;
        public int CountCertR = 0;
        public int CountCertNC17 = 0;
        public int CountCertOther = 0;

        public void OnGet(decimal? minrating = null, bool notyetrated = false, Cert cert = Cert.all)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            if (minrating.HasValue)
                FilterMinRating = minrating.Value;

            FilterNotYetRated = notyetrated;
            FilterCert = cert & Cert.all2;
            if (FilterCert == Cert.all2)
                FilterCert = Cert.all;

            ImdbUserId = configuration.GetSection("Temp")["ImdbUserId"];

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                var user = db.Users.Find(ImdbUserId);
                if (user != null)
                {
                    RefreshRequestTime = user.RefreshRequestTime;
                    LastRefreshRatingsTime = user.LastRefreshRatingsTime;
                    LastRefreshSuccess = user.LastRefreshSuccess;
                    user.Usages++;
                    user.LastUsageTime = DateTime.Now;
                    db.SaveChanges();
                }

                Count = db.MovieEvents.Count();
                CountMinRating5 = db.MovieEvents.Where(m => m.ImdbRating >= 50).Count();
                CountMinRating6 = db.MovieEvents.Where(m => m.ImdbRating >= 60).Count();
                CountMinRating7 = db.MovieEvents.Where(m => m.ImdbRating >= 70).Count();
                CountMinRating8 = db.MovieEvents.Where(m => m.ImdbRating >= 80).Count();
                CountMinRating9 = db.MovieEvents.Where(m => m.ImdbRating >= 90).Count();
                CountCertNone =  db.MovieEvents.Where(m => string.IsNullOrEmpty(m.Certification)).Count();
                CountCertG =  db.MovieEvents.Where(m => m.Certification == "US:G").Count();
                CountCertPG =  db.MovieEvents.Where(m => m.Certification == "US:PG").Count();
                CountCertPG13 =  db.MovieEvents.Where(m => m.Certification == "US:PG-13").Count();
                CountCertR =  db.MovieEvents.Where(m => m.Certification == "US:R").Count();
                CountCertNC17 =  db.MovieEvents.Where(m => m.Certification == "US:NC-17").Count();
                CountCertOther =  Count - CountCertNone - CountCertG - CountCertPG - CountCertPG13 - CountCertR - CountCertNC17;

                Records = (
                    from me in db.MovieEvents.Include(m => m.Channel)
                    join ur in db.UserRatings on me.ImdbId equals ur.ImdbMovieId into urGroup
                    from ur in urGroup.DefaultIfEmpty(null)
                    where 
                        (!FilterMinRating.HasValue || me.ImdbRating >= FilterMinRating.Value * 10)
                        &&
                        (!FilterNotYetRated || ur == null)
                        && 
                        (FilterCert == Cert.all || (ParseCertification(me.Certification) & FilterCert) != 0)
                    select new Record() { MovieEvent = me, UserRating = ur }
                ).ToList();

                CountNotYetRated = Records.Where(r => r.UserRating == null).Count();
                // MovieEvents = db.MovieEvents.Include(m => m.Channel)
                //     .Where(m => !MinRating.HasValue || m.ImdbRating >= MinRating.Value * 10)
                //     .ToList();
            }
        }

        private static Cert ParseCertification(string certification)
        {
            switch (certification)
            {
                case null:
                case "":
                    return Cert.none;
                case "US:G":
                    return Cert.g;
                case "US:PG":
                    return Cert.pg;
                case "US:PG-13":
                    return Cert.pg13;
                case "US:R":
                    return Cert.r;
                case "US:NC-17":
                    return Cert.nc17;
                default:
                    return Cert.other;
            }
        }
    }
}
