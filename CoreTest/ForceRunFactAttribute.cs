
namespace FxMovies.CoreTest;

public class ForceRunFactAttribute : FactAttribute
{
    public ForceRunFactAttribute(string filter)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("FORCE_RUN_TEST"), filter, StringComparison.InvariantCultureIgnoreCase))
            Skip = $"ForceRun - set FORCE_RUN_TEST={filter} to enable";
    }
}
