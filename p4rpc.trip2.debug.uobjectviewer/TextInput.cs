extern alias imgui;
using System.Runtime.CompilerServices;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using System.Runtime.InteropServices;
using System.Text;

namespace p4rpc.trip2.debug.uobjectviewer;

public class ResizableTextInput : IDisposable
{
    private static nuint STARTING_BUFFER_SIZE = 0x8;
    private bool bIsDisposed = false;
    private string label;
    private static nint resizableTextCb;
    private bool bDebug = false;

    // To sync buffer ptr/size between native callback and managed data
    private unsafe ResizableTextInputNative* pUserData;
    public unsafe struct ResizableTextInputNative
    {
        public sbyte* buf;
        public int bufSize;
        public bool bDirty;
        public int dirtyTextLen;
    }

    public unsafe ResizableTextInput(string _label)
    {
        label = _label;
        pUserData = (ResizableTextInputNative*)NativeMemory.AllocZeroed((nuint)sizeof(ResizableTextInputNative));
        pUserData->buf = (sbyte*)NativeMemory.AllocZeroed(STARTING_BUFFER_SIZE);
        pUserData->bufSize = (int)STARTING_BUFFER_SIZE;
        pUserData->bDirty = false;
        pUserData->dirtyTextLen = 0;
        resizableTextCb = ResizableTextGetPtr(&ResizableTextCallback);
    }
    public unsafe bool Draw(ImGuiInputTextFlags flags = 0)
    {
        if (bDebug)
            ImGui.Text($"Buffer: {(nint)pUserData->buf:X}, Capacity: {pUserData->bufSize}");
        return ImGui.__Internal.InputText(label, pUserData->buf, pUserData->bufSize, 
            (int)(flags | ImGuiInputTextFlags.CallbackResize | ImGuiInputTextFlags.CallbackEdit), 
            resizableTextCb, (nint)pUserData
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static nint ResizableTextGetPtr(delegate* unmanaged[Stdcall]<nint, int> cb) => (nint)cb;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)]), SuppressGCTransition]
    public static unsafe int ResizableTextCallback(nint pData)
    {
        var data = (ImGuiInputTextCallbackData.__Internal*)pData;
        var userData = (ResizableTextInputNative*)data->UserData;
        
        
        switch ((ImGuiInputTextFlags)data->EventFlag)
        {
            case ImGuiInputTextFlags.CallbackResize:
                var bufSize = &userData->bufSize;
                // native code resize
                if ((uint)data->BufSize > *bufSize)
                {
                    var newSize = *bufSize;
                    while (newSize < data->BufSize)
                        newSize *= 2;
                    // Console.WriteLine($"Resize {(nint)userData:X}: {userData->bufSize} -> {newSize}");
                    var nBuf = NativeMemory.AllocZeroed((nuint)newSize);
                    NativeMemory.Copy((void*)data->Buf, nBuf, (nuint)(*bufSize));
                    NativeMemory.Free((void*)data->Buf);
                    userData->buf = (sbyte*)nBuf;
                    *bufSize = newSize;
                    data->Buf = (nint)userData->buf;
                }
                break;
            case ImGuiInputTextFlags.CallbackEdit:
                if (userData->bDirty)
                {
                    data->BufTextLen = userData->dirtyTextLen;
                    userData->bDirty = false;
                }
                break;
        }
        return 0;
    }

    public unsafe void ReplaceBuffer(string str)
    {
        var bStr = Encoding.UTF8.GetBytes(str);
        if (bStr.Length > pUserData->bufSize)
        { // managed resize
            int newSize = pUserData->bufSize;
            while (newSize < bStr.Length)
                newSize *= 2;
            var nBuf = (sbyte*)NativeMemory.Alloc((nuint)newSize);
            NativeMemory.Free(pUserData->buf);
            pUserData->buf = nBuf;
            pUserData->bufSize = newSize;
        }
        NativeMemory.Clear(pUserData->buf, (nuint)pUserData->bufSize);
        fixed (byte* pStr = bStr)
            NativeMemory.Copy(pStr, pUserData->buf, (nuint)bStr.Length);
        pUserData->dirtyTextLen = bStr.Length;
        pUserData->bDirty = true;
    }

    public unsafe void* GetBuffer() => pUserData->buf;

    // impl IDisposable
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    ~ResizableTextInput() => Dispose(false);
    protected virtual unsafe void Dispose(bool disposing)
    {
        if (!bIsDisposed)
        {
            NativeMemory.Free(pUserData->buf);
            NativeMemory.Free(pUserData);
        }
    }
}