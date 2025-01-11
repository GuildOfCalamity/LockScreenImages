using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace LockScreenImages;

/// <summary>
/// A custom designer-less MessageBox replacement.
/// </summary>
public partial class QuestionForm : Form
{
    /// <summary>
    /// The main user method to show the custom message box.
    /// </summary>
    /// <param name="message">the text to display in the center of the dialog</param>
    /// <param name="title">the text to display on the title bar</param>
    /// <param name="addIcon">true to show the question icon, false to hide the icon</param>
    public static DialogResult Show(string message, string title, bool addIcon = false, bool acrylic = false)
    {
        DialogResult result = DialogResult.None;
        using (var customMessageBox = new QuestionForm(message, title, addIcon, acrylic))
        {
            result = customMessageBox.ShowDialog();
        }
        return result;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    QuestionForm(string message, string title, bool addIcon, bool acrylic)
    {
        InitializeComponents(message, title, addIcon, acrylic);
    }

    /// <summary>
    /// Replaces the standard <see cref="InitializeComponent"/>.
    /// </summary>
    void InitializeComponents(string message, string title, bool addIcon, bool acrylic)
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
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 12, 12));
        }

        if (addIcon && !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconQuestion.png")))
        {
            Debug.WriteLine($"[WARNING] 'InfoQuestion.png' could not be located, disabling the icon option.");
            addIcon = false;
        }

        if (addIcon)
        {
            pictureBox = new PictureBox { Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IconQuestion.png")) };
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

        #region [Form Yes Button]
        yesButton = new Button();
        yesButton.TabIndex = 0;
        yesButton.TabStop = true;
        yesButton.Text = yesText;
        yesButton.TextAlign = ContentAlignment.MiddleCenter;
        yesButton.Font = new Font("Calibri", 14);
        yesButton.ForeColor = clrForeText;
        yesButton.MinimumSize = new Size(mainFormSize.Width / 2, 33);
        yesButton.FlatStyle = FlatStyle.Flat;
        yesButton.FlatAppearance.BorderSize = 0;
        yesButton.BackColor = clrSuccess;
        yesButton.FlatAppearance.MouseOverBackColor = clrMouseOver;
        yesButton.Dock = DockStyle.Left;
        yesButton.DialogResult = DialogResult.Yes;
        yesButton.Click += (sender, e) =>
        {
            this.DialogResult = DialogResult.Yes; // Unnecessary, but let's do it anyways.
            this.Close();
        };
        #endregion

        #region [Form No Button]
        noButton = new Button();
        noButton.TabIndex = 0;
        noButton.TabStop = true;
        noButton.Text = noText;
        noButton.TextAlign = ContentAlignment.MiddleCenter;
        noButton.Font = new Font("Calibri", 14);
        noButton.ForeColor = clrForeText;
        noButton.MinimumSize = new Size(mainFormSize.Width / 2, 33);
        noButton.FlatStyle = FlatStyle.Flat;
        noButton.FlatAppearance.BorderSize = 0;
        noButton.BackColor = clrError;
        noButton.FlatAppearance.MouseOverBackColor = clrMouseOver;
        noButton.Dock = DockStyle.Right;
        noButton.DialogResult = DialogResult.No;
        noButton.Click += (sender, e) =>
        {
            this.DialogResult = DialogResult.No; // Unnecessary, but let's do it anyways.
            this.Close();
        };
        #endregion

        #region [Button Container]
        var panel = new Panel();
        panel.Size = new Size(mainFormSize.Width, mainFormSize.Height / 5);
        panel.Dock = DockStyle.Bottom;
        panel.Controls.Add(yesButton);
        panel.Controls.Add(noButton);
        #endregion

        this.Controls.Add(panel);

        this.Shown += (sender, e) =>
        {
            this.ActiveControl = yesButton;
            if (autoTab)
                SendKeys.SendWait("{TAB}");
        };

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
            {
                DwmHelper.Windows10EnableTranslucent(this.Handle);
            }

            // Set Drop shadow of a border-less Form
            if (this.FormBorderStyle == FormBorderStyle.None)
                DwmHelper.WindowBorderlessDropShadow(this.Handle, 2);
        }
        #endregion
    }

    #region [Props]
    bool autoTab = false;
    bool roundedForm = true;
    string yesText = "YES";
    string noText = "NO";
    Label? messageLabel;
    Button? yesButton;
    Button? noButton;
    PictureBox? pictureBox;
    FormBorderStyle borderStyle = FormBorderStyle.FixedToolWindow;
    Point iconLocus = new Point(16, 62);
    Size iconSize = new Size(48, 48);
    Size mainFormSize = new Size(510, 210);
    Padding withIconPadding = new Padding(75, 10, 20, 10);
    Padding withoutIconPadding = new Padding(15);
    Color clrForeText = Color.White;
    Color clrInfo = Color.FromArgb(0, 8, 48);
    Color clrWarning = Color.FromArgb(60, 50, 0);
    Color clrSuccess = Color.FromArgb(25, 45, 25);
    Color clrError = Color.FromArgb(45, 25, 25);
    Color clrBackground = Color.FromArgb(35, 35, 35);
    Color clrMouseOver = Color.FromArgb(55, 55, 55);
    #endregion

    #region [RFU]
    /// <summary>
    /// Occurs when the user inputs data.
    /// </summary>
    public event EventHandler? DataReceived;

    /// <summary>
    /// Raises the <see cref="DataReceived"/> event.
    /// </summary>
    protected virtual void OnDataReceived() => DataReceived?.Invoke(this, new EventArgs());
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
