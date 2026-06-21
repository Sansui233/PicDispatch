using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace PicDispatch.Services
{
    public class FolderPickerService
    {
        private const uint FOS_OVERWRITEPROMPT = 0x00000002;
        private const uint FOS_STRICTFILETYPES = 0x00000004;
        private const uint FOS_NOCHANGEDIR = 0x00000008;
        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST = 0x00000800;
        private const uint FOS_FILEMUSTEXIST = 0x00001000;
        private const uint FOS_NOREADONLYRETURN = 0x00008000;
        private const uint SIGDN_FILESYSPATH = 0x80058000;
        private const int ERROR_CANCELLED = unchecked((int)0x800704C7);

        public string PickFolder(Window owner, string initialPath)
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                if (TryPickWithModernDialog(owner, initialPath, out var modernPath))
                {
                    return modernPath;
                }
            }

            return PickWithLegacyDialog(initialPath);
        }

        private static bool TryPickWithModernDialog(Window owner, string initialPath, out string selectedPath)
        {
            IFileOpenDialog dialog = null;
            selectedPath = null;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialog();
                dialog.GetOptions(out var options);
                dialog.SetOptions(options |
                                  FOS_PICKFOLDERS |
                                  FOS_FORCEFILESYSTEM |
                                  FOS_PATHMUSTEXIST |
                                  FOS_FILEMUSTEXIST |
                                  FOS_NOCHANGEDIR |
                                  FOS_NOREADONLYRETURN);

                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                {
                    SHCreateItemFromParsingName(initialPath, IntPtr.Zero, typeof(IShellItem).GUID, out var folderItem);
                    try
                    {
                        dialog.SetFolder(folderItem);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(folderItem);
                    }
                }

                var ownerHandle = owner == null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
                var hr = dialog.Show(ownerHandle);
                if (hr == ERROR_CANCELLED)
                {
                    return true;
                }

                Marshal.ThrowExceptionForHR(hr);
                dialog.GetResult(out var result);
                result.GetDisplayName(SIGDN_FILESYSPATH, out var pathPointer);

                try
                {
                    selectedPath = Marshal.PtrToStringUni(pathPointer);
                    return true;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPointer);
                    Marshal.ReleaseComObject(result);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (dialog != null)
                {
                    Marshal.ReleaseComObject(dialog);
                }
            }
        }

        private static string PickWithLegacyDialog(string initialPath)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                {
                    dialog.SelectedPath = initialPath;
                }

                return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr bindingContext,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        [ComImport]
        [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);

            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint fos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(IntPtr ppenum);
            void GetSelectedItems(IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }
}
