namespace FxMovies.Core.Entities;

public class ImdbMovieAlternative
{
    public int Id { get; set; }
    public ImdbMovie Movie { get; set; }
    public string AlternativeTitle { get; set; }
    public string Normalized { get; set; }
    public int MovieId { get; set; }
}
