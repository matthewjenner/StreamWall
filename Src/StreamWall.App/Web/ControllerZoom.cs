using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace StreamWall.App.Web;

/// <summary>
/// Zoom for one web view. Zoom belongs to the WebView2 controller, which the SDK cannot wrap from
/// the pointer Avalonia hands out, and the SDK's raw COM interfaces are embedded interop types that
/// C# cannot reference. So this declares the controller interface itself (GUID and method order
/// read from the SDK's own definition) to read and set <c>ZoomFactor</c>.
/// </summary>
/// <remarks>
/// Changes (Ctrl+wheel, Ctrl+plus/minus, the toolbar) are noticed by reading the factor twice a
/// second, not through the ZoomFactorChanged event: WebView2 could not call back into a
/// self-declared handler (verified in the demo: setting worked, the event never arrived). UI thread
/// only, like everything WebView2.
/// </remarks>
public sealed class ControllerZoom : IDisposable
{
    private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(500);

    private readonly Action<double> _changed;
    private readonly DispatcherTimer _poll = new() { Interval = PollEvery };
    private IWebView2Controller? _controller;
    private double _last;

    public ControllerZoom(IntPtr controller, Action<double> changed)
    {
        _changed = changed;
        _controller = (IWebView2Controller)Marshal.GetObjectForIUnknown(controller);
        _last = Factor;
        _poll.Tick += (_, _) => Check();
        _poll.Start();
    }

    public double Factor
    {
        get => _controller is not null && _controller.get_ZoomFactor(out double z) == 0 ? z : 1.0;
        set
        {
            if (_controller is not null && Math.Abs(Factor - value) > 0.001)
            {
                Marshal.ThrowExceptionForHR(_controller.put_ZoomFactor(value));
                Check();
            }
        }
    }

    public void Dispose()
    {
        _poll.Stop();
        if (_controller is null)
            return;
        Marshal.ReleaseComObject(_controller);
        _controller = null;
    }

    private void Check()
    {
        double now = Factor;
        if (_controller is null || Math.Abs(now - _last) < 0.001)
            return;
        _last = now;
        _changed(now);
    }

    /// <summary>ICoreWebView2Controller, only up to the zoom members (COM calls go by vtable position).</summary>
    [ComImport]
    [Guid("4D00C0D1-9434-4EB6-8078-8697A560334F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWebView2Controller
    {
        [PreserveSig] int get_IsVisible(out int isVisible);
        [PreserveSig] int put_IsVisible(int isVisible);
        [PreserveSig] int get_Bounds(out Rect bounds);
        [PreserveSig] int put_Bounds(Rect bounds);
        [PreserveSig] int get_ZoomFactor(out double zoomFactor);
        [PreserveSig] int put_ZoomFactor(double zoomFactor);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
}
