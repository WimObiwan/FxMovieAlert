using System.Reflection;
using System.Text.RegularExpressions;
using FxMovies.Core;

namespace FxMovies.CoreTest;

public class VersionInfoTest
{
    [Fact]
    public void Test()
    {
        VersionInfo versionInfo = new(typeof(object).Assembly);
        Assert.NotNull(versionInfo.Version);
        var isMatch = Regex.IsMatch(versionInfo.Version!, @"^\d+\.\d+\.\d+\+.*$");
        Assert.True(isMatch, versionInfo.Version);

        isMatch = Regex.IsMatch(versionInfo.DotNetCoreVersion, @"^\.NET \d+\.\d+\.\d+$");
        Assert.True(isMatch, versionInfo.DotNetCoreVersion);

        VersionInfo versionInfo2 = new(new TestAssembly());
        Assert.Null(versionInfo2.Version);
    }

    private class TestAssembly : Assembly
    {
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return new Attribute[0];
        }
    }
}