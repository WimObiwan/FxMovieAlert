using System.Collections.Generic;
using System.Threading.Tasks;
using FxMovies.Core.Entities;

namespace FxMovies.Core.Services;

public interface IMovieEventService
{
    string ProviderName { get; }
    string ProviderCode { get; }

    /// <summary>
    ///     The channels the provider offers its movies on, the channel that is easiest to get
    ///     to first: a movie that is offered on more than one of them is stored once, on the
    ///     first of those channels.
    /// </summary>
    IList<string> ChannelCodes { get; }

    Task<IList<MovieEvent>> GetMovieEvents();
}