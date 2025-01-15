using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LockScreenImages;

public enum eLogLevel
{
    Debug,
    Information,
    Important,
    Warning,
    Error,
    Fatal,
}

/// <summary>
/// Agnostic version of Cage.Logging.CageLog
/// </summary>
public class Logger
{
    #region [Members]
    static int levelMaxLength = 0;
    static string defaultComp = "LockScreenImages";
    static string logDrive = string.Empty;
    static string basepath = string.Empty;
    static object fileLock = new object();
    static object driveLock = new object();
    #endregion

    #region [Public Props]
    public static string CurrentFilePath
    {
        get => Path.Combine(CurrentLogDirectory, DateTime.Now.Date.ToString("dd") + ".log");
    }

    public static string CurrentLogDirectory
    {
        get
        {
            if (string.IsNullOrEmpty(basepath))
            {
                DriveInfo[] info = DriveInfo.GetDrives();
                if (info.Any(i => i.DriveType == DriveType.Fixed && i.IsReady == true && i.Name == @"D:\"))
                    basepath = @"D:\";
                else
                    basepath = @"C:\";
                basepath += Path.Combine("Logs", defaultComp);
                Directory.CreateDirectory(basepath);
            }
            return Path.Combine(basepath, String.Format("{0}\\{1}-{2}", DateTime.Now.Year.ToString("0000"), DateTime.Now.Month.ToString("00"), DateTime.Now.Date.ToString("MMMM")));
        }
    }
    #endregion

    #region [Public Methods]
    public static void Write(string applicationName, eLogLevel level, string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (string.IsNullOrEmpty(applicationName))
            applicationName = AppDomain.CurrentDomain.FriendlyName;
        
        string logString = "";
        string formatString = "{0:[yyyy-MM-dd hh:mm:ss.fff tt]} {1} {2} {3}";
        string clientToLogFile = applicationName.PadRight(10).Substring(0, 10);
        object[] objArray = { DateTime.Now, clientToLogFile, level.ToString("G").PadRight(levelMaxLength), message };
        
        try
        {
            logString = String.Format(formatString, objArray);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Logger.Write: {ex.Message}");
        }

        if (level == eLogLevel.Debug)
            Debug.WriteLine($"[DEBUG] {logString}");

        WriteToFile(defaultComp, logString);
    }
    #endregion

    #region [Private Methods]
    static bool WriteToFile(string compType, string message, bool insertTimeStamp = false)
    {
        try
        {
            lock (driveLock)
            {
                if (string.IsNullOrEmpty(logDrive))
                {
                    DriveInfo[] info = DriveInfo.GetDrives();
                    if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"D:\")))
                    {
                        logDrive = @"D:\Logs";
                    }
                    else
                    {
                        logDrive = @"C:\Logs";
                    }
                }
            }

            lock (fileLock)
            {
                string path = $@"{logDrive}\{compType}\{DateTime.Now.Year.ToString("0000")}\{DateTime.Now.Month.ToString("00")}-{DateTime.Now.Date.ToString("MMMM")}\";
                DirectoryInfo? dInfo = new DirectoryInfo(path);
                if (dInfo != null && !dInfo.Exists)
                    dInfo.Create();

                dInfo = null;
                path += compType + "_" + DateTime.Now.ToString("dd") + ".log";

                if (!File.Exists(path))
                {
                    CreateNewFile(path);
                    Thread.Sleep(1);
                }

                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    if (insertTimeStamp)
                        writer.WriteLine($"[{DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}] {message}");
                    else
                        writer.WriteLine(message);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] LogToFile: {ex.Message}");
            return false;
        }
    }

    static void CreateNewFile(string fullPath)
    {
        var tmp = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(tmp))
            Directory.CreateDirectory(tmp);
        File.Create(fullPath).Close();
        Write(defaultComp, eLogLevel.Information, $"-------------Created New Log File For v{Program.GetCurrentAssemblyVersion()}--------------");
        try { ThreadPool.QueueUserWorkItem(PurgeLogs); }
        catch { }
    }

    static void PurgeLogs(object state)
    {
        try
        {
            if (string.IsNullOrEmpty(basepath))
            {
                string temp = CurrentLogDirectory; // do not remove this line
            }
            CleanUpLogFiles(basepath, 365);
        }
        catch (Exception ex)
        {
            Write("TrioIQ", eLogLevel.Warning, $"Failure during log file purge. Exception: {ex.Message}");
        }
    }

    static void CleanUpLogFiles(string pathToDeleteFrom, int maxNumberOfDays)
    {
        if (!Directory.Exists(pathToDeleteFrom))
            return;

        Write(defaultComp, eLogLevel.Information, $"Purging logs older than {maxNumberOfDays} days.");
        string[] logFiles = Directory.GetFiles(pathToDeleteFrom, "*.log", SearchOption.AllDirectories);
        IEnumerable<string> topFiles = logFiles.OrderBy(files => files).Take(5000); //only remove 5K files per check
        string lastFilePath = "";
        foreach (string fn in topFiles)
        {
            lastFilePath = fn;
            DateTime dtOfLog = System.IO.File.GetCreationTime(fn);

            if ((DateTime.Now - dtOfLog).TotalDays > maxNumberOfDays)
            {
                Write(defaultComp, eLogLevel.Information, $"Deleting log {fn}");
                try { File.Delete(fn); }
                catch { }
            }
            Thread.Sleep(20);
        }

        if (!string.IsNullOrEmpty(lastFilePath))
        {
            if (Directory.GetFiles(Path.GetDirectoryName(lastFilePath)).Length < 1) //remove folder if no more files exist
            {
                try
                {
                    Directory.Delete(Path.GetDirectoryName(lastFilePath), true);
                    Write(defaultComp, eLogLevel.Information, $"Deleting directory {Path.GetDirectoryName(lastFilePath)}");
                }
                catch { }
            }
        }
    }
    #endregion
}
