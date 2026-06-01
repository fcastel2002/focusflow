using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FocusAnchor.Data.Google;

public sealed class WindowsGoogleCredentialStore : IGoogleCredentialStore
{
    private const string TargetName = "FocusAnchor.GoogleCalendar.RefreshToken";

    public string? ReadRefreshToken()
    {
        if (!CredRead(TargetName, 1, 0, out var credentialPointer))
        {
            return Marshal.GetLastWin32Error() == 1168
                ? null
                : throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPointer);
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void SaveRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

        try
        {
            var credential = new Credential
            {
                Type = 1,
                TargetName = TargetName,
                CredentialBlobSize = bytes.Length,
                CredentialBlob = handle.AddrOfPinnedObject(),
                Persist = 2,
                UserName = "FocusAnchor"
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            handle.Free();
        }
    }

    public void DeleteRefreshToken()
    {
        if (!CredDelete(TargetName, 1, 0) && Marshal.GetLastWin32Error() != 1168)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref Credential userCredential, [In] uint flags);

    [DllImport("advapi32", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);
}
