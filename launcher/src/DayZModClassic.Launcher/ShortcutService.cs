using System;
using System.IO;

namespace DayZModClassic.Launcher;

public static class ShortcutService
{
    private static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "DayZ Mod Classic.lnk");

    public static bool DesktopShortcutExists() => File.Exists(ShortcutPath);

    public static void CreateDesktopShortcut(string exePath)
    {
        // WScript.Shell via late-bound COM; no interop assembly needed.
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell unavailable");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic lnk = shell.CreateShortcut(ShortcutPath);
            lnk.TargetPath = exePath;
            lnk.WorkingDirectory = Path.GetDirectoryName(exePath);
            lnk.IconLocation = exePath + ",0";
            lnk.Description = "DayZ Mod Classic launcher";
            lnk.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
        }
    }
}
