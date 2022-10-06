using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FxMovies.Core;
using Xunit;

namespace FxMovies.CoreTest;

public class VersionInfoTest
{
    [Fact]
    public void Test()
    {
        VersionInfo versionInfo = new(typeof(object).Assembly);
        Assert.NotNull(versionInfo.Version);
        bool isMatch = Regex.IsMatch(versionInfo.Version!, @"^\d+\.\d+\.\d+\+.*$");
        Assert.True(isMatch, versionInfo.Version);

        isMatch = Regex.IsMatch(versionInfo.DotNetCoreVersion, @"^\.NET \d+\.\d+\.\d+$");
        Assert.True(isMatch, versionInfo.DotNetCoreVersion);
    }
}