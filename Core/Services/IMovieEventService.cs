using System.Collections.Generic;
using System.Threading.Tasks;
using FxMovies.Core.Entities;

namespace FxMovies.Core.Services;

public interface IMovieEventService
{
    string ProviderName { get; }
    string ChannelCode { get; }
    Task<IList<MovieEvent>> GetMovieEvents();
}