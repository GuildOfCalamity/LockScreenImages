using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace LockScreenImages;

public enum MessageLevel
{
    Info = 0,
    Warning = 1,
    Success = 2,
    Error = 3,
}

/// <summary>
/// A custom designer-less MessageBox replacement.
/// </summary>
public partial class MessageForm : Form
{
    /// <summary>
    /// The main user method to show the custom message box.
    /// </summary>
    /// <param name="message">the text to display in the center of the dialog</param>
    /// <param name="title">the text to display on the title bar</param>
    /// <param name="autoClose"><see cref="TimeSpan"/> to close the dialog, use <see cref="TimeSpan.Zero"/> or null for indefinite</param>
    /// <param name="level"><see cref="MessageLevel"/></param>
    /// <param name="addIcon">true to show the <see cref="MessageLevel"/> icon, false to hide the icon</param>
    public static void Show(string message, string title, MessageLevel level = MessageLevel.Info, bool addIcon = false, bool acrylic = false, TimeSpan ? autoClose = null)
    {
        using (var customMessageBox = new MessageForm(message, title, level, addIcon, acrylic, autoClose))
        {
            customMessageBox.ShowDialog();
        }
    }

    /// <summary>
    /// Constructor
    /// </summary>
    MessageForm(string message, string title, MessageLevel level, bool addIcon, bool acrylic, TimeSpan? autoClose)
    {
        //var fcoll = Application.OpenForms.OfType<Form>();
        //foreach (var frm in fcoll) { /* We'll only have one form open, the Main Form. */ }
        InitializeComponents(message, title, level, addIcon, acrylic, autoClose);
    }

    /// <summary>
    /// Replaces the standard <see cref="InitializeComponent"/>.
    /// </summary>
    void InitializeComponents(string message, string title, MessageLevel level, bool addIcon, bool acrylic, TimeSpan? autoClose)
    {
        #region [Form Background, Icon & Border]
        this.Text = title;
        this.FormBorderStyle = borderStyle;
        this.ClientSize = mainFormSize;
        this.BackColor = clrBackground;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico"));

        if (this.FormBorderStyle == FormBorderStyle.FixedDialog)
        {
            this.MaximizeBox = false;
            this.MinimizeBox = false;
        }

        if (roundedForm)
        {
            this.FormBorderStyle = FormBorderStyle.None; // Do not use border styles with this API, it will look weird.
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 13, 13));
        }

        if (addIcon && !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconInfo.png")))
        {
            Debug.WriteLine($"[WARNING] 'InfoIcon.png' could not be located, disabling the icon option.");
            addIcon = false;
        }

        if (addIcon)
        {
            switch (level)
            {
                case MessageLevel.Info: pictureBox = new PictureBox { Image    = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconInfo.png")) }; break;
                case MessageLevel.Warning: pictureBox = new PictureBox { Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconWarning.png")) }; break;
                case MessageLevel.Success: pictureBox = new PictureBox { Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconSuccess.png")) }; break;
                case MessageLevel.Error: pictureBox = new PictureBox { Image   = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconError.png")) }; break;
                default: pictureBox = new PictureBox { Image                   = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconInfo.png")) }; break;
            }
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox.Size = iconSize;
            pictureBox.Location = iconLocus;
            this.Controls.Add(pictureBox);
        }
        #endregion

        #region [Form Message Label]
        messageLabel = new Label();
        messageLabel.ForeColor = clrForeText;
        // Attempt to auto-size the available label area.
        // We could change this to a TextBox with a vertical scrollbar.
        if (message.Length > 600)
            messageLabel.Font = new Font("Calibri", 7);
        else if (message.Length > 500)
            messageLabel.Font = new Font("Calibri", 8);
        else if (message.Length > 400)
            messageLabel.Font = new Font("Calibri", 9);
        else if (message.Length > 300)
            messageLabel.Font = new Font("Calibri", 10);
        else if (message.Length > 200)
            messageLabel.Font = new Font("Calibri", 11);
        else if (message.Length > 100)
            messageLabel.Font = new Font("Calibri", 12);
        else // standard font size
            messageLabel.Font = new Font("Calibri", 13);
        messageLabel.Text = message;
        messageLabel.TextAlign = ContentAlignment.MiddleLeft;
        messageLabel.Dock = DockStyle.Fill;
        messageLabel.Padding = addIcon ? withIconPadding : withoutIconPadding;
        this.Controls.Add(messageLabel);
        #endregion

        #region [Form Button]
        okButton = new Button();
        okButton.TabIndex = 0;
        okButton.TabStop = true;
        okButton.Text = okText;
        okButton.TextAlign = ContentAlignment.MiddleCenter;
        okButton.Font = new Font("Calibri", 14);
        okButton.ForeColor = clrForeText;
        okButton.MinimumSize = new Size(300, 33);
        okButton.FlatStyle = FlatStyle.Flat;
        okButton.FlatAppearance.BorderSize = 0;
        switch (level)
        {
            case MessageLevel.Info:
                okButton.BackColor = clrInfo;
                okButton.FlatAppearance.MouseOverBackColor = clrMouseOver; //ColorBlend(Color.Gray, clrInfo);
                break;
            case MessageLevel.Warning:
                okButton.BackColor = clrWarning;
                okButton.FlatAppearance.MouseOverBackColor = clrMouseOver; //ColorBlend(Color.Gray, clrWarning);
                break;
            case MessageLevel.Success:
                okButton.BackColor = clrSuccess;
                okButton.FlatAppearance.MouseOverBackColor = clrMouseOver; //ColorBlend(Color.Gray, clrSuccess);
                break;
            case MessageLevel.Error:
                okButton.BackColor = clrError;
                okButton.FlatAppearance.MouseOverBackColor = clrMouseOver; //ColorBlend(Color.Gray, clrError);
                break;
            default:
                okButton.BackColor = clrBackground;
                okButton.FlatAppearance.MouseOverBackColor = clrMouseOver;
                break;
        }
        okButton.Dock = DockStyle.Bottom;
        okButton.Click += (sender, e) =>
        {
            if (tmrClose != null)
            {
                tmrClose.Stop();
                tmrClose.Dispose();
            }
            this.Close();
        };
        this.Shown += (sender, e) =>
        {
            this.ActiveControl = okButton;
            if (autoTab)
                SendKeys.SendWait("{TAB}");
        };
        this.Controls.Add(okButton);
        #endregion

        if (roundedForm)
        {   // Support dragging the dialog, since the message label will
            // take up most of the real estate when there is no title bar.
            messageLabel.MouseDown += (obj, mea) =>
            {
                if (mea.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
        }

        // Do we have a TimeSpan to observe?
        if (autoClose != null && autoClose != TimeSpan.Zero && tmrClose == null)
        {
            tmrClose = new System.Windows.Forms.Timer();
            var overflow = (int)autoClose.Value.TotalMilliseconds;
            if (overflow == int.MinValue)
                overflow = int.MaxValue;
            tmrClose.Interval = overflow;
            tmrClose.Tick += TimerOnTick;
            tmrClose.Start();
        }

        #region [Acrylic Background]
        if (acrylic && DwmHelper.IsDWMCompositionEnabled())
        {
            // If the version is not being observed correctly
            // then make sure to add app.manifest to the project.
            if (Environment.OSVersion.Version.Major == 10)
            {
                DwmHelper.Windows10EnableBlurBehind(this.Handle);
                // Enables content rendered in the non-client area to be visible on the frame drawn by DWM.
                //DwmHelper.WindowSetAttribute(this.Handle, DwmHelper.DWMWINDOWATTRIBUTE.AllowNCPaint, 1);
            }
            else if (Environment.OSVersion.Version.Major > 6)
            {
                DwmHelper.Windows10EnableTransparentGradient(this.Handle);
                //DwmHelper.WindowSheetOfGlass(this.Handle);
            }
            else
            {   // This will also work for Windows 10+ also, but the effect
                // is only a transparency and not the acrylic/glass effect.
                DwmHelper.Windows10EnableTranslucent(this.Handle);
            }

            // Set Drop shadow of a border-less Form
            if (this.FormBorderStyle == FormBorderStyle.None)
                DwmHelper.WindowBorderlessDropShadow(this.Handle, 2);
        }
        #endregion
    }

    /// <summary>
    /// Auto-close timer event.
    /// </summary>
    void TimerOnTick(object? sender, EventArgs e)
    {
        if (tmrClose != null)
        {
            tmrClose.Stop();
            tmrClose.Dispose();
            this.Close();
        }
    }

    #region [Props]
    bool autoTab = false;
    bool roundedForm = true;
    string okText = "OK"; //"Acknowledge"
    Label? messageLabel;
    Button? okButton;
    PictureBox? pictureBox;
    FormBorderStyle borderStyle = FormBorderStyle.FixedToolWindow;
    Point iconLocus = new Point(16, 62);
    Size iconSize = new Size(48, 48);
    Size mainFormSize = new Size(530, 210);
    Padding withIconPadding = new Padding(75, 10, 20, 10);
    Padding withoutIconPadding = new Padding(15);
    Color clrForeText = Color.White;
    Color clrInfo = Color.FromArgb(0, 8, 48);
    Color clrWarning = Color.FromArgb(60, 50, 0);
    Color clrSuccess = Color.FromArgb(25, 45, 25);
    Color clrError = Color.FromArgb(45, 25, 25);
    Color clrBackground = Color.FromArgb(35, 35, 35);
    Color clrMouseOver = Color.FromArgb(55, 55, 55);
    System.Windows.Forms.Timer tmrClose = null;
    #endregion

    #region [Round Border]
    const int WM_NCLBUTTONDOWN = 0xA1;
    const int HT_CAPTION = 0x2;

    [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ReleaseCapture();
    #endregion
}
