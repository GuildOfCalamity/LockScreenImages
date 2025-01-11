using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Xml;

namespace LockScreenImages;

public sealed class AppSettingsManager
{
    const string VERSION = "1.0";
    const string EXTENSION = ".config.xml";
    private AppSettingsManager() { }

    #region [Backing Members]
    static AppSettingsManager _Settings = null;
    bool debugMode = false;
    bool firstRun = true;
    bool glassWindow = true;
    int inactivityTimeout = 15;
    int windowState = -1; // Maximized, Minimized, Normal
    int windowsDPI = 96;
    int lastCount = 0;
    double windowWidth = -1;
    double windowHeight = -1;
    double windowTop = -1;
    double windowLeft = -1;
    string theme = "Dark";
    string startupPosition = string.Empty; // FormStartPosition enum
    DateTime lastUse = DateTime.MinValue;
    #endregion

    #region [Public Properties]
    /// <summary>
    /// Static reference to this class.
    /// </summary>
    /// <remarks>
    /// The first time this property is used the existing settings will be 
    /// loaded via the <see cref="Load(object, string, string)"/> method.
    /// </remarks>
    public static AppSettingsManager Settings
    {
        get
        {
            if (_Settings == null)
            {
                _Settings = new AppSettingsManager();
                Load(_Settings, Location, VERSION);
            }
            return _Settings;
        }
    }
    public static string Location
    {
        get => Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? AppDomain.CurrentDomain.BaseDirectory), $"{Assembly.GetExecutingAssembly().GetName().Name}{EXTENSION}");
    }
    public static string Version { get => VERSION; }
    public static double WindowWidth { get => Settings.windowWidth; set => Settings.windowWidth = value; }
    public static double WindowHeight { get => Settings.windowHeight; set => Settings.windowHeight = value; }
    public static double WindowTop { get => Settings.windowTop; set => Settings.windowTop = value; }
    public static double WindowLeft { get => Settings.windowLeft; set => Settings.windowLeft = value; }
    public static int WindowState { get => Settings.windowState; set => Settings.windowState = value; }
    public static int WindowsDPI { get => Settings.windowsDPI; set => Settings.windowsDPI = value; }
    public static bool FirstRun { get => Settings.firstRun; set => Settings.firstRun = value; }
    public static bool DebugMode { get => Settings.debugMode; set => Settings.debugMode = value; }
    public static string Theme { get => Settings.theme; set => Settings.theme = value; }
    public static string StartupPosition { get => Settings.startupPosition; set => Settings.startupPosition = value; }
    public static DateTime LastUse { get => Settings.lastUse; set => Settings.lastUse = value; }
    public static int InactivityTimeout { get => Settings.inactivityTimeout; set => Settings.inactivityTimeout = value; }
    public static int LastCount { get => Settings.lastCount; set => Settings.lastCount = value; }
    public static bool GlassWindow { get => Settings.glassWindow; set => Settings.glassWindow = value; }
    #endregion

    #region [I/O Methods]
    /// <summary>
    /// Loads the specified file into the given class with the given version.
    /// </summary>
    /// <param name="classRecord">Class</param>
    /// <param name="path">File path</param>
    /// <param name="version">Version of class</param>
    /// <returns>true if class contains values from file, false otherwise</returns>
    public static bool Load(object classRecord, string path, string version)
    {
        try
        {
            Type recordType = classRecord.GetType();
            XmlDocument xmlDoc = new XmlDocument();
            XmlNode? rootNode = null;

            if (!File.Exists(path))
                return false;

            xmlDoc.Load(path);
            // The root must match the name of the class
            rootNode = xmlDoc.SelectSingleNode(recordType.Name);

            if (rootNode != null)
            {
                // check for correct version
                if (rootNode.Attributes.Count > 0 && rootNode.Attributes["version"] != null && rootNode.Attributes["version"].Value.Equals(version))
                {
                    XmlNodeList propertyNodes = rootNode?.SelectNodes("property");

                    Debug.WriteLine($"[INFO] Discovered {propertyNodes.Count} properties.");

                    // Do we have any properties to traverse?
                    if (propertyNodes != null && propertyNodes.Count > 0)
                    {
                        // Gather all properties of the provided class.
                        PropertyInfo[] properties = recordType.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance);

                        // Walk through each property in the provided class and try to match them with the XML data.
                        foreach (XmlNode node in propertyNodes)
                        {
                            try
                            {
                                var name = node.Attributes["name"].Value;
                                var data = node.FirstChild.InnerText;

                                foreach (PropertyInfo property in properties)
                                {
                                    if (property.Name.Equals(name))
                                    {
                                        try
                                        {
                                            // Attempt to use the type's Parse method with a string parameter.
                                            MethodInfo? method = property.PropertyType.GetMethod("Parse", new Type[] { typeof(string) });
                                            if (method != null)
                                            {
                                                // Property contains a parse.
                                                property.SetValue(classRecord, method.Invoke(property, new object[] { data }), null);
                                            }
                                            else
                                            {
                                                // If we don't have a reflected Parse method, then try to set the object directly.
                                                if (property.CanWrite)
                                                    property.SetValue(classRecord, data, null);
                                            }
                                            method = null;
                                        }
                                        catch (Exception ex)
                                        {
                                            Program.DebugLog($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: During load method reflection: {ex.Message}");
                                        }

                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Program.DebugLog($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: Property node issue: {ex.Message}");
                            }
                        }

                        return true;
                    }
                }
                else
                {
                    Program.DebugLog($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: Version \"{version}\" mismatch during load settings.");
                }
            }
            else
            {
                Program.DebugLog($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: Root name \"{recordType.Name}\" not found in settings.");
            }
        }
        catch (Exception ex)
        {
            Program.DebugLog($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: Unable to load settings \"{path}\", version {version}, error: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Saves the given class' properties to the given file with the given version.
    /// </summary>
    /// <param name="classRecord">Class to save</param>
    /// <param name="path">File path</param>
    /// <param name="version">Version of class</param>
    /// <returns>true if successful, false otherwise</returns>
    public static bool Save(object classRecord, string path, string version)
    {
        try
        {
            Type? recordType = classRecord.GetType();
            XmlDocument? xmlDoc = new XmlDocument();
            XmlDeclaration decl = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", "yes");
            XmlNode rootNode = xmlDoc.CreateElement(recordType.Name);
            XmlAttribute attrib = xmlDoc.CreateAttribute("version");
            XmlNode? propertyNode = null;
            XmlNode? valueNode = null;

            attrib.Value = version;
            rootNode.Attributes?.Append(attrib);

            // Gather all properties of the provided class.
            PropertyInfo[]? properties = recordType.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                if (property.CanWrite)
                {
                    try
                    {
                        propertyNode = xmlDoc.CreateElement("property");
                        valueNode = xmlDoc.CreateElement("value");

                        attrib = xmlDoc.CreateAttribute("name");
                        attrib.Value = property.Name;
                        propertyNode.Attributes?.Append(attrib);

                        attrib = xmlDoc.CreateAttribute("type");
                        attrib.Value = property.PropertyType.ToString();
                        propertyNode.Attributes?.Append(attrib);

                        if (property.GetValue(classRecord, null) != null)
                            valueNode.InnerText = $"{property?.GetValue(classRecord, null)}";

                        propertyNode.AppendChild(valueNode);
                        rootNode.AppendChild(propertyNode);
                    }
                    catch (Exception ex)
                    {
                        Program.DebugLog($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: Could not create property element: {ex.Message}");
                    }
                }
            }

            xmlDoc.AppendChild(decl);
            xmlDoc.AppendChild(rootNode);

            FileInfo info = new FileInfo(path);
            if (info != null && !info.Directory.Exists)
                info.Directory.Create();

            // Save the new XML data to disk.
            xmlDoc.Save(path);

            recordType = null;
            properties = null;
            xmlDoc = null;

            return true;
        }
        catch (Exception ex)
        {
            Program.DebugLog($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: Unable to save settings \"{path}\", version {version}, error: {ex.Message}");
        }

        return false;
    }
    #endregion

    #region [Overrides]
    public override string ToString() => "{" +
        "Top=" + WindowTop.ToString(CultureInfo.CurrentCulture) + "," +
        "Left=" + WindowLeft.ToString(CultureInfo.CurrentCulture) + "," +
        "Width=" + WindowWidth.ToString(CultureInfo.CurrentCulture) + "," +
        "Height=" + WindowHeight.ToString(CultureInfo.CurrentCulture) + "," +
        "DebugMode=" + DebugMode.ToString(CultureInfo.CurrentCulture) + "," +
        "FirstRun=" + FirstRun.ToString(CultureInfo.CurrentCulture) + "," +
        "LastUse=" + LastUse.ToString(CultureInfo.CurrentCulture) + "," +
        "WindowState=" + WindowState.ToString(CultureInfo.CurrentCulture) + "," +
        "InactivityTimeout=" + InactivityTimeout.ToString(CultureInfo.CurrentCulture) + "}";
    #endregion
}
