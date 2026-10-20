using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexBar.Windows.Tray;

internal static class TaskbarShell
{
    // Call only after TaskbarButtonCreated. Updating the native window frame does
    // not invalidate Explorer's cached taskbar image; an empty overlay update does.
    // Avalonia uses the same refresh in its Win32 WindowImpl.RefreshIcon.
    public static void RefreshIcon(IntPtr windowHandle)
    {
        object? shell = null;
        try
        {
            shell = new TaskbarList();
            var taskbar = (ITaskbarList3)shell;
            Marshal.ThrowExceptionForHR(taskbar.HrInit());
            Marshal.ThrowExceptionForHR(taskbar.SetOverlayIcon(windowHandle, IntPtr.Zero, null));
        }
        catch (COMException exception)
        {
            // Explorer can be unavailable during sign-in/restart. Keep the app
            // usable; the next TaskbarButtonCreated will request a fresh update.
            Trace.TraceWarning($"Taskbar icon refresh failed: {exception.Message}");
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
    private class TaskbarList
    {
    }

    // Preserve the complete native vtable order, including inherited methods.
    // Only HrInit and SetOverlayIcon are used; pointers represent unused native
    // THUMBBUTTON arrays and RECT structures without introducing extra wrappers.
    [ComImport]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        [PreserveSig]
        int HrInit();
        [PreserveSig]
        int AddTab(IntPtr windowHandle);
        [PreserveSig]
        int DeleteTab(IntPtr windowHandle);
        [PreserveSig]
        int ActivateTab(IntPtr windowHandle);
        [PreserveSig]
        int SetActiveAlt(IntPtr windowHandle);
        [PreserveSig]
        int MarkFullscreenWindow(IntPtr windowHandle, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        [PreserveSig]
        int SetProgressValue(IntPtr windowHandle, ulong completed, ulong total);
        [PreserveSig]
        int SetProgressState(IntPtr windowHandle, uint flags);
        [PreserveSig]
        int RegisterTab(IntPtr tabHandle, IntPtr parentHandle);
        [PreserveSig]
        int UnregisterTab(IntPtr tabHandle);
        [PreserveSig]
        int SetTabOrder(IntPtr tabHandle, IntPtr insertBefore);
        [PreserveSig]
        int SetTabActive(IntPtr tabHandle, IntPtr parentHandle, uint reserved);
        [PreserveSig]
        int ThumbBarAddButtons(IntPtr windowHandle, uint count, IntPtr buttons);
        [PreserveSig]
        int ThumbBarUpdateButtons(IntPtr windowHandle, uint count, IntPtr buttons);
        [PreserveSig]
        int ThumbBarSetImageList(IntPtr windowHandle, IntPtr imageList);
        [PreserveSig]
        int SetOverlayIcon(IntPtr windowHandle, IntPtr icon, [MarshalAs(UnmanagedType.LPWStr)] string? description);
        [PreserveSig]
        int SetThumbnailTooltip(IntPtr windowHandle, [MarshalAs(UnmanagedType.LPWStr)] string? tooltip);
        [PreserveSig]
        int SetThumbnailClip(IntPtr windowHandle, IntPtr clip);
    }
}
