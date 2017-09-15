using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FxMovies.FxMoviesDB;
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

            Console.WriteLine("Using database: {0}", connectionString);

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                DateTime now = DateTime.Now;

                // Remove all old MovieEvents
                {
                    var set = db.MovieEvents;
                    set.RemoveRange(set.Where(x => x.StartTime < now.Date));
                }

                int maxDays;
                if (!int.TryParse(configuration.GetSection("Grabber")["MaxDays"], out maxDays))
                    maxDays = 7;

                for (int days = 0; days <= maxDays; days++)
                {
                    DateTime date = now.Date.AddDays(days);
                    var movies = HumoGrabber.GetGuide(date).Result;

                    Console.WriteLine(date.ToString());
                    foreach (var movie in movies)
                    {
                        Console.WriteLine("{0} {1} {2} {4}", movie.Channel.Name, movie.Title, movie.Year, movie.Channel.Name, movie.StartTime);
                    }

                    var existingMovies = db.MovieEvents.Where(x => x.StartTime.Date == date);
                    Console.WriteLine("Existing movies: {0}", existingMovies.Count());
                    Console.WriteLine("New movies: {0}", movies.Count());

                    // Update channels
                    foreach (var channel in movies.Select(m => m.Channel).Distinct())
                    {
                        Channel existingChannel = db.Channels.Find(channel.Code);
                        if (existingChannel != null)
                        {
                            existingChannel.Name = channel.Name;
                            existingChannel.LogoS = channel.LogoS;
                            existingChannel.LogoM = channel.LogoM;
                            existingChannel.LogoL = channel.LogoL;
                            db.Channels.Update(existingChannel);
                            foreach (var movie in movies.Where(m => m.Channel == channel))
                                movie.Channel = existingChannel;
                        }
                        else
                        {
                            db.Channels.Add(channel);
                        }
                    }

                    // Remove exising movies that don't appear in new movies
                    {
                        var remove = existingMovies.Where(m1 => !movies.Any(m2 => m2.Id == m1.Id));
                        Console.WriteLine("Existing movies to be removed: {0}", remove.Count());
                        db.RemoveRange(remove);
                    }

                    // Update movies
                    foreach (var movie in movies)
                    {
                        var existingMovie = db.MovieEvents.Find(movie.Id);
                        if (existingMovie != null)
                        {
                            existingMovie.Title = movie.Title;
                            existingMovie.Year = movie.Year;
                            existingMovie.StartTime = movie.StartTime;
                            existingMovie.EndTime = movie.EndTime;
                            existingMovie.Channel = movie.Channel;
                            existingMovie.PosterS = movie.PosterS;
                            existingMovie.PosterM = movie.PosterM;
                            existingMovie.PosterL = movie.PosterL;
                            existingMovie.Duration = movie.Duration;
                            existingMovie.Genre = movie.Genre;
                            existingMovie.Content = movie.Content;
                        }
                        else
                        {
                            db.MovieEvents.Add(movie);
                        }
                    }

                    // {
                    //     set.RemoveRange(set.Where(x => x.StartTime.Date == date));
                    //     db.SaveChanges();
                    // }

                    db.SaveChanges();
                }
            }
        }
    }
}
