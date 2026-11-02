using System.IO;
using System.Windows;
namespace Launcher.UI;
public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) => { Log(e.Exception); e.Handled = true; MessageBox.Show(e.Exception.Message, "OpenGDLauncher error"); Shutdown(-1); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
    }
    private static void Log(Exception? exception)
    {
        try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "launcher-error.log"), exception?.ToString() ?? "Unknown error"); } catch { }
    }
}
