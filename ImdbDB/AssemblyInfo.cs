using System.Reflection;

[assembly:
    AssemblyVersion(
        ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.Commits)]

[assembly:
    AssemblyFileVersion(ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." +
                        ThisAssembly.Git.Commits)]

[assembly: AssemblyInformationalVersion(
    ThisAssembly.Git.SemVer.Major + "." +
    ThisAssembly.Git.SemVer.Minor + "." +
    ThisAssembly.Git.Commits + "-" +
    ThisAssembly.Git.Branch + "+" +
    ThisAssembly.Git.Commit)]

[assembly: AssemblyCompany("Fox Innovations")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyProduct("FilmOpTv")]

[assembly: AssemblyTitle("FilmOpTv Web")]

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
