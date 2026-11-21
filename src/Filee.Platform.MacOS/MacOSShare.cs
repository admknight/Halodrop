// Morph change: the macOS share menu (NSSharingServicePicker) for the "Share" file action: AirDrop, Mail, Messages,
// Notes and whatever else the Mac offers, for the files themselves. Shown next to the given window (an NSWindow or an
// NSView, as Avalonia hands it over). Runs on the main thread, as AppKit wants.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Filee.Platform.MacOS;

[SupportedOSPlatform("macos")]
internal static class MacOSShare
{
    private const string ObjC = "/usr/lib/libobjc.dylib";

    private static readonly nint AppKit = NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");

    /// <summary>The picker on screen: AppKit doesn't keep it alive while its menu is open.</summary>
    private static nint _picker;

    /// <summary>NSMinYEdge: the menu opens below the point.</summary>
    private const nint BelowEdge = 1;

    internal static bool Show(IReadOnlyList<string> paths, nint window)
    {
        GC.KeepAlive(AppKit);
        if (paths.Count == 0 || window == 0)
            return false;
        var view = SendBool(window, Sel("respondsToSelector:"), Sel("contentView")) ? Send(window, Sel("contentView")) : window;
        if (view == 0)
            return false;

        var items = Send(Class("NSMutableArray"), Sel("array"));
        foreach (var path in paths.Select(Path.GetFullPath))
        {
            var text = Marshal.StringToCoTaskMemUTF8(path);
            try
            {
                var str = Send(Class("NSString"), Sel("stringWithUTF8String:"), text);
                SendVoid(items, Sel("addObject:"), Send(Class("NSURL"), Sel("fileURLWithPath:"), str));
            }
            finally
            {
                Marshal.FreeCoTaskMem(text);
            }
        }

        if (_picker != 0)
            SendVoid(_picker, Sel("release"));
        _picker = Send(Send(Class("NSSharingServicePicker"), Sel("alloc")), Sel("initWithItems:"), items);
        if (_picker == 0)
            return false;
        var bounds = SendRect(view, Sel("bounds"));
        var spot = new CGRect(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, 1, 1);
        ShowPicker(_picker, Sel("showRelativeToRect:ofView:preferredEdge:"), spot, view, BelowEdge);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CGRect(double X, double Y, double Width, double Height);

    private static nint Class(string name) => objc_getClass(name);
    private static nint Sel(string name) => sel_registerName(name);

    [DllImport(ObjC)]
    private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector, nint argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector, nint argument);

    // A CGRect (four doubles) comes back in registers on arm64 and through objc_msgSend_stret on x64.
    private static CGRect SendRect(nint receiver, nint selector) =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? SendRectArm(receiver, selector) : SendRectStret(receiver, selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern CGRect SendRectArm(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend_stret")]
    private static extern CGRect SendRectStret(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void ShowPicker(nint receiver, nint selector, CGRect rect, nint view, nint edge);
}
