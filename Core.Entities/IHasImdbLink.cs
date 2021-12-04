namespace FxMovies.Core.Entities;

public interface IHasImdbLink
{
    string Title { get; }
    int? Year { get; }
    Movie Movie { get; set; }
}