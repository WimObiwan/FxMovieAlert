using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace FxMovies.Grabber
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            DateTime date = DateTime.Now.Date.AddDays(1);
            var movies = HumoGrabber.GetGuide(date).Result;
            var fxMoviesDB = new FxMoviesDB(connectionString);

            fxMoviesDB.RemoveForDate(date);

            Console.WriteLine(date.ToString());
            foreach (var movie in movies)
            {
                fxMoviesDB.Save(movie);

                Console.WriteLine("{0} {1} {2} {4} {5}", movie.Channel.Name, movie.Title, movie.Year, movie.Channel.Name, movie.StartTime, movie.EndTime);
            }
        }
    }
}
