using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CyberManager.UI.Services;

/// <summary>
/// Applies native Windows DWM Acrylic / BlurBehind effects to popup HWNDs such as ContextMenus.
/// </summary>
public static class MenuGlassEffect
{
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Desktop Acrylic

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_ENABLE_HOSTBACKDROP = 5
    }

    public static void ApplyBlur(IntPtr hwnd, bool isDark = true)
    {
        if (hwnd == IntPtr.Zero) return;

        // Try SetWindowCompositionAttribute (Acrylic / BlurBehind on layered WPF popups)
        try
        {
            var policy = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 2,
                // Soft gradient tint in ABGR format
                GradientColor = isDark ? 0x01101724 : 0x01FFFFFF
            };

            int size = Marshal.SizeOf(policy);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, buffer, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = buffer,
                    SizeOfData = size
                };
                int res = SetWindowCompositionAttribute(hwnd, ref data);
                if (res == 0)
                {
                    // Fallback to standard blur behind
                    policy.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
                    policy.GradientColor = 0;
                    Marshal.StructureToPtr(policy, buffer, false);
                    _ = SetWindowCompositionAttribute(hwnd, ref data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // Ignore if composition attribute is unsupported
        }

        // Also hint DWM TransientWindow backdrop where supported (Windows 11 22H2+)
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            try
            {
                int backdrop = DWMSBT_TRANSIENTWINDOW;
                _ = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch
            {
                // Ignore
            }
        }
    }
}
