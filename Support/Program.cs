using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LockScreenImages;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (!Debugger.IsAttached)
        {
            Application.ThreadException += Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }
        try
        {
            Application.Run(new MainForm());
        }
        catch (FileNotFoundException ex)
        {
            MessageForm.Show($"[ERROR] Make sure this file exists and try again…{Environment.NewLine}{Environment.NewLine}{ex.Message}", "Startup Error", MessageLevel.Error, false, false, null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex}", GetCurrentAssemblyName(), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #region [Event Hanlders]
    /// <summary>
    /// Domain unhandled exception
    /// </summary>
    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        if (ex != null)
        {
            Debug.WriteLine($"UnhandledException: {ex.Message}, IsTerminating: {e.IsTerminating}");
            Debug.WriteLine($"{Environment.StackTrace}");
        }
    }

    /// <summary>
    /// Domain thread exception
    /// </summary>
    static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
    {
        Debug.WriteLine($"ThreadException: {e.Exception.Message}");
        Debug.WriteLine($"{Environment.StackTrace}");
    }
    #endregion

    /// <summary>
    /// For local exception logging.
    /// </summary>
    public static void DebugLog(string message, LogLevel level = LogLevel.Informative, [CallerMemberName] string origin = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        message = $"[{DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}] ⇒ {level} ⇒ {System.IO.Path.GetFileName(filePath)} ⇒ {origin}(line{lineNumber}) ⇒ {message}";
        if (level <= LogLevel.Debug)
        {
            Debug.WriteLine($"{message}");
        }
        else
        {
            using (var sw = File.AppendText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Debug.log")))
            {
                sw.WriteLine(message);
            }
        }
    }

    #region [Reflection Helpers]
    /// <summary>
    /// Returns the declaring type's namespace.
    /// </summary>
    public static string GetCurrentNamespace() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace ?? "App";

    /// <summary>
    /// Returns the declaring type's full name.
    /// </summary>
    public static string GetCurrentFullName() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Assembly.FullName ?? "App";

    /// <summary>
    /// Returns the declaring type's assembly name.
    /// </summary>
    public static string GetCurrentAssemblyName() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "App";

    /// <summary>
    /// Returns the AssemblyVersion, not the FileVersion.
    /// </summary>
    public static Version GetCurrentAssemblyVersion() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
    #endregion
}

public enum LogLevel
{
    Debug = 1,
    Verbose = 2,
    Informative = 3,
    Warning = 4,
    Error = 5,
    Success = 6,
}