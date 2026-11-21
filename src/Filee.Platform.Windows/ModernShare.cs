// Morph change: Windows' Share panel for the "Share" file action (Nearby sharing, Mail, Teams, Phone Link and the other
// apps that take files), the way Explorer's own "Share" command opens it: the shell's share handler is given the files
// as a data object and its "Windows.ModernShare" command is invoked. Runs on the UI thread (an STA one, as the shell
// expects). False when Windows has no share handler or refuses, so the caller can say so.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace Filee.Platform.Windows;

[SupportedOSPlatform("windows")]
internal static class ModernShare
{
    /// <summary>The shell's share handler (the one behind Explorer's "Share").</summary>
    private static readonly Guid ShareHandler = new("e2bf9676-5f8f-435c-97eb-11607a5bedf7");

    private static readonly Guid BhidDataObject = new("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");
    private static readonly Guid IidDataObject = new("0000010e-0000-0000-C000-000000000046");

    private const string Verb = "Windows.ModernShare";

    internal static bool Show(IReadOnlyList<string> paths, nint window)
    {
        var pidls = new List<nint>();
        nint menu = 0;
        try
        {
            foreach (var path in paths.Select(Path.GetFullPath))
            {
                var pidl = ILCreateFromPathW(path);
                if (pidl == 0)
                    return false;
                pidls.Add(pidl);
            }
            if (pidls.Count == 0 || SHCreateShellItemArrayFromIDLists((uint)pidls.Count, [.. pidls], out var items) != 0)
                return false;
            var bhid = BhidDataObject;
            var iid = IidDataObject;
            if (items.BindToHandler(0, ref bhid, ref iid, out var dataPointer) != 0 || dataPointer == 0)
                return false;
            var data = (IDataObject)Marshal.GetObjectForIUnknown(dataPointer);
            Marshal.Release(dataPointer);

            var type = Type.GetTypeFromCLSID(ShareHandler, throwOnError: false);
            if (type is null || Activator.CreateInstance(type) is not { } handler)
                return false;
            if (((IShellExtInit)handler).Initialize(0, data, 0) != 0)
                return false;
            var contextMenu = (IContextMenu)handler;
            menu = CreatePopupMenu();
            contextMenu.QueryContextMenu(menu, 0, 1, 0x7FFF, 0);

            var verbA = Marshal.StringToHGlobalAnsi(Verb);
            var verbW = Marshal.StringToHGlobalUni(Verb);
            try
            {
                var info = new CommandInfo
                {
                    Size = (uint)Marshal.SizeOf<CommandInfo>(),
                    Mask = UnicodeMask,
                    Window = window,
                    Verb = verbA,
                    VerbW = verbW,
                    Show = 1,
                };
                return contextMenu.InvokeCommand(ref info) == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(verbA);
                Marshal.FreeHGlobal(verbW);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (menu != 0)
                DestroyMenu(menu);
            foreach (var pidl in pidls)
                ILFree(pidl);
        }
    }

    private const uint UnicodeMask = 0x00004000; // CMIC_MASK_UNICODE

    [StructLayout(LayoutKind.Sequential)]
    private struct CommandInfo
    {
        public uint Size;
        public uint Mask;
        public nint Window;
        public nint Verb;
        public nint Parameters;
        public nint Directory;
        public int Show;
        public uint HotKey;
        public nint Icon;
        public nint Title;
        public nint VerbW;
        public nint ParametersW;
        public nint DirectoryW;
        public nint TitleW;
        public int X;
        public int Y;
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        [PreserveSig]
        int BindToHandler(nint context, ref Guid handler, ref Guid interfaceId, out nint result);
    }

    [ComImport, Guid("000214E8-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellExtInit
    {
        [PreserveSig]
        int Initialize(nint folder, IDataObject data, nint progIdKey);
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);

        [PreserveSig]
        int InvokeCommand(ref CommandInfo info);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint ILCreateFromPathW(string path);

    [DllImport("shell32.dll")]
    private static extern void ILFree(nint pidl);

    [DllImport("shell32.dll")]
    private static extern int SHCreateShellItemArrayFromIDLists(uint count, nint[] pidls, out IShellItemArray items);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);
}
