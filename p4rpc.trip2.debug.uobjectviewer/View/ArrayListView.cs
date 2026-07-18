extern alias imgui;
using System.Numerics;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using RyoTune.Reloaded;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class ArrayPropertyViewer(nint baseAddress, IFProperty property, int index,
    IUnrealClasses classes, IUnrealMemory memory, nint arrayAddress) : BasePropertyViewer(baseAddress, property)
{
    private int Index => index;
    private nint ArrayAddress => arrayAddress;
    
    protected IUnrealClasses Classes => classes;
    protected IUnrealMemory Memory => memory;
    
    public override void Draw(App owner, UObjectWindow window)
    {
        ImGui.TableNextRow(0, 0);
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{Index}");
        ImGui.TableSetColumnIndex(1);
        Inner.GetViewer(owner, window).Draw();
        ImGui.TableSetColumnIndex(2);
        if (ImGui.Button($"Remove##{Inner.BaseAddress:X}", ImGui.ImVec2ImVec2Nil()))
        {
            unsafe
            {
                var ArrayRepr = (TArray<byte>*)ArrayAddress;
                var GetEntry = (int i) => (nint)ArrayRepr->AllocatorInstance + i * Inner.Property.ElementSize;
                for (var i = Index; i < ArrayRepr->ArrayNum - 1; i++)
                {
                    NativeMemory.Copy((void*)GetEntry(i + 1), (void*)GetEntry(i),
                        (nuint)Inner.Property.ElementSize);
                }
                ArrayRepr->ArrayNum--;
            }
        }
        ImGui.SameLine(0, 10);
        // Duplicate the entry instead of creating a blank entry so we don't crash on types that
        // require dereferencing to display such as FText
        if (ImGui.Button($"Copy Above##{Inner.BaseAddress:X}", ImGui.ImVec2ImVec2Nil()))
        {
            unsafe
            {
                var ArrayRepr = (TArray<byte>*)ArrayAddress;
                var GetEntry = (int i) => (nint)ArrayRepr->AllocatorInstance + i * Inner.Property.ElementSize;
                // try resize
                if (ArrayRepr->ArrayNum == ArrayRepr->ArrayMax)
                {
                    TArrayListStatic.ResizeToStatic(
                        ArrayRepr, TArrayListStatic.CalculateNewArraySizeStatic(ArrayRepr), 
                        Inner.Property.ElementSize, Memory);   
                }
                for (var i = ArrayRepr->ArrayNum; i > Index; i--)
                {
                    NativeMemory.Copy((void*)GetEntry(i - 1), (void*)GetEntry(i),
                        (nuint)Inner.Property.ElementSize);
                }
                /*
                NativeMemory.Clear(
                    (void*)((nint)ArrayRepr->AllocatorInstance + Index * Inner.Property.ElementSize),
                    (nuint)Inner.Property.ElementSize);
                */
                ArrayRepr->ArrayNum++;
            }
        }
    }
}

public class ArrayListView(PropertyListView? parent, nint baseAddress, IFArrayProperty value,
    IUnrealClasses classes, IUnrealMemory memory) : PropertyListView(parent)
{
    
    protected readonly nint BaseAddress = baseAddress;
    protected readonly IFArrayProperty Value = value;
    protected IUnrealClasses Classes => classes;
    protected IUnrealMemory Memory => memory;
    
    private static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Index", _ => 0),
        new("Value", _ => 0),
        new("Actions", _ => 0),
    ];
    
    public override void Draw(App owner, UObjectWindow window)
    {
        ImGui.Text($"Data Type is {owner.TypeName.GetPropertyTypeName(Value.Inner)}");
        ImGui.SameLine(0, 10);
        if (ImGui.Button($"Copy Last Element##{BaseAddress:x}", ImGui.ImVec2ImVec2Nil()))
        {
            unsafe
            {
                var ArrayRepr = (TArray<byte>*)BaseAddress;
                var GetEntry = (int i) => (nint)ArrayRepr->AllocatorInstance + i * Value.Inner.ElementSize;
                if (ArrayRepr->ArrayNum == ArrayRepr->ArrayMax)
                {
                    TArrayListStatic.ResizeToStatic(
                        ArrayRepr, TArrayListStatic.CalculateNewArraySizeStatic(ArrayRepr), 
                        Value.Inner.ElementSize, Memory);   
                }
                NativeMemory.Copy((void*)GetEntry(ArrayRepr->ArrayNum - 1), (void*)GetEntry(ArrayRepr->ArrayNum),
                    (nuint)Value.Inner.ElementSize);
                /*
                NativeMemory.Clear(
                    (void*)((nint)ArrayRepr->AllocatorInstance + ArrayRepr->ArrayNum * Value.Inner.ElementSize),
                    (nuint)value.Inner.ElementSize);
                */
                ArrayRepr->ArrayNum++;
            }
        }
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        var regionAvailable = new Vector2(regionAvail.x, regionAvail.y);
        if (ImGui.BeginTable($"##ArrayTable{BaseAddress:x}", 3, (int)flags, 
                ImGui.ImVec2ImVec2Nil(), 0))
        {
            // var columnFlags = (int)ImGuiTableColumnFlags.WidthFixed;
            var columnFlags = 0;
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column.Name, columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
            ImGui.TableHeadersRow();
            unsafe
            {
                var ArrayRepr = (TArray<byte>*)BaseAddress;
                for (var i = 0; i < ArrayRepr->ArrayNum; i++)
                {
                    var Address = (nint)ArrayRepr->AllocatorInstance + i * Value.Inner.ElementSize;
                    new ArrayPropertyViewer(Address, Value.Inner, i, Classes, Memory, BaseAddress).Draw(owner, window);
                }
            }
            ImGui.EndTable();
        }
    }
    
    public override string ToString() => $"{GetKey():X}";

    public override PropertyListKey GetKey() => new(BaseAddress, Classes.GetPropertyTypeName(Value));
}