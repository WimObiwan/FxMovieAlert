using System.Reflection;

namespace FxMovies.Core;

public interface IVersionInfo
{
    string Version { get; }
    string DotNetCoreVersion { get; }
}

public class VersionInfo : IVersionInfo
{
    private string assemblyVersion;

    public VersionInfo(Assembly assemblyForVersion)
    {
        assemblyVersion = assemblyForVersion
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;
    }

    public string Version => assemblyVersion;

    public string DotNetCoreVersion => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
}