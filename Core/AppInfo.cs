namespace BAModManager.Core;

/// <summary>Which of the two programs this is (set at build time, see AppFlavor in BAModManager.csproj).</summary>
public static class AppInfo
{
#if LAUNCHER
    public const bool IsLauncher = true;
    /// <summary>Used for the settings folder, the single-instance lock and the .bamod file type.</summary>
    public const string Id = "BAModLauncher";
    public const string ShortName = "Mod Launcher";
#else
    public const bool IsLauncher = false;
    public const string Id = "BAModManager";
    public const string ShortName = "Mod Manager";
#endif
    public const string FullName = "Broken Arrow " + ShortName;
}
