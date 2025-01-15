using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LockScreenImages;

/// <summary>
/// This has been reworked for a dark theme.
/// </summary>
/// <remarks>
/// Original source ⇒ https://github.com/keenthinker/WindowsLockScreenImages
/// </remarks>
public partial class MainForm : Form
{
    Image? appLogo;
    Image? imgBkgnd;
    Image? imgArrow;
    string assetsUserDirectory = string.Empty;
    FileInfo[] assets = Array.Empty<FileInfo>();
    ContextMenuStrip? contextMenu;
    StatusStrip? statusStrip;
    ListBox listBox = new();
    PictureBox? pictureBox;
    CustomPopupMenu? popupMenu;
    string deferredScriptName = "cleanup.bat";
    string copyFolderName = "ImageCopy";
    List<ListBoxMetaItem?> deferredRemovals = new();
    bool win7TitleBar = false;
    static Profile? profile;

    public MainForm()
    {
        InitializeComponent();

        appLogo = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico"));
        imgBkgnd = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Background.png"));
        imgArrow = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconOrb5.png"));
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Icon = appLogo.ToIcon(); // -or- this.Icon = new Icon(AppDomain.CurrentDomain.BaseDirectory +  @"\AppIcon.ico");
        this.Load += OnFormLoad;
        this.FormClosing += OnFormClosing;
        this.assets = GetContentDeliveryAssets();
        Application.ApplicationExit += OnApplicationExit;

        if (!this.assets.Any())
        {
            MessageForm.Show("No image files detected.\r\nApplication will exit after closing this dialog.", Program.GetCurrentAssemblyName(), MessageLevel.Error, true, false);
            Application.Exit();
        }

        #region [Create Form Controls]
        // status bar
        statusStrip = new StatusStrip();
        statusStrip.Renderer = new CustomToolStripRenderer();

        // label to show file information (name, size)
        var statusStripLabel = new ToolStripStatusLabel() 
        {
            Text = "🔔 Click an image from the list to view (courtesy of ContentDeliveryManager)",
            ForeColor = Color.WhiteSmoke,
        };
        statusStrip.Items.Add(statusStripLabel);
        
        // This is a place-holder for padding
        statusStrip.Items.Add(new ToolStripStatusLabel() 
        {
            Text = "",
            ForeColor = Color.WhiteSmoke, 
            Spring = true, 
            TextAlign = ContentAlignment.MiddleRight 
        });
        
        var statusStripSaveButton = new ToolStripButton()
        {
            Text = "Save selected image",
            ForeColor = Color.WhiteSmoke,
            Image = imgArrow
        };

        // NOTE: If adding a ToolStripSplitButton you can add items to the button's drop-down.
        //statusStripSaveButton.DropDownItems.Add("SubItem 1", imgBkgnd, OnSubItemClick);
        //statusStripSaveButton.DropDownItems.Add("SubItem 2", imgBkgnd, OnSubItemClick);
        //statusStripSaveButton.DropDownItems.Add("SubItem 3", imgBkgnd, OnSubItemClick);

        statusStrip.Items.Add(statusStripSaveButton);
        statusStrip.Items.Add(new ToolStripSeparator());
        
        var statusStripOpenButton = new ToolStripButton()
        {
            Text = "Open images folder",
            ForeColor = Color.WhiteSmoke,
            Image = imgArrow
        };
        
        statusStrip.Items.Add(statusStripOpenButton);
        statusStrip.Items.Add(new ToolStripSeparator());

        var statusStripCopyButton = new ToolStripButton()
        {
            Text = "Copy all images",
            ForeColor = Color.WhiteSmoke,
            Image = imgArrow
        };

        statusStrip.Items.Add(statusStripCopyButton);

        // split panel
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6
        };
        
        // right = image preview
        pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        popupMenu = new CustomPopupMenu(new Dictionary<string, Image>
        {
            { "Show file info", imgArrow },
            { "Make desktop background", imgArrow },
            { "App setting count", imgArrow },
            { "Delete on exit", imgArrow },
        }, new Font("Calibri", 12));
        popupMenu.MenuItemClicked += CustomPopupMenuOnMenuItemClicked;

        // Initialize the ContextMenuStrip
        contextMenu = new ContextMenuStrip() 
        { 
            BackColor = Color.Black, 
            ForeColor = Color.WhiteSmoke 
        };
        var cmi1 = contextMenu.Items.Add("Show file info", imgArrow, OnInfoClick);
        cmi1.BackgroundImage = imgBkgnd;
        cmi1.BackgroundImageLayout = ImageLayout.Stretch;
        cmi1.MouseEnter += StatusStripButtonOnMouseEnter;
        cmi1.MouseLeave += StatusStripButtonOnMouseLeave;

        var cmi2 = contextMenu.Items.Add("Delete on exit", imgArrow, OnRemoveFromListClick);
        cmi2.BackgroundImage = imgBkgnd;
        cmi2.BackgroundImageLayout = ImageLayout.Stretch;
        cmi2.MouseEnter += StatusStripButtonOnMouseEnter;
        cmi2.MouseLeave += StatusStripButtonOnMouseLeave;

        // left = list of files
        listBox = new ListBox
        {
            Font = new Font("Consolas", 10),
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = Color.WhiteSmoke,
            Dock = DockStyle.Fill,
            ContextMenuStrip = contextMenu
        };

        // OPTIONAL: Custom popup menu instead on ContextMenuStrip
        pictureBox.MouseDown += (s, e) =>
        {
            if (e.Clicks == 1 && e.Button == MouseButtons.Right)
            {
                var mp = listBox.PointToClient(Cursor.Position);
                popupMenu.Show(this, new Point(mp.X + 10, mp.Y));
            }
        };

        ToolTip tt = new ToolTip() { IsBalloon = true, AutoPopDelay = 5000, InitialDelay = 500, ReshowDelay = 500, ShowAlways = true };
        tt.SetToolTip(listBox, assetsUserDirectory);

        foreach (var fileInfo in this.assets)
        {
            listBox.Items.Add(new ListBoxMetaItem(fileInfo));
        }
        #endregion

        #region [Configure Control Events]
        statusStripSaveButton.MouseEnter += StatusStripButtonOnMouseEnter;
        statusStripSaveButton.MouseLeave += StatusStripButtonOnMouseLeave;
        statusStripOpenButton.MouseEnter += StatusStripButtonOnMouseEnter;
        statusStripOpenButton.MouseLeave += StatusStripButtonOnMouseLeave;
        statusStripCopyButton.MouseEnter += StatusStripButtonOnMouseEnter;
        statusStripCopyButton.MouseLeave += StatusStripButtonOnMouseLeave;

        statusStripOpenButton.Click += (s, e) =>
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                Arguments = this.assetsUserDirectory,
                FileName = "explorer.exe"
            };
            Process.Start(startInfo);
        };

        statusStripSaveButton.Click += (s, e) =>
        {
            var selectedIndex = listBox.SelectedIndex;
            if (selectedIndex >= 0)
            {
                var metaItem = listBox.Items[selectedIndex] as ListBoxMetaItem;
                if (metaItem is not null)
                {
                    var fullFileName = metaItem.Info?.FullName;
        
                    var saveFileDialog = new SaveFileDialog
                    {
                        FileName = $"{metaItem.Info?.Name}.jpg",
                        Filter = "JPEG (*.jpg)|*.jpeg,*.jpg",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    };
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        if (File.Exists(fullFileName))
                        {
                            Image.FromFile(fullFileName).Save(saveFileDialog.FileName, ImageFormat.Jpeg);
                        }
                    }
                }
            }
            else
            {
                MessageForm.Show("You must first click on an image from the list to select it and then save it.", Program.GetCurrentAssemblyName(), MessageLevel.Warning, true, false, TimeSpan.FromSeconds(5));
            }
        };

        statusStripCopyButton.Click += (s, e) =>
        {
            try
            {
                string currentFile = string.Empty;
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, copyFolderName));
                foreach (var file in this.assets)
                {
                    currentFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, copyFolderName, $"{file.Name}.{(file.FullName.DetermineType())}");
                    File.Copy(file.FullName, currentFile, true);
                    ((ToolStripStatusLabel)statusStrip.Items[0]).Text = $"✔️ Copied {file.Name}";
                }
                // After completion, open destination using File Explorer.
                Process.Start($"explorer.exe", $"/select,{currentFile}");
            }
            catch (Exception ex)
            {
                MessageForm.Show($"Copy failed: {ex.Message}", Program.GetCurrentAssemblyName(), MessageLevel.Warning, true, false, TimeSpan.FromSeconds(6));
            }
        };

        listBox.SelectedIndexChanged += (s, e) =>
        {
            var selectedIndex = listBox.SelectedIndex;
            if (selectedIndex >= 0)
            {
                var metaItem = listBox.Items[selectedIndex] as ListBoxMetaItem;
                if (metaItem is not null)
                {
                    var fullFileName = metaItem.Info?.FullName;
                    if (File.Exists(fullFileName))
                    {
                        pictureBox.Image = Image.FromFile(fullFileName);
                    }
                    var filenameLength = (metaItem.Info is not null) ? metaItem.Info.Name.Length : 0;
                    ((ToolStripStatusLabel)statusStrip.Items[0]).Text = $"🔔 {metaItem.Info?.Name.Truncate(32)} ({metaItem.HumanReadableSize()})";
                }
                else
                {
                    ((ToolStripStatusLabel)statusStrip.Items[0]).Text = $"🔔 {nameof(ListBoxMetaItem)} was null for this image.";
                }
            }
        };
        #endregion

        // Left side should be smaller than right side.
        splitContainer.SplitterDistance = 12;

        // Add controls to split container.
        splitContainer.Panel1.Controls.Add(listBox);
        splitContainer.Panel2.Controls.Add(pictureBox);

        // Add containers to main form.
        this.Controls.Add(splitContainer);
        this.Controls.Add(statusStrip);

        // NOTE: There seems to be a bug with setting the background color on status strip
        //       buttons, so I'm including a workaround via the CustomToolStripRenderer.
        //statusStrip.Items[0].BackColor = Color.DarkGreen;
        //statusStrip.Items[1].BackColor = Color.DarkGreen;
        //statusStrip.Items[2].BackColor = Color.DarkGreen;
        //statusStrip.Items[3].BackColor = Color.DarkGreen;

        #region [Acrylic background]
        // This will determine the glass accent color (does not support alpha).
        this.BackColor = Color.FromArgb(20, 20, 30);
        if (DwmHelper.IsDWMCompositionEnabled())
        {   
            // If the version is not being observed correctly make sure to add "App.Manifest" to the project.
            Debug.WriteLine($"[INFO] OSVersion.Major={Environment.OSVersion.Version.Major}");
            if (Environment.OSVersion.Version.Major > 6)
            {
                if (AppSettingsManager.GlassWindow)
                    DwmHelper.Windows10EnableBlurBehind(this.Handle);
                else
                    DwmHelper.Windows10EnableTransparentGradient(this.Handle, alpha: 0xAF);
            }
            else
            {
                DwmHelper.Windows10EnableTranslucent(this.Handle);
            }

            if (win7TitleBar)
            {   // Causes the title bar to revert to Windows 7 style.
                DwmHelper.WindowDisableRendering(this.Handle); 
                Debug.WriteLine($"[INFO] Form's border style is {this.FormBorderStyle}");
            }
            else
            {   // Set drop shadow if we're a border-less form
                if (this.FormBorderStyle == FormBorderStyle.None)
                    DwmHelper.WindowBorderlessDropShadow(this.Handle, 3);
            }
        }
        #endregion
    }

    /// <summary>
    /// This will behave slightly different on Win10 vs Win11.
    /// </summary>
    void StatusStripButtonOnMouseEnter(object? sender, EventArgs e)
    {
        switch (sender)
        {
            case ToolStripSplitButton tssb:
                tssb.ForeColor = Color.FromArgb(20, 20, 30);
                break;
            case ToolStripItem tsi:
                tsi.ForeColor = Color.FromArgb(20,20,30);
                //tsi.Font = new Font("Calibri", 10, FontStyle.Bold);
                break;
            default:
                Debug.WriteLine($"Undefined type: {sender?.GetType()}");
                break;
        }
    }

    /// <summary>
    /// This will behave slightly different on Win10 vs Win11.
    /// </summary>
    void StatusStripButtonOnMouseLeave(object? sender, EventArgs e)
    {
        switch (sender)
        {
            case ToolStripSplitButton tssb:
                tssb.ForeColor = Color.WhiteSmoke;
                break;
            case ToolStripItem tsi:
                tsi.ForeColor = Color.WhiteSmoke;
                //tsi.Font = new Font("Calibri", 10, FontStyle.Regular);
                break;
            default:
                Debug.WriteLine($"Undefined type: {sender?.GetType()}");
                break;
        }
    }

    void OnSubItemClick(object? sender, EventArgs e)
    {
        Debug.WriteLine($"[INFO] SubItemClick: {sender}");
    }

    FileInfo[] GetContentDeliveryAssets()
    {
        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        //const string directory = @"C:\Users\USERNAME\AppData\Local\Packages\Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy\LocalState\Assets";
        var directory = Path.Combine(userDirectory, @"AppData\Local\Packages\Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy\LocalState\Assets");
        this.assetsUserDirectory = directory;
        if (!Directory.Exists(directory))
        {
            MessageForm.Show($"Directory '{directory}' does not exist!", Program.GetCurrentAssemblyName(), MessageLevel.Warning, true, false, TimeSpan.FromSeconds(5));
            return this.assets;
        }
        return new DirectoryInfo(directory).GetFiles();
    }

    void OnInfoClick(object? sender, EventArgs e)
    {
        if (listBox.SelectedItem != null && listBox.SelectedIndex >= 0)
        {
            var metaItem = listBox.Items[listBox.SelectedIndex] as ListBoxMetaItem;
            if (metaItem == null) { return; }
            MessageForm.Show($"Name: {metaItem.Info?.Name.Truncate(32)}\r\nLastWrite: {metaItem.Info?.LastWriteTime}\r\nSize: {metaItem.Info?.Length.HumanReadableSize()}\r\nImage type: {metaItem.Info?.FullName.DetermineType()}", $"Details", MessageLevel.Info, true, false, TimeSpan.FromSeconds(9));
        }
        else
        {
            MessageForm.Show($"Please select an image from the list.", $"Notice", MessageLevel.Warning, true, false, TimeSpan.FromSeconds(3));
        }
    }

    void OnRemoveFromListClick(object? sender, EventArgs e)
    {
        if (listBox.SelectedItem != null && listBox.SelectedIndex >= 0)
        {
            //ShowQuestionDialog("Are you sure?");
            var selected = listBox.SelectedItem as ListBoxMetaItem;
            try
            {
                deferredRemovals.Add(selected);
                listBox.Items.Remove(listBox.SelectedItem);
            }
            catch (Exception ex)
            {
                MessageForm.Show($"{selected.Info?.FullName} could not be removed.\r\n{ex.Message}", $"Details", MessageLevel.Error, true, false, TimeSpan.FromSeconds(9));
            }
        }
        else
        {
            MessageForm.Show($"Please select an image from the list.", $"Notice", MessageLevel.Warning, true, false, TimeSpan.FromSeconds(3));
        }
    }

    void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{deferredScriptName}")))
                File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{deferredScriptName}"));

            #region [Application Settings]
            if (this.WindowState == FormWindowState.Normal)
            {
                AppSettingsManager.WindowLeft = this.Left;
                AppSettingsManager.WindowTop = this.Top;
                AppSettingsManager.WindowHeight = this.Height;
                AppSettingsManager.WindowWidth = this.Width;
                AppSettingsManager.StartupPosition = $"{this.StartPosition}";
            }
            AppSettingsManager.WindowState = (int)this.WindowState;
            if (AppSettingsManager.LastUse == DateTime.MinValue)
                Debug.WriteLine($"[INFO] {Program.GetCurrentAssemblyName()} has never been run.");
            else
                Debug.WriteLine($"[INFO] {Program.GetCurrentAssemblyName()} was last run on {AppSettingsManager.LastUse}");

            Debug.WriteLine($"[INFO] Last asset count was {AppSettingsManager.LastCount}.");
            #endregion

            #region [Profile Encryption Test]
            //profile = new Serializer().Load(Path.Combine(Environment.CurrentDirectory, "profile.json"));
            //if (profile == null || string.IsNullOrEmpty(profile.Title)) {
            //    profile = new Profile() {
            //        Title = $"{Program.GetCurrentAssemblyName()}",
            //        LastUse = DateTime.Now.ToString(), LastCount = "0",
            //        APIKey = "super secret value here",
            //        PositionX = "200", PositionY = "200",
            //    };
            //    Serializer.Save(Path.Combine(Environment.CurrentDirectory, "profile.json"), profile);
            //}
            #endregion
        }
        catch (Exception) { }
    }

    void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        Debug.WriteLine($"[INFO] FormClosing {DateTime.Now.ToString("hh:mm:ss.fff tt")}");

        AppSettingsManager.LastCount = this.assets.Length;
        AppSettingsManager.LastUse = DateTime.Now;
        AppSettingsManager.Save(AppSettingsManager.Settings, AppSettingsManager.Location, AppSettingsManager.Version);

        if (deferredRemovals.Count > 0)
        {
            // Create batch file for cleanup
            using (StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + $"\\{deferredScriptName}", false, Encoding.ASCII))
            {
                sw.WriteLine("@ECHO OFF");
                sw.WriteLine($"ECHO {Program.GetCurrentAssemblyName()} scheduled clean-up for {DateTime.Now.ToLongTimeString()} on {DateTime.Now.ToLongDateString()}");
                foreach (var item in deferredRemovals)
                {
                    if (item == null) { continue; }
                    var output = $"del \"{item.Info?.FullName}\"";
                    sw.WriteLine(output);
                }
            }
        }
    }

    void OnApplicationExit(object? sender, EventArgs e)
    {
        if (deferredRemovals.Count > 0)
        {
            Logger.Write("LockScreenImages", eLogLevel.Information, $"Invoking deferred cleanup at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
            //Task.Run(() =>
            //{
                // Launch batch file
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    FileName = $"{deferredScriptName}",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    //CreateNoWindow = true,
                };
                Process.Start(startInfo);
            //});
        }
    }

    void ShowQuestionDialog(string question, string title = "Question", Action? yes = null, Action? no = null)
    {
        var dr = QuestionForm.Show(question, title, true);
        if (dr == DialogResult.No)
            no?.Invoke();
        else if (dr == DialogResult.Yes)
            yes?.Invoke();
    }

    /// <summary>
    /// Handle the clicked menu item from our <see cref="CustomPopupMenu"/>.
    /// </summary>
    void CustomPopupMenuOnMenuItemClicked(object? sender, EventArgs e)
    {
        var cea = e as MenuItemClickedEventArgs;
        Debug.WriteLine($"[INFO] You selected \"{cea.ItemText}\"");
        if (cea.ItemText.Contains("info", StringComparison.OrdinalIgnoreCase) && listBox.SelectedItem != null && listBox.SelectedIndex >= 0)
        {
            var metaItem = listBox.Items[listBox.SelectedIndex] as ListBoxMetaItem;
            if (metaItem == null) { return; }
            MessageForm.Show($"Name: {metaItem.Info?.Name.Truncate(32)}\r\nLastWrite: {metaItem.Info?.LastWriteTime}\r\nSize: {metaItem.Info?.Length.HumanReadableSize()}\r\nImage type: {metaItem.Info?.FullName.DetermineType()}", $"Details", MessageLevel.Info, true, false, TimeSpan.FromSeconds(9));
        }
        else if (cea.ItemText.Contains("delete", StringComparison.OrdinalIgnoreCase) && listBox.SelectedItem != null && listBox.SelectedIndex >= 0)
        {
            var metaItem = listBox.Items[listBox.SelectedIndex] as ListBoxMetaItem;
            if (metaItem == null) { return; }
            ShowQuestionDialog($"The file '{metaItem.Info.Name}' will be removed upon exit.{Environment.NewLine}{Environment.NewLine}Are you sure?", "Remove?", delegate() 
            {
                var selected = listBox.SelectedItem as ListBoxMetaItem;
                try
                {
                    deferredRemovals.Add(selected);
                    listBox.Items.Remove(listBox.SelectedItem);
                }
                catch (Exception ex)
                {
                    MessageForm.Show($"{selected.Info?.FullName} could not be removed.\r\n{ex.Message}", $"Details", MessageLevel.Error, true, false, TimeSpan.FromSeconds(9));
                }
            }, delegate() 
            {
            ((ToolStripStatusLabel)statusStrip.Items[0]).Text = $"🔔 Delete was canceled by user.";
            });
        }
        else if (cea.ItemText.Contains("count", StringComparison.OrdinalIgnoreCase))
        {
            MessageForm.Show($"Last Count: {AppSettingsManager.LastCount}\r\nLast Used: {AppSettingsManager.LastUse}\r\nDebug Mode: {AppSettingsManager.DebugMode}", $"Details", MessageLevel.Info, true, false, TimeSpan.FromSeconds(10));
        }
        else if (cea.ItemText.Contains("background", StringComparison.OrdinalIgnoreCase))
        {
            var selected = listBox.SelectedItem as ListBoxMetaItem;
            if (selected == null) { return; }
            ShowQuestionDialog($"Are you sure you want to change your desktop wallpaper?", "Replace?", delegate ()
            {
                try
                {
                    if (File.Exists(selected.Info.FullName))
                    {
                        _ = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, selected.Info.FullName, SPIF_UPDATEINIFILE);
                        ((ToolStripStatusLabel)statusStrip.Items[0]).Text = $"🔔 Desktop wallpaper updated";
                    }
                }
                catch (Exception ex)
                {
                    MessageForm.Show($"{selected.Info?.FullName} could not be used as wallpaper.\r\n{ex.Message}", $"Details", MessageLevel.Error, true, false, TimeSpan.FromSeconds(9));
                }
            }, delegate ()
            {
                ((ToolStripStatusLabel)statusStrip.Items[0]).Text = $"🔔 Desktop wallpaper change canceled by user";
            });
        }
    }

    #region [Win32 API]
    /// <summary>
    /// SystemParametersInfo reads or sets information about numerous settings in Windows.
    /// These include Windows's accessibility features as well as various settings for other things.
    /// The exact behavior of the function depends on the flag passed as uAction.
    /// All sizes and dimensions used by this function are measured in pixels.
    /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa
    /// </summary>
    /// <param name="uiAction">The exact behavior of the function depends on the SPI flag passed as uAction.</param>
    /// <param name="uiParam">The purpose of this parameter varies with uAction. </param>
    /// <param name="pvParam">The purpose of this parameter varies with uAction. In VB, if this is to be set as a string or to 0, the ByVal keyword must preceed it.</param>
    /// <param name="fWinIni">Zero or more of the following flags specifying the change notification to take place. Generally, this can be set to 0 if the function merely queries information, but should be set to something if the function sets information.
    ///   SPIF_SENDWININICHANGE = 0x02 (broadcast the change made by the function to all running programs)
    ///   SPIF_UPDATEINIFILE    = 0x01 (save the change made by the function to the user profile)
    /// </param>
    /// <returns></returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.I4)]
    static extern Int32 SystemParametersInfo(UInt32 uiAction, UInt32 uiParam, String pvParam, UInt32 fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.I4)]
    static extern Int32 SystemParametersInfo(UInt32 uiAction, UInt32 uiParam, ref UInt32 pvParam, UInt32 fWinIni);

    // Set the current desktop wallpaper bitmap. uiParam must be 0. pvParam is a String holding the filename of the bitmap file to use as the wallpaper. 
    static UInt32 SPI_SETDESKWALLPAPER = 20;
    // Determine if the warning beeper is on or off. uiParam must be 0. pvParam is a Long-type variable which receives 0 if the warning beeper is off, or a non-zero value if it is on. 
    static UInt32 SPI_GETBEEP = 1;
    // Determine if fast Alt-Tab task switching is enabled. uiParam must be 0. pvParam is a Long-type variable which receives 0 if fast task switching is not enabled, or a non-zero value if it is. 
    static UInt32 SPI_GETFASTTASKSWITCH = 35;
    // Determine whether font smoothing is enabled or not. uiParam must be 0. pvParam is a Long-type variable which receives 0 if font smoothing is not enabled, or a non-zero value if it is. 
    static UInt32 SPI_GETFONTSMOOTHING = 74;
    static UInt32 SPIF_UPDATEINIFILE = 0x1;
    static UInt32 SPIF_SENDWININICHANGE = 0x2;
    #endregion

    #region [Thread-safe Control Methods]
    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="pb"><see cref="ProgressBar"/></param>
    /// <param name="value">the new value</param>
    public void SetValue(ProgressBar pb, int value)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SetValue(pb, value)));
        else
            pb.Value = value;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="pb"><see cref="ProgressBar"/></param>
    /// <param name="value">the new value</param>
    public void IncrementValue(ProgressBar pb, int value)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => IncrementValue(pb, value)));
        else
            pb.Increment(value);
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="pb"><see cref="ProgressBar"/></param>
    /// <returns>true if visible, false otherwise</returns>
    public bool GetIsVisible(ProgressBar pb)
    {
        if (InvokeRequired)
        {
            return (bool)Invoke(new Func<bool>(() => GetIsVisible(pb)));
            // NOTE: It's better to use the TaskCompletionSource version instead of this:
            //return (bool)BeginInvoke(new Action(() => GetIsVisible(pb))).AsyncState;
        }
        else
        {
            try { return pb.Visible; }
            catch { return false; }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="pb"><see cref="PictureBox"/></param>
    /// <param name="image"><see cref="System.Drawing.Image"/></param>
    public void SetImage(PictureBox pb, System.Drawing.Image image)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SetImage(pb, image)));
        else
            pb.Image = image;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void SetPictureBoxSizeMode(PictureBox pb, PictureBoxSizeMode sizeMode)
    {
        if (pb.InvokeRequired)
            pb.Invoke(new Action(() => pb.SizeMode = sizeMode));
        else
            pb.SizeMode = sizeMode;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="cb"><see cref="ComboBox"/></param>
    /// <param name="item"><see cref="object"/></param>
    public void AddItem(ComboBox cb, object item)
    {
        if (InvokeRequired)
            cb.Invoke(new Action(() => cb.Items.Add(item)));
        else
            cb.Items.Add(item);
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void SetFocus(Control ctrl)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SetFocus(ctrl)));
        else
            ctrl.Focus();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    /// <param name="text">string data</param>
    public void SetText(Control ctrl, string text)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SetText(ctrl, text)));
        else
            ctrl.Text = text;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void HideControl(Control ctrl)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => HideControl(ctrl)));
        else
            ctrl.Hide();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void ShowControl(Control ctrl)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ShowControl(ctrl)));
        else
            ctrl.Show();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void SelectControl(Control ctrl)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SelectControl(ctrl)));
        else
            ctrl.Select();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void UpdateControl(Control ctrl)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => UpdateControl(ctrl)));
        else
            ctrl.Update();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void InvalidateControl(Control ctrl)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => InvalidateControl(ctrl)));
        else
            ctrl.Invalidate();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void RefreshControl(Control ctrl)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => RefreshControl(ctrl)));
        else
            ctrl.Refresh();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void SetControlCursor(Control ctrl, Cursor cursor)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SetControlCursor(ctrl, cursor)));
        else
        {
            if (cursor != null)
                ctrl.Cursor = cursor;
            else
                ctrl.Cursor = System.Windows.Forms.Cursor.Current; // ctrl.Cursor = Cursors.Arrow

            #region [loading from stream]
            //using (MemoryStream cursorStream = new MemoryStream(Resources.pointer1))
            //{
            //    ctrl.Cursor = new System.Windows.Forms.Cursor(cursorStream));
            //}

            //using (Stream stream = this.GetType().Assembly.GetManifestResourceStream($"{Program.GetCurrentNamespace()}.Resources.pointer1.cur"))
            //{
            //    ctrl.Cursor = new System.Windows.Forms.Cursor(stream);
            //}
            #endregion
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public System.Drawing.Rectangle GetControlBounds(Control ctrl)
    {
        if (InvokeRequired)
        {
            return (System.Drawing.Rectangle)Invoke(new Func<System.Drawing.Rectangle>(() => GetControlBounds(ctrl)));
        }
        else
        {
            try { return Screen.FromControl(ctrl).Bounds; }
            catch { return new System.Drawing.Rectangle(); }
            //var screen = Screen.FromControl(ctrl);
            //ctrl.Location = new Point(screen.WorkingArea.Width / 2 - this.Width / 2, screen.WorkingArea.Height / 2 - this.Height / 2);
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public System.Drawing.Rectangle GetControlWorkingArea(Control ctrl)
    {
        if (InvokeRequired)
        {
            return (System.Drawing.Rectangle)Invoke(new Func<System.Drawing.Rectangle>(() => GetControlWorkingArea(ctrl)));
        }
        else
        {
            try { return Screen.FromControl(ctrl).WorkingArea; }
            catch { return new System.Drawing.Rectangle(); }
            //var screen = Screen.FromControl(ctrl);
            //ctrl.Location = new Point(screen.WorkingArea.Width / 2 - (this.Width / 2), screen.WorkingArea.Height / 2 - (this.Height / 2));
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    /// <remarks>centering based on main form</remarks>
    public void CenterControl(Control ctrl)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => CenterControl(ctrl)));
        }
        else
        {
            ctrl.BringToFront();
            ctrl.Location = new Point(this.Width / 2 - (ctrl.Width / 2), this.Height / 2 - (ctrl.Height / 2));
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    public void SetControlLocation(Control ctrl, System.Drawing.Point point)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SetControlLocation(ctrl, point)));
        else
            ctrl.Location = point;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    /// <param name="state">true=visible, false=invisible</param>
    public void ToggleVisible(Control ctrl, bool state)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ToggleVisible(ctrl, state)));
        else
            ctrl.Visible = state;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    /// <param name="state">true=enabled, false=disabled</param>
    public void ToggleEnabled(Control ctrl, bool state)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ToggleEnabled(ctrl, state)));
        else
            ctrl.Enabled = state;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="CheckBox"/></param>
    /// <param name="state">true=checked, false=unchecked</param>
    public void ToggleChecked(CheckBox ctrl, bool state)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ToggleChecked(ctrl, state)));
        else
            ctrl.Checked = state;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ctrl"><see cref="Control"/></param>
    /// <param name="data">text for control</param>
    public void UpdateText(Control ctrl, string data)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => UpdateText(ctrl, data)));
        else
            ctrl.Text = data;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="state"><see cref="FormWindowState"/></param>
    public void UpdateWindowState(FormWindowState state)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => UpdateWindowState(state)));
        else
        {
            try { this.WindowState = state; }
            catch { }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    public void SelectAll(TextBox tb)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SelectAll(tb)));
        else
        {
            try { tb.SelectAll(); }
            catch { }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    /// <param name="data">text to append</param>
    public void AppendTextBox(TextBox tb, string data)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => AppendTextBox(tb, data)));
        else
        {
            try { tb.AppendText(data); }
            catch { }
        }
    }

    /// <summary>
    /// Thread-safe method via <see cref="TaskCompletionSource{TResult}"/>
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    /// <param name="data">text to append</param>
    public Task AppendTextBoxAsync(TextBox tb, string data)
    {
        var tcs = new TaskCompletionSource<bool>();
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
            {
                try
                {
                    tb.AppendText(data);
                    tcs.SetResult(true);
                }
                catch (Exception ex) { tcs.SetException(ex); }
            }));
        }
        else
        {
            try
            {
                tb.AppendText(data);
                tcs.SetResult(true);
            }
            catch (Exception ex) { tcs.SetException(ex); }
        }
        return tcs.Task;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    /// <returns>the <see cref="TextBox"/>'s contents</returns>
    public string GetContents(TextBox tb)
    {
        if (InvokeRequired)
        {
            return (string)Invoke(new Func<string>(() => GetContents(tb)));

            // NOTE: It's better to use the TaskCompletionSource version instead of this:
            //return (string)BeginInvoke(new Action(() => GetContents(tb))).AsyncState;
        }
        else
        {
            try { return tb.Text; }
            catch { return string.Empty; }
        }
    }

    /// <summary>
    /// Thread-safe method via <see cref="TaskCompletionSource{TResult}"/>
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    /// <returns>the <see cref="TextBox"/>'s contents</returns>
    public Task<string> GetContentsAsync(TextBox tb)
    {
        var tcs = new TaskCompletionSource<string>();
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
            {
                try { tcs.SetResult(tb.Text); }
                catch (Exception ex) { tcs.SetException(ex); }
            }));
        }
        else
        {
            try { tcs.SetResult(tb.Text); }
            catch (Exception ex) { tcs.SetException(ex); }
        }
        return tcs.Task;
    }

    /// <summary>
    /// Thread-safe method via <see cref="System.Delegate"/>
    /// <example><code>
    /// GetContentsAsync(txtBoxCtrl, (text) => 
    /// { 
    ///     Debug.WriteLine($"{text}"); 
    /// });
    /// </code></example>
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    /// <param name="callback"><see cref="Action{T}"/></param>
    public void GetContentsAsync(TextBox tb, Action<string> callback)
    {
        if (InvokeRequired)
            tb.BeginInvoke(new Action(() => callback(GetContents(tb))));
        else
        {
            try { callback(tb.Text); }
            catch { callback(string.Empty); }
        }
    }

    /// <summary>
    /// Thread-safe method via <see cref="ValueTask{T}"/>
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    public ValueTask<string> GetContentsValueTask(TextBox tb)
    {
        if (tb.InvokeRequired)
            return new ValueTask<string>(tb.Invoke(new Func<string>(() => tb.Text)) as string);
        else
        {
            try { return new ValueTask<string>(tb.Text); }
            catch (Exception ex) { return ValueTask.FromException<string>(ex); }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="data">text to add to <paramref name="lb"/></param>
    /// <param name="timeStamp">true=include time, false=omit time</param>
    public void AddToListBox(ListBox lb, string data = "", bool timeStamp = false)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => AddToListBox(lb, data, timeStamp)));
        else
        {
            if (timeStamp)
                lb.Items.Add("[" + DateTime.Now.ToString("hh:mm:ss tt fff") + "] " + data);
            else
                lb.Items.Add(data);

            // auto-scroll the list box
            try { lb.SelectedIndex = lb.Items.Count - 1; }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="items"><see cref="List{T}"/> of items to add to <paramref name="lb"/></param>
    /// <param name="timeStamp">true=include time, false=omit time</param>
    public void AddToListBox(ListBox lb, List<string> items, bool timeStamp = false)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => AddToListBox(lb, items, timeStamp)));
        else
        {
            if (items.Count == 0)
                return;

            lb.BeginUpdate(); // This is very important for performance (when adding many items).
            foreach (string item in items)
            {
                if (timeStamp)
                    lb.Items.Add("[" + DateTime.Now.ToString("hh:mm:ss tt fff") + "] " + item);
                else
                    lb.Items.Add(item);
            }
            lb.EndUpdate(); // This is very important for performance (when adding many items).

            // auto-scroll the list box
            try { lb.SelectedIndex = lb.Items.Count - 1; }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public object GetListBoxSelectedItem(ListBox lb)
    {
        if (lb.InvokeRequired)
            return (object)Invoke(new Func<object>(() => lb.SelectedItem));
        else
        {
            try { return lb.SelectedItem; }
            catch { return null; }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="data">text to add to log</param>
    /// <param name="timeStamp">true=include time, false=omit time</param>
    public void AddToListView(ListView lv, string data = "", bool timeStamp = false)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => AddToListView(lv, data, timeStamp)));
        else
        {
            if (timeStamp)
                lv.Items.Add("[" + DateTime.Now.ToString("hh:mm:ss tt fff") + "] " + data);
            else
                lv.Items.Add(data);
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="items"><see cref="List{T}"/> of items to add</param>
    /// <param name="timeStamp">true=include time, false=omit time</param>
    public void AddToListView(ListView lv, List<string> items, bool timeStamp = false)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => AddToListView(lv, items, timeStamp)));
        else
        {
            lv.BeginUpdate(); // This is very important for performance (when adding many items)
            foreach (string item in items)
            {
                if (timeStamp)
                    lv.Items.Add("[" + DateTime.Now.ToString("hh:mm:ss tt fff") + "] " + item);
                else
                    lv.Items.Add(item);
            }
            lv.EndUpdate(); // This is very important for performance (when adding many items)
        }
    }

    /// <summary>
    /// <para><see cref="System.Windows.Forms.View"/> options</para>
    /// <para>LargeIcon: Each item appears as a full-sized icon with a label below it.</para>
    /// <para>Details: Each item appears on a separate line with further information about each item arranged in columns.</para>
    /// <para>SmallIcon: Each item appears as a small icon with a label to its right.</para>
    /// <para>List: Each item appears as a small icon with a label to its right.</para>
    /// <para>Tile: Each item appears as a full-sized icon with the item label and subitem information to the right of it.</para>
    /// </summary>
    public void SetListViewView(ListView lv, System.Windows.Forms.View view)
    {
        if (lv.InvokeRequired)
            lv.Invoke(new Action(() => lv.View = view));
        else
            lv.View = view;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void ClearListBox(ListBox lb)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ClearListBox(lb)));
        else
            lb.Items.Clear();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void ClearListView(ListView lv)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ClearListView(lv)));
        else
            lv.Items.Clear();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    public void ClearTextBox(TextBox tb)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ClearTextBox(tb)));
        else
            tb.Clear();
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="tb"><see cref="TextBox"/></param>
    /// <param name="target">text to find</param>
    /// <param name="replacement">text to replace <paramref name="target"/> with</param>
    public void ReplaceTextBox(TextBox tb, string target, string replacement = "")
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ReplaceTextBox(tb, target, replacement)));
        else
            tb.Text.Replace(target, replacement);
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="item">object to remove</param>
    public void RemoveItemFromListBox(ListBox listBox, object item)
    {
        if (listBox.InvokeRequired)
            listBox.Invoke(new Action(() => listBox.Items.Remove(item)));
        else
            listBox.Items.Remove(item);
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="data">text to insert into the list</param>
    /// <param name="index">location of insertion</param>
    public void ChangeListBoxItem(ListBox lb, string data, int index)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ChangeListBoxItem(lb, data, index)));
        else
        {
            if ((lb.Items.Count > 0) && (index <= lb.Items.Count))
            {
                lb.Items[index] = data;
                lb.SelectedIndex = index;
            }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="data">text to insert into the list</param>
    /// <param name="index">location of insertion</param>
    public void ChangeListViewItem(ListView lv, string data, int index)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => ChangeListViewItem(lv, data, index)));
        else
        {
            if ((lv.Items.Count > 0) && (index <= lv.Items.Count))
            {
                lv.Items.RemoveAt(index);
                lv.Items.Insert(index, data);
            }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void SetToolStripStatusLabelText(ToolStripStatusLabel tssl, string text)
    {
        if (tssl.Owner.InvokeRequired)
            tssl.Owner.Invoke(new Action(() => tssl.Text = text));
        else
            tssl.Text = text;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void SetStatusStripText(StatusStrip ss, string text)
    {
        if (InvokeRequired)
            BeginInvoke(new Action(() => SetStatusStripText(ss, text)));
        else
        {
            if (ss.Items[0].GetType() == typeof(ToolStripStatusLabel))
                ss.Items[0].Text = text;
            else
                MessageBox.Show("The first item in the StatusStrip is not a ToolStripStatusLabel.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="cb"><see cref="ComboBox"/></param>
    public void SelectComboBoxItemByIndex(ComboBox cb, int index)
    {
        if (cb.InvokeRequired)
            cb.Invoke(new Action(() => cb.SelectedIndex = index));
        else
            cb.SelectedIndex = index;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="cb"><see cref="ComboBox"/></param>
    public object GetComboBoxSelectedItem(ComboBox cb)
    {
        if (cb.InvokeRequired)
            return (object)Invoke(new Func<object>(() => cb.SelectedItem));
        else
        {
            try { return cb.SelectedItem; }
            catch { return null; }
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void SetToolTipText(ToolTip tt, Control ctrl, string text)
    {
        if (ctrl.InvokeRequired)
            ctrl.Invoke(new Action(() => tt.SetToolTip(ctrl, text)));
        else
            tt.SetToolTip(ctrl, text);
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void SetToolStripMenuItemEnabled(ToolStripMenuItem mi, bool enabled)
    {
        if (mi.Owner.InvokeRequired)
            mi.Owner.Invoke(new Action(() => mi.Enabled = enabled));
        else
            mi.Enabled = enabled;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="gb"><see cref="GroupBox"/></param>
    public void SetGroupBoxText(GroupBox gb, string text)
    {
        if (gb.InvokeRequired)
            gb.Invoke(new Action(() => gb.Text = text));
        else
            gb.Text = text;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void SetSplitterDistance(Splitter spltr, int distance)
    {
        if (spltr.InvokeRequired)
            spltr.Invoke(new Action(() => spltr.SplitPosition = distance));
        else
            spltr.SplitPosition = distance;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    public void SetToolStripProgressBarValue(ToolStripProgressBar tspb, int value)
    {
        if (tspb.Owner.InvokeRequired)
            tspb.Owner.Invoke(new Action(() => tspb.Value = value));
        else
            tspb.Value = value;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="tsb"><see cref="ToolStripButton"/></param>
    public void SetToolStripButtonChecked(ToolStripButton tsb, bool isChecked)
    {
        if (tsb.Owner.InvokeRequired)
            tsb.Owner.Invoke(new Action(() => tsb.Checked = isChecked));
        else
            tsb.Checked = isChecked;
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="ni"><see cref="NotifyIcon"/></param>
    public void ShowNotifyIconBalloonTip(NotifyIcon ni, int timeout, string tipTitle, string tipText, ToolTipIcon icon)
    {
        if (ni.Icon != null && ni.Visible)
        {
            if (ni.Container is ISynchronizeInvoke syncObj && syncObj.InvokeRequired)
                syncObj.Invoke(new Action(() => ni.ShowBalloonTip(timeout, tipTitle, tipText, icon)), null);
            else
                ni.ShowBalloonTip(timeout, tipTitle, tipText, icon);
        }
    }

    /// <summary>
    /// Thread-safe method
    /// </summary>
    /// <param name="frm"><see cref="Form"/></param>
    public void EnableFormDoubleBuffering(Form frm, bool enable)
    {
        if (frm.InvokeRequired)
        {
            frm.Invoke(new Action(() => 
            {
                var dgvType = frm.GetType();
                var pi = dgvType.GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pi?.SetValue(frm, enable, null);
            }));
        }
        else
        {
            var dgvType = frm.GetType();
            var pi = dgvType.GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pi?.SetValue(frm, enable, null);
        }
    }
    #endregion
}

#region [Support]
public class CustomToolStripRenderer : ToolStripProfessionalRenderer
{
    SolidBrush brsh = new SolidBrush(Color.FromArgb(255, 20, 20, 40));

    /// <summary>
    /// This will only be called when the ToolStrip is drawn, e.g. TextUpdated, OnLoad, OnRestore, DragOut/DragIn.
    /// </summary>
    /// <param name="e"><see cref="ToolStripRenderEventArgs"/></param>
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {

        if (!e.AffectedBounds.IsEmpty)
        {
            Debug.WriteLine($"[INFO] Painting the toolstrip at {DateTime.Now.ToLongTimeString()}");
            Rectangle bounds = new Rectangle(Point.Empty, e.AffectedBounds.Size);
            e.Graphics.FillRectangle(brsh, bounds);
        }
        else
            base.OnRenderToolStripBackground(e);
    }

    /// <summary>
    /// Did not work properly for this application.
    /// </summary>
    /// <param name="e"><see cref="ToolStripItemRenderEventArgs"/></param>
    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        var btn = e.Item as ToolStripButton;
        if (btn != null && btn.CheckOnClick && btn.Checked)
        {
            Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);
            e.Graphics.FillRectangle(brsh, bounds);
        }
        else 
            base.OnRenderButtonBackground(e);
    }
}

public class CustomPopupMenu : ContextMenuStrip
{
    public event EventHandler MenuItemClicked;

    public CustomPopupMenu(Dictionary<string, Image> items, Font menuFont)
    {
        // Add menu items
        foreach (KeyValuePair<string, Image> item in items)
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem(item.Key);
            menuItem.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            menuItem.BackColor = Color.FromArgb(40, 40, 40);
            menuItem.ForeColor = Color.WhiteSmoke;
            menuItem.TextAlign = ContentAlignment.MiddleLeft;
            menuItem.Image = item.Value;
            menuItem.ImageAlign = ContentAlignment.MiddleCenter;
            menuItem.Font = menuFont;
            menuItem.Click += MenuItem_Click;
            menuItem.MouseEnter += MenuItem_MouseEnter;
            menuItem.MouseLeave += MenuItem_MouseLeave;
            this.Items.Add(menuItem);
        }
    }

    void MenuItem_MouseLeave(object? sender, EventArgs e)
    {
        var mi = sender as ToolStripMenuItem;
        if (mi == null) { return; }
        mi.ForeColor = System.Drawing.Color.WhiteSmoke;
        //mi.Font = new Font(mi.Font, FontStyle.Regular);
    }

    void MenuItem_MouseEnter(object? sender, EventArgs e)
    {
        var mi = sender as ToolStripMenuItem;
        if (mi == null) { return; }
        mi.ForeColor = System.Drawing.Color.Black;
        //mi.Font = new Font(mi.Font, FontStyle.Bold);
    }

    /// <summary>
    /// Trigger the MenuItemClicked event with the clicked menu item's text
    /// </summary>
    void MenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem)
        {
            MenuItemClicked?.Invoke(this, new MenuItemClickedEventArgs(menuItem.Text));
        }
    }
}

public class MenuItemClickedEventArgs : EventArgs
{
    public string ItemText { get; }
    public MenuItemClickedEventArgs(string clickedItemText)
    {
        ItemText = clickedItemText;
    }
}
#endregion
