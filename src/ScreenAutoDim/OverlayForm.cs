using System.Runtime.InteropServices;

namespace ScreenAutoDim;

/// <summary>A black, click-through, always-on-top window covering one monitor. Its opacity is the dimming amount.</summary>
internal sealed class OverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WM_DPICHANGED = 0x02E0;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private readonly Rectangle _bounds;

    public OverlayForm(Rectangle bounds)
    {
        _bounds = bounds;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Opacity = 0;
        Bounds = bounds;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // The overlay must keep the exact pixel rectangle of its monitor, so never let a DPI change resize it.
        if (m.Msg == WM_DPICHANGED)
            return;
        base.WndProc(ref m);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Reposition();
    }

    /// <summary>Snaps the window to its monitor and back above other topmost windows.</summary>
    public void Reposition()
    {
        if (IsHandleCreated)
            SetWindowPos(Handle, HWND_TOPMOST, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, SWP_NOACTIVATE);
    }

    /// <param name="dimming">0 = transparent, 1 = fully black.</param>
    public void SetDimming(double dimming) => Opacity = Math.Clamp(dimming, 0, 1 - AppConfig.MinBrightness / 100.0);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
