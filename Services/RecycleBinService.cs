using System.Runtime.InteropServices;
using DuplicateFinder.Data;
using DuplicateFinder.Models;

namespace DuplicateFinder.Services;

public static class RecycleBinService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    /// <summary>
    /// Envoie un fichier dans la corbeille Windows et le supprime de la base.
    /// </summary>
    public static bool SendToRecycleBin(FileEntry file, FileRepository repo)
    {
        if (!File.Exists(file.FullPath)) return false;

        // Double null-terminator requis par l'API Shell
        var path = file.FullPath + "\0\0";
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path,
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
        };

        int result = SHFileOperation(ref op);
        if (result == 0 && !op.fAnyOperationsAborted)
        {
            repo.DeleteFile(file.Id);
            return true;
        }
        return false;
    }
}
