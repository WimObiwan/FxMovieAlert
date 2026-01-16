using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FxMovies.Site.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Site.Services;

public class SearchBotVerificationService : ISearchBotVerificationService
{
    private readonly ILogger<SearchBotVerificationService> _logger;
    private readonly SearchBotVerificationOptions _options;
    private readonly ConcurrentDictionary<string, (bool IsBot, DateTime ExpiresAt)> _cache = new();

    public SearchBotVerificationService(
        ILogger<SearchBotVerificationService> logger,
        IOptions<SearchBotVerificationOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<bool> IsVerifiedSearchBotAsync(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        // Check cache first
        if (_cache.TryGetValue(ipAddress, out var cachedResult))
        {
            if (DateTime.UtcNow < cachedResult.ExpiresAt)
            {
                _logger.LogDebug("Cache hit for IP {IpAddress}: {IsBot}", ipAddress, cachedResult.IsBot);
                return cachedResult.IsBot;
            }

            // Remove expired entry
            _cache.TryRemove(ipAddress, out _);
        }

        // Verify the IP address
        var isBot = await VerifySearchBotAsync(ipAddress);

        // Cache the result
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.CacheDurationMinutes);
        _cache[ipAddress] = (isBot, expiresAt);

        _logger.LogInformation("Verified IP {IpAddress} as search bot: {IsBot}", ipAddress, isBot);
        return isBot;
    }

    private async Task<bool> VerifySearchBotAsync(string ipAddress)
    {
        try
        {
            // Perform reverse DNS lookup
            var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
            var hostName = hostEntry.HostName.ToLowerInvariant();

            _logger.LogDebug("Reverse DNS for {IpAddress}: {HostName}", ipAddress, hostName);

            // Check if hostname matches known search bot patterns
            bool isKnownBot = false;
            string botProvider = string.Empty;

            // Google (Googlebot)
            if (hostName.EndsWith(".googlebot.com") || hostName.EndsWith(".google.com"))
            {
                isKnownBot = true;
                botProvider = "Google";
            }
            // Bing (Bingbot)
            else if (hostName.EndsWith(".search.msn.com"))
            {
                isKnownBot = true;
                botProvider = "Bing";
            }
            // Yahoo (Slurp)
            else if (hostName.EndsWith(".crawl.yahoo.net"))
            {
                isKnownBot = true;
                botProvider = "Yahoo";
            }
            // DuckDuckGo
            else if (hostName.EndsWith(".duckduckgo.com"))
            {
                isKnownBot = true;
                botProvider = "DuckDuckGo";
            }
            // Yandex
            else if (hostName.EndsWith(".yandex.com") || hostName.EndsWith(".yandex.ru") || hostName.EndsWith(".yandex.net"))
            {
                isKnownBot = true;
                botProvider = "Yandex";
            }
            // Baidu
            else if (hostName.EndsWith(".crawl.baidu.com") || hostName.EndsWith(".crawl.baidu.jp"))
            {
                isKnownBot = true;
                botProvider = "Baidu";
            }

            if (!isKnownBot)
            {
                _logger.LogDebug("Hostname {HostName} does not match known search bot patterns", hostName);
                return false;
            }

            // Perform forward DNS lookup to verify
            var forwardEntry = await Dns.GetHostEntryAsync(hostName);
            var isVerified = forwardEntry.AddressList.Any(a => a.ToString() == ipAddress);

            if (isVerified)
            {
                _logger.LogInformation("Verified {BotProvider} bot from IP {IpAddress} with hostname {HostName}",
                    botProvider, ipAddress, hostName);
            }
            else
            {
                _logger.LogWarning("Failed forward DNS verification for IP {IpAddress} claiming to be {BotProvider} with hostname {HostName}",
                    ipAddress, botProvider, hostName);
            }

            return isVerified;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to verify search bot for IP {IpAddress}", ipAddress);
            return false;
        }
    }
}
