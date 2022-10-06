using System.Reflection;
using System.Runtime.InteropServices;

namespace FxMovies.Core;

public interface IVersionInfo
{
    string? Version { get; }
    string DotNetCoreVersion { get; }
}

public class VersionInfo : IVersionInfo
{
    public VersionInfo(Assembly assemblyForVersion)
    {
        Version = assemblyForVersion
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
    }

    public string? Version { get; }

    public string DotNetCoreVersion => RuntimeInformation.FrameworkDescription;
}