using System.Diagnostics;
using System.Runtime.InteropServices;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debug.helloworld;

public static class GameVersion
{
    private static nint WinVerDLL;

    private const string WinVerDLLName = "Api-ms-win-core-version-l1-1-0.dll";
    private const string RootBlock = "\\";
    private const string TranslateBlock = "\\VarFileInfo\\Translation";
    private const uint FixedFileInfoMagic = 0xfeef04bd;

    private static nint GetWinVerDLL()
    {
        if (WinVerDLL != nint.Zero) return WinVerDLL;
        WinVerDLL = Kernel32.LoadLibraryA(WinVerDLLName);
        Log.Debug($"Got {WinVerDLLName} at 0x{WinVerDLL:x}");
        return WinVerDLL;
    }

    private static byte[]? FileVersionInfo;
    
    // From UnrealEssentials
    private static byte[] GetFileVersionInfo()
    {
        if (FileVersionInfo != null)
            return FileVersionInfo;
        var mainModule = Process.GetCurrentProcess().MainModule!;
        unsafe
        {
            var getFileVersionInfoSizeA = (delegate* unmanaged[Stdcall]<string, uint*, uint>)Kernel32.GetProcAddress(
                GetWinVerDLL(), "GetFileVersionInfoSizeA");
            if (getFileVersionInfoSizeA == null) return [];
            var infoSize = getFileVersionInfoSizeA(mainModule.FileName, null);
            FileVersionInfo = new byte[infoSize];
            var getFileVersionInfoA = (delegate* unmanaged[Stdcall]<string, uint, uint, byte*, bool>)Kernel32.GetProcAddress(
                GetWinVerDLL(), "GetFileVersionInfoA");
            if (getFileVersionInfoA == null) return [];
            fixed (byte* pInfoBuffer = FileVersionInfo)
                if (!getFileVersionInfoA(mainModule.FileName, 0, infoSize, pInfoBuffer))
                    return [];
            return FileVersionInfo;
        }
    }

    private static unsafe FixedFileInfo* GetFixedFileInfo(byte* pInfoBuffer)
    {
        // https://learn.microsoft.com/en-us/windows/win32/api/winver/nf-winver-verqueryvaluea
        var verQueryValueA = (delegate* unmanaged[Stdcall]<byte*, string, nint*, uint*, bool>)
            Kernel32.GetProcAddress(GetWinVerDLL(), "VerQueryValueA");
        if (verQueryValueA == null) return null;
        FixedFileInfo* root = null;
        uint rootSize = 0;
        if (!verQueryValueA(pInfoBuffer, RootBlock, (nint*)(&root), &rootSize) 
            || root->dwSignature != FixedFileInfoMagic) return null;
        return root;
    }

    public static string? GetFileVersion()
    {
        unsafe
        {
            fixed (byte* pInfoBuffer = GetFileVersionInfo())
            {
                var root = GetFixedFileInfo(pInfoBuffer);
                if (root == null) return null;
                var major = root->dwFileVersionMS >> 0x10;
                var minor = root->dwFileVersionMS & 0xffff;
                var revision = root->dwFileVersionLS >> 0x10;
                var patch = root->dwFileVersionLS & 0xffff;
                return $"{major}.{minor}.{revision}.{patch}";
            }   
        }
    }

    public static string? GetProductVersion()
    {
        unsafe
        {
            fixed (byte* pInfoBuffer = GetFileVersionInfo())
            {
                var root = GetFixedFileInfo(pInfoBuffer);
                if (root == null) return null;
                var major = root->dwProductVersionMS >> 0x10;
                var minor = root->dwProductVersionMS & 0xffff;
                var revision = root->dwProductVersionLS >> 0x10;
                var patch = root->dwProductVersionLS & 0xffff;
                return $"{major}.{minor}.{revision}.{patch}";
            }   
        }
    }

    public static string? GetLocalizedProperty(string PropertyName)
    {
        unsafe
        {
            fixed (byte* pInfoBuffer = GetFileVersionInfo())
            {
                // https://learn.microsoft.com/en-us/windows/win32/api/winver/nf-winver-verqueryvaluea
                var verQueryValueA = (delegate* unmanaged[Stdcall]<byte*, string, nint*, uint*, bool>)
                    Kernel32.GetProcAddress(GetWinVerDLL(), "VerQueryValueA");
                if (verQueryValueA == null) return null;
                // Get language + codepage
                LanguageCodePage* translate = null;
                uint translateSize = 0;
                if (!verQueryValueA(pInfoBuffer, TranslateBlock, (nint*)(&translate), &translateSize)) return null;
                for (var i = 0; i < translateSize / sizeof(LanguageCodePage); i++)
                {
                    // Check FileDescription entry for StringFileInfo
                    var translateEntry = translate + i;
                    char* fileDescription = null;
                    uint fileDescBytes = 0;
                    // VerQueryValue for strings includes null terminator in length
                    if (!verQueryValueA(pInfoBuffer,
                            $"\\StringFileInfo\\{translateEntry->wLanguage:x04}{translateEntry->wCodePage:x04}\\{PropertyName}",
                            (nint*)(&fileDescription), &fileDescBytes))
                        return null;
                    var nameDesc = Marshal.PtrToStringAnsi((nint)fileDescription, (int)fileDescBytes - 1);
                    return nameDesc;
                }
                return null;
            }
        }
    }
}