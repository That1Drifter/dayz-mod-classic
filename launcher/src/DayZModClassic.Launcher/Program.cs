using System;
using System.Threading;
using System.Windows.Forms;

namespace DayZModClassic.Launcher;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Logger.Exception("AppDomain.UnhandledException", ex);
        };
        Application.ThreadException += (_, e) =>
            Logger.Exception("Application.ThreadException", e.Exception);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Logger.PruneOldLogs(7);
        Logger.Info($"launcher start pid={Environment.ProcessId} os=\"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}\" runtime=\"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}\"");

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());

        Logger.Info("launcher exit");
    }
}
