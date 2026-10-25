using BAModManager.Core;
using BAModManager.Ui;

namespace BAModManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Files passed on the command line, e.g. when a .bamod is double-clicked
        var files = args.Where(a => File.Exists(a) || Directory.Exists(a)).ToArray();

        // Only one Mod Manager: a second launch hands its files to the open window
        using var mutex = new Mutex(true, $@"Local\{AppInfo.Id}.SingleInstance", out bool firstInstance);
        if (!firstInstance)
        {
            if (args.Contains(Loc.RestartArg))
            {
                // Restarting after a language change: wait for the old window to close
                try { mutex.WaitOne(10000); }
                catch (AbandonedMutexException) { /* the old one has exited */ }
            }
            else if (SingleInstance.SendToRunning(files))
            {
                return;
            }
        }

        Loc.SetLanguage(AppSettings.Load().Language);
        ApplicationConfiguration.Initialize();
        AppFonts.Load();
#if LAUNCHER
        // Mod Launcher: one PLAY button for one modpack. .bamod files only open with it when the player turns that on.
        Application.Run(new LauncherForm(ManagerConfig.Load(), files));
#else
        FileAssociation.Register();
        Application.Run(new MainForm(ManagerConfig.Load(), files));
#endif
        GC.KeepAlive(mutex);
    }
}
