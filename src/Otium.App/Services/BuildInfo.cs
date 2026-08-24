using System.Reflection;

namespace Otium.App.Services;

public static class BuildInfo
{
#if OTIUM_DEVELOPMENT_BUILD
    public static bool IsDevelopmentBuild => true;
    public const string Flavor = "development";
#else
    public static bool IsDevelopmentBuild => false;
    public const string Flavor = "public";
#endif

    public static string Version
    {
        get
        {
            System.Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }
    }
}
