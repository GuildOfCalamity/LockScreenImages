using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace LockScreenImages;

/// <summary>
/// https://learn.microsoft.com/en-us/windows/win32/dwm/blur-ovw
/// https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw
/// https://learn.microsoft.com/en-us/windows/win32/dwm/composition-ovw#controlling-non-client-region-rendering
/// Two of the visual effects that DWM enables are transparency of the non-client region of a window, and transition effects.
/// Your application might have to disable or re-enable these effects for styling or compatibility reasons.
/// The following functions are used to manage transparency and transition effect behavior:
/// - DwmGetWindowAttribute
/// - DwmSetWindowAttribute
/// 
/// Portions of this code are from the following:
/// https://stackoverflow.com/questions/51578104/how-to-create-a-semi-transparent-or-blurred-backcolor-in-a-windows-form
/// </summary>
/// <remarks>
/// Minimum supported client: Windows Vista [desktop apps only]
/// As of Windows 8, DWM composition is always enabled, so this message is not sent regardless 
/// of video mode changes. If you are using Windows 7/Vista then you may want to add 
/// "override void WndProc(ref Message m)" for DWMCOMPOSITIONCHANGED messages.
/// </remarks>
[SuppressUnmanagedCodeSecurity]
public class DwmHelper
{
    // https://learn.microsoft.com/en-us/windows/win32/dwm/wm-dwmcompositionchanged
    public const int WM_DWMCOMPOSITIONCHANGED = 0x031E;

    // https://learn.microsoft.com/en-us/windows/win32/dwm/dwm-tnp-constants
    public const int DWM_TNP_RECTDESTINATION = 0x0001;
    public const int DWM_TNP_RECTSOURCE = 0x0002;
    public const int DWM_TNP_OPACITY = 0x0004;
    public const int DWM_TNP_VISIBLE = 0x0008;
    public const int DWM_TNP_SOURCECLIENTAREAONLY = 0x0010;

    #region [Structs]
    public struct MARGINS
    {
        public int leftWidth;
        public int rightWidth;
        public int topHeight;
        public int bottomHeight;

        public MARGINS(int LeftWidth, int RightWidth, int TopHeight, int BottomHeight)
        {
            leftWidth = LeftWidth;
            rightWidth = RightWidth;
            topHeight = TopHeight;
            bottomHeight = BottomHeight;
        }

        public void NoMargins()
        {
            leftWidth = 0;
            rightWidth = 0;
            topHeight = 0;
            bottomHeight = 0;
        }

        public void SheetOfGlass()
        {
            leftWidth = -1;
            rightWidth = -1;
            topHeight = -1;
            bottomHeight = -1;
        }
    }

    // https://learn.microsoft.com/en-us/windows/win32/dwm/dwm-bb-constants
    [Flags]
    public enum DWM_BB
    {
        Enable = 1,
        BlurRegion = 2,
        TransitionOnMaximized = 4
    }

    // https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
    public enum DWMWINDOWATTRIBUTE : uint
    {
        NCRenderingEnabled = 1,       // Get attribute
        NCRenderingPolicy,            // Enable or disable non-client rendering
        TransitionsForceDisabled,
        AllowNCPaint,
        CaptionButtonBounds,          // Get attribute
        NonClientRtlLayout,
        ForceIconicRepresentation,
        Flip3DPolicy,
        ExtendedFrameBounds,          // Get attribute
        HasIconicBitmap,
        DisallowPeek,
        ExcludedFromPeek,
        Cloak,
        Cloaked,                      // Get attribute. Returns a DWMCLOAKEDREASON
        FreezeRepresentation,
        PassiveUpdateMode,
        UseHostBackDropBrush,
        AccentPolicy = 19,            // Win 10 (undocumented)
        ImmersiveDarkMode = 20,       // Win 11 22000
        WindowCornerPreference = 33,  // Win 11 22000
        BorderColor,                  // Win 11 22000
        CaptionColor,                 // Win 11 22000
        TextColor,                    // Win 11 22000
        VisibleFrameBorderThickness,  // Win 11 22000
        SystemBackdropType            // Win 11 22621
    }

    public enum DWMCLOACKEDREASON : uint
    {
        DWM_CLOAKED_APP = 0x0000001,       // Cloaked by its owner application.
        DWM_CLOAKED_SHELL = 0x0000002,     // Cloaked by the Shell.
        DWM_CLOAKED_INHERITED = 0x0000004  // Inherited from its owner window.
    }

    public enum DWMNCRENDERINGPOLICY : uint
    {
        UseWindowStyle, // Enable/disable non-client rendering based on window style
        Disabled,       // Disabled non-client rendering; window style is ignored
        Enabled,        // Enabled non-client rendering; window style is ignored
    };

    public enum DWMACCENTSTATE
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_INVALID_STATE = 4
    }

    [Flags]
    public enum CompositionAction : uint
    {
        DWM_EC_DISABLECOMPOSITION = 0,
        DWM_EC_ENABLECOMPOSITION = 1
    }

    // Values designating how Flip3D treats a given window.
    enum DWMFLIP3DWINDOWPOLICY : uint
    {
        Default,        // Hide or include the window in Flip3D based on window style and visibility.
        ExcludeBelow,   // Display the window under Flip3D and disabled.
        ExcludeAbove,   // Display the window above Flip3D and enabled.
    };

    public enum ThumbProperties_dwFlags : uint
    {
        RectDestination = 0x00000001,
        RectSource = 0x00000002,
        Opacity = 0x00000004,
        Visible = 0x00000008,
        SourceClientAreaOnly = 0x00000010
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AccentPolicy
    {
        public DWMACCENTSTATE AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;

        public AccentPolicy(DWMACCENTSTATE accentState, int accentFlags, uint gradientColor, int animationId)
        {
            AccentState = accentState;
            AccentFlags = accentFlags;
            GradientColor = gradientColor;
            AnimationId = animationId;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_BLURBEHIND
    {
        public DWM_BB dwFlags;
        public int fEnable;
        public IntPtr hRgnBlur;
        public int fTransitionOnMaximized;

        public DWM_BLURBEHIND(bool enabled)
        {
            dwFlags = DWM_BB.Enable;
            fEnable = (enabled) ? 1 : 0;
            hRgnBlur = IntPtr.Zero;
            fTransitionOnMaximized = 0;
        }

        public Region Region => Region.FromHrgn(hRgnBlur);

        public bool TransitionOnMaximized
        {
            get => fTransitionOnMaximized > 0;
            set
            {
                fTransitionOnMaximized = (value) ? 1 : 0;
                dwFlags |= DWM_BB.TransitionOnMaximized;
            }
        }

        public void SetRegion(Graphics graphics, Region region)
        {
            hRgnBlur = region.GetHrgn(graphics);
            dwFlags |= DWM_BB.BlurRegion;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WinCompositionAttrData
    {
        public DWMWINDOWATTRIBUTE Attribute;
        public IntPtr Data;  // Will point to an AccentPolicy struct, where Attribute will be DWMWINDOWATTRIBUTE.AccentPolicy
        public int SizeOfData;

        public WinCompositionAttrData(DWMWINDOWATTRIBUTE attribute, IntPtr data, int sizeOfData)
        {
            Attribute = attribute;
            Data = data;
            SizeOfData = sizeOfData;
        }
    }

    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// https://github.com/Alexpux/mingw-w64/blob/master/mingw-w64-headers/include/dwmapi.h
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ns-dwmapi-dwm_thumbnail_properties
    /// https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;              // A bitwise combination of DWM thumbnail constant values that indicates which members of this structure are set.
        public RECT rcDestination;        // The area in the destination window where the thumbnail will be rendered.
        public RECT rcSource;             // The region of the source window to use as the thumbnail. By default, the entire window is used as the thumbnail.
        public byte opacity;              // The opacity with which to render the thumbnail. 0 is fully transparent while 255 is fully opaque. The default value is 255.
        public int fVisible;              // TRUE to make the thumbnail visible; otherwise, FALSE. The default is FALSE.
        public int fSourceClientAreaOnly; // TRUE to use only the thumbnail source's client area; otherwise, FALSE. The default is FALSE.
    }

    public enum DWM_SYSTEMBACKDROP_TYPE : uint
    {
        DWMSBT_AUTO,            // Let the Desktop Window Manager (DWM) automatically decide the system-drawn backdrop material for this window. 
        DWMSBT_NONE,            // Don't draw any system backdrop.
        DWMSBT_MAINWINDOW,      // Draw the backdrop material effect corresponding to a long-lived window behind the entire window bounds.
        DWMSBT_TRANSIENTWINDOW, // Draw the backdrop material effect corresponding to a transient window behind the entire window bounds.
        DWMSBT_TABBEDWINDOW     // Draw the backdrop material effect corresponding to a window with a tabbed title bar behind the entire window bounds.
    }
    #endregion

    static int GetBlurBehindPolicyAccentFlags()
    {
        int drawLeftBorder = 20;
        int drawTopBorder = 40;
        int drawRightBorder = 80;
        int drawBottomBorder = 100;
        return (drawLeftBorder | drawTopBorder | drawRightBorder | drawBottomBorder);
    }

    #region [PInvokes]
    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmenableblurbehindwindow
    /// </summary>
    [DllImport("dwmapi.dll")]
    internal static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blurBehind);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmenablecomposition
    /// </summary>
    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmEnableComposition(CompositionAction uCompositionAction);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmextendframeintoclientarea
    /// </summary>
    [DllImport("dwmapi.dll")]
    internal static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetwindowattribute
    /// </summary>
    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr, ref int attrValue, int attrSize);

    /// <summary>
    /// Sets the value of Desktop Window Manager (DWM) non-client rendering attributes for a window.
    /// For programming guidance, and code examples, see Controlling non-client region rendering.
    /// Minimum supported client: Windows Vista [desktop apps only]
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmsetwindowattribute
    /// https://learn.microsoft.com/en-us/windows/win32/dwm/composition-ovw#controlling-non-client-region-rendering
    /// </summary>
    /// <param name="hwnd">The handle to the window for which the attribute value is to be set.</param>
    /// <param name="attr">A flag describing which value to set, specified as a value of the DWMWINDOWATTRIBUTE enumeration. This parameter specifies which attribute to set, and the pvAttribute parameter points to an object containing the attribute value.</param>
    /// <param name="attrValue">A pointer to an object containing the attribute value to set. The type of the value set depends on the value of the dwAttribute parameter. The DWMWINDOWATTRIBUTE enumeration topic indicates, in the row for each flag, what type of value you should pass a pointer to in the pvAttribute parameter.</param>
    /// <param name="attrSize">The size, in bytes, of the attribute value being set via the pvAttribute parameter. The type of the value set, and therefore its size in bytes, depends on the value of the dwAttribute parameter.</param>
    /// <returns>HRESULT</returns>
    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr, ref int attrValue, int attrSize);

    /// <summary>
    /// http://undoc.airesoft.co.uk/user32.dll/SetWindowCompositionAttribute.php
    /// </summary>
    /// <remarks>
    /// You can restore the normal mode after setting the transparency with ACCENT_DISABLED instead of ACCENT_ENABLE_BLURBEHIND or other value.
    /// </remarks>
    [DllImport("User32.dll", SetLastError = true)]
    internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WinCompositionAttrData data);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmiscompositionenabled
    /// </summary>
    [DllImport("dwmapi.dll")]
    internal static extern int DwmIsCompositionEnabled(ref int pfEnabled);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmseticonicthumbnail
    /// </summary>
    /// <param name="hwnd">A handle to the window or tab. This window must belong to the calling process.</param>
    /// <param name="hbmp">A handle to the bitmap to represent the window that hwnd specifies.</param>
    /// <param name="dwSITFlags">
    /// <br>The display options for the thumbnail. One of the following values:</br>
    /// <br>0x00000000 = No frame is displayed around the provided thumbnail.</br>
    /// <br>0x00000001 = Displays a frame around the provided thumbnail.</br>
    /// </param>
    /// <returns>HRESULT</returns>
    [DllImport("dwmapi.dll", SetLastError = true)]
    internal static extern int DwmSetIconicThumbnail(IntPtr hwnd, IntPtr hbmp, uint dwSITFlags);

    /// <summary>
    /// Creates a Desktop Window Manager (DWM) thumbnail relationship between the destination and source windows.
    /// </summary>
    /// <param name="hwndDestination">The handle to the window that will use the DWM thumbnail. Setting the destination window handle to anything other than a top-level window type will result in a return value of E_INVALIDARG.</param>
    /// <param name="hwndSource">The handle to the window to use as the thumbnail source. Setting the source window handle to anything other than a top-level window type will result in a return value of E_INVALIDARG.</param>
    /// <param name="phThumbnailId">A pointer to a handle that, when this function returns successfully, represents the registration of the DWM thumbnail.</param>
    /// <returns>HRESULT</returns>
    [DllImport("dwmapi.dll", SetLastError = true)]
    internal static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, IntPtr phThumbnailId);

    /// <summary>
    /// Updates the properties for a Desktop Window Manager (DWM) thumbnail.
    /// </summary>
    /// <param name="hThumbnailId">The handle to the DWM thumbnail to be updated. Null or invalid thumbnails, as well as thumbnails owned by other processes will result in a return value of E_INVALIDARG.</param>
    /// <param name="ptnProperties">A pointer to a DWM_THUMBNAIL_PROPERTIES structure that contains the new thumbnail properties.</param>
    /// <returns>HRESULT</returns>
    [DllImport("dwmapi.dll", SetLastError = true)]
    internal static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, DWM_THUMBNAIL_PROPERTIES ptnProperties);
    #endregion

    #region [Public Methods]
    /// <summary>
    /// If the major version is reported incorrectly make sure you've added
    /// an "app.manifest" to your solution and uncomment the line:
    /// <example><code>supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"</code></example>
    /// </summary>
    /// <returns>true if DWM composition is enabled, false otherwise</returns>
    public static bool IsDWMCompositionEnabled() => Environment.OSVersion.Version.Major >= 6 && IsCompositionEnabled();
    static bool IsCompositionEnabled()
    {
        int pfEnabled = 0;
        int result = DwmIsCompositionEnabled(ref pfEnabled);
        return (pfEnabled == 1) ? true : false;
    }

    public static bool IsNonClientRenderingEnabled(IntPtr hWnd)
    {
        int gwaEnabled = 0;
        int result = DwmGetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.NCRenderingEnabled, ref gwaEnabled, sizeof(int));
        return gwaEnabled == 1;
    }

    /*
       https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmsetwindowattribute
       HRESULT DwmSetWindowAttribute(
           HWND    hwnd,         // The handle to the window for which the attribute value is to be set.
           DWORD   dwAttribute,  // A flag describing which value to set, specified as a value of the DWMWINDOWATTRIBUTE enumeration. This parameter specifies which attribute to set, and the pvAttribute parameter points to an object containing the attribute value.
           LPCVOID pvAttribute,  // A pointer to an object containing the attribute value to set. The type of the value set depends on the value of the dwAttribute parameter. The DWMWINDOWATTRIBUTE enumeration topic indicates, in the row for each flag, what type of value you should pass a pointer to in the pvAttribute parameter.
           DWORD   cbAttribute   // The size, in bytes, of the attribute value being set via the pvAttribute parameter. The type of the value set, and therefore its size in bytes, depends on the value of the dwAttribute parameter.
       );
       - If the function succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.
       - It's not valid to call this function with the dwAttribute parameter set to DWMWA_NCRENDERING_ENABLED.
       - To enable or disable non-client rendering, you should use the DWMWA_NCRENDERING_POLICY attribute, and set the desired value. 
       - Minimum supported client: Windows Vista
    */
    public static bool WindowSetAttribute(IntPtr hWnd, DWMWINDOWATTRIBUTE attribute, int attributeValue)
    {
        int result = DwmSetWindowAttribute(hWnd, attribute, ref attributeValue, sizeof(int));
        return (result == 0);
    }

    public static void Windows10EnableBlurBehind(IntPtr hWnd)
    {
        DWMNCRENDERINGPOLICY policy = DWMNCRENDERINGPOLICY.Enabled;
        WindowSetAttribute(hWnd, DWMWINDOWATTRIBUTE.NCRenderingPolicy, (int)policy);

        AccentPolicy accPolicy = new AccentPolicy()
        {
            AccentState = DWMACCENTSTATE.ACCENT_ENABLE_BLURBEHIND,
        };

        int accentSize = Marshal.SizeOf(accPolicy);
        IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
        Marshal.StructureToPtr(accPolicy, accentPtr, false);
        var data = new WinCompositionAttrData(DWMWINDOWATTRIBUTE.AccentPolicy, accentPtr, accentSize);

        SetWindowCompositionAttribute(hWnd, ref data);
        Marshal.FreeHGlobal(accentPtr);
    }

    public static void Windows10EnableTransparentGradient(IntPtr hWnd, uint alpha = 0x40, uint blue = 0x60, uint green = 0x00, uint red = 0x00)
    {
        DWMNCRENDERINGPOLICY policy = DWMNCRENDERINGPOLICY.Enabled;
        WindowSetAttribute(hWnd, DWMWINDOWATTRIBUTE.NCRenderingPolicy, (int)policy);

        string color = $"{alpha:X2}{blue:X2}{green:X2}{red:X2}";

        AccentPolicy accPolicy = new AccentPolicy()
        {
            AccentState = DWMACCENTSTATE.ACCENT_ENABLE_TRANSPARENTGRADIENT,
            AccentFlags = 2, // Must be 2 for standard transparent gradient.
            GradientColor = Convert.ToUInt32(color, 16) // 0x40602020 - First 2 bytes are the alpha value, the next 6 bytes are the BGR values.
        };

        int accentSize = Marshal.SizeOf(accPolicy);
        IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
        Marshal.StructureToPtr(accPolicy, accentPtr, false);
        var data = new WinCompositionAttrData(DWMWINDOWATTRIBUTE.AccentPolicy, accentPtr, accentSize);

        SetWindowCompositionAttribute(hWnd, ref data);
        Marshal.FreeHGlobal(accentPtr);
    }

    /// <summary>
    /// This will also work for Windows 10+, but the effect is
    /// only a transparency and not the acrylic/glass effect.
    /// </summary>
    public static bool Windows10EnableTranslucent(IntPtr hWnd)
    {
        DWMNCRENDERINGPOLICY policy = DWMNCRENDERINGPOLICY.Enabled;
        WindowSetAttribute(hWnd, DWMWINDOWATTRIBUTE.NCRenderingPolicy, (int)policy);

        DWM_BLURBEHIND dwm_BB = new DWM_BLURBEHIND(true);
        int result = DwmEnableBlurBehindWindow(hWnd, ref dwm_BB);
        return result == 0;
    }

    /// <summary>
    /// Extend frame on the bottom of client area.
    /// </summary>
    public static bool WindowExtendIntoClientArea(IntPtr hWnd, MARGINS margins)
    {
        int result = DwmExtendFrameIntoClientArea(hWnd, ref margins);
        return result == 0;
    }

    public static bool WindowBorderlessDropShadow(IntPtr hWnd, int shadowSize)
    {
        MARGINS margins = new MARGINS(0, shadowSize, 0, shadowSize);
        int result = DwmExtendFrameIntoClientArea(hWnd, ref margins);
        return result == 0;
    }

    /// <summary>
    /// All margins set to -1 => Sheet Of Glass effect
    /// </summary>
    /// <param name="hWnd">pointer to window</param>
    /// <returns>true if successful, false otherwise</returns>
    public static bool WindowSheetOfGlass(IntPtr hWnd)
    {
        MARGINS margins = new MARGINS();
        margins.SheetOfGlass();
        int result = DwmExtendFrameIntoClientArea(hWnd, ref margins);
        return result == 0;
    }

    public static bool WindowDisableRendering(IntPtr hWnd)
    {
        int ncrp = (int)DWMNCRENDERINGPOLICY.Disabled;
        // Disable non-client area rendering on the window.
        int result = DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.NCRenderingPolicy, ref ncrp, sizeof(int));
        return result == 0;
    }
    #endregion
}
