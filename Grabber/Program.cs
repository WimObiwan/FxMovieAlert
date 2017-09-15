using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FxMovies.FxMoviesDB;
using Microsoft.Extensions.Configuration;

namespace FxMovies.Grabber
{
    class Program
    {
        static void Main(string[] args)
        {
            //UpdateDatabaseEpg();
            UpdateImdbData();
        }

        static void UpdateDatabaseEpg()
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

        static void UpdateImdbData()
        {
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.basics.tsv.gz title.basics.tsv.gz
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.ratings.tsv.gz title.ratings.tsv.gz
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                var movies = db.MovieEvents;

                var fileToDecompress = new FileInfo(configuration.GetSection("Grabber")["ImdbMoviesList"]);
                using (var originalFileStream = fileToDecompress.OpenRead())
                using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                using (var textReader = new StreamReader(decompressionStream))
                {
                    int count = 0;
                    string text;
                    while ((text = textReader.ReadLine()) != null)
                    {
                        count++;

                        foreach (var movie in movies)
                        {
                            if (text.Contains(movie.Title) && text.Contains(movie.Year.ToString()))
                            {
                                Console.WriteLine(text);
                            
                                string imdbId = text.Substring(0, 9);
                                if (movie.ImdbId == null)
                                    movie.ImdbId = text.Substring(0, 9);
                                else if (!movie.ImdbId.Equals(imdbId))
                                    Console.WriteLine ("Possible double entry with {0}", movie.ImdbId);
                            }
                        }
                    }

                    Console.WriteLine("IMDB movies scanned: {0}", count);
                }

                db.SaveChanges();
            }
        }
    }
}
