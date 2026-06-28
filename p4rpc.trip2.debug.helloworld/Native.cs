using System.Runtime.InteropServices;

namespace p4rpc.trip2.debug.helloworld;

public static class Kernel32
{
    [DllImport("kernel32", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
    public static extern nint LoadLibraryA(string libFileName);
    
    [DllImport("kernel32", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
    public static extern nint GetProcAddress(nint module, string procName);
}

#region PEResource

#pragma warning disable CS0649

internal struct LanguageCodePage
{
    internal short wLanguage;
    internal short wCodePage;
}

internal struct FixedFileInfo
{
    internal uint dwSignature;
    internal uint dwStrucVersion;
    internal uint dwFileVersionMS;
    internal uint dwFileVersionLS;
    internal uint dwProductVersionMS;
    internal uint dwProductVersionLS;
    internal uint dwFileFlagsMask;
    internal uint dwFileFlags;
    internal uint dwFileOS;
    internal uint dwFileType;
    internal uint dwFileSubtype;
    internal uint dwFileDateMS;
    internal uint dwFileDateLS;
}

#pragma warning restore CS0649

#endregion