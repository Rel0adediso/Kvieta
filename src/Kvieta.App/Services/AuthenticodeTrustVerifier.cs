using System.Runtime.InteropServices;
using System.IO;

namespace Kvieta.App.Services;

public static class AuthenticodeTrustVerifier
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    public static bool IsTrusted(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

        IntPtr pathPointer = IntPtr.Zero;
        IntPtr fileInfoPointer = IntPtr.Zero;
        WINTRUST_DATA trustData = new();
        try
        {
            pathPointer = Marshal.StringToCoTaskMemUni(filePath);
            WINTRUST_FILE_INFO fileInfo = new()
            {
                StructSize = checked((uint)Marshal.SizeOf<WINTRUST_FILE_INFO>()),
                FilePath = pathPointer
            };
            fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, fDeleteOld: false);
            trustData = new WINTRUST_DATA
            {
                StructSize = checked((uint)Marshal.SizeOf<WINTRUST_DATA>()),
                UiChoice = 2,
                RevocationChecks = 0,
                UnionChoice = 1,
                FileInfo = fileInfoPointer,
                StateAction = 1,
                ProviderFlags = 0x00001000
            };
            Guid action = GenericVerifyV2;
            return WinVerifyTrust(new IntPtr(-1), ref action, ref trustData) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (trustData.StateData != IntPtr.Zero)
            {
                trustData.StateAction = 2;
                Guid action = GenericVerifyV2;
                _ = WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
            }
            if (fileInfoPointer != IntPtr.Zero) Marshal.FreeHGlobal(fileInfoPointer);
            if (pathPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pathPointer);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WinVerifyTrust(IntPtr windowHandle, ref Guid actionId, ref WINTRUST_DATA trustData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint StructSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
