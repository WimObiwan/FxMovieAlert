using System.Threading.Tasks;

namespace FxMovies.Site.Services;

public interface ISearchBotVerificationService
{
    /// <summary>
    /// Checks if the given IP address belongs to a verified search bot (Google, Bing, etc.)
    /// </summary>
    /// <param name="ipAddress">The IP address to verify</param>
    /// <returns>True if the IP belongs to a verified search bot, false otherwise</returns>
    Task<bool> IsVerifiedSearchBotAsync(string ipAddress);
}
