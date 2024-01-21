using System.Collections.Generic;
using System.Threading.Tasks;
using FxMovies.Core.Entities;

namespace FxMovies.Core.Services;

public interface IMovieEventService
{
    string ProviderName { get; }
    string ProviderCode { get; }
    IList<string> ChannelCodes { get; }
    Task<IList<MovieEvent>> GetMovieEvents();
}