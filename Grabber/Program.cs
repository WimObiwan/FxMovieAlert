using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace FxMovies.Grabber
{
    class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            for (int days = 0; days < 3; days++)
            {
                DateTime date = DateTime.Now.Date.AddDays(days);
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
}
