extern alias imgui;
using System.Numerics;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class MapPropertyViewer(nint baseAddress, IFProperty property, int index) 
    : BasePropertyViewer(baseAddress, property)
{

    private int Index => index;
    
    public override void Draw(App owner, UObjectWindow window)
    {
        ImGui.TableNextRow(0, 0);
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{Index}");
        ImGui.TableSetColumnIndex(1);
        GetViewer(owner, window).Draw();
        ImGui.TableSetColumnIndex(2);
        if (ImGui.Button($"Remove##{BaseAddress:X}", ImGui.ImVec2ImVec2Nil())) {}
    }
}

public class MapListView(PropertyListView? parent, IntPtr baseAddress, IFMapProperty value) : PropertyListView(parent)
{
    protected readonly nint BaseAddress = baseAddress;
    protected readonly IFMapProperty Value = value;
    
    private static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Key", _ => 0),
        new("Value", _ => 0),
        new("Remove", _ => 0)
    ];
    
    public override void Draw(App owner, UObjectWindow window)
    {
        var KeyType = owner.TypeName.GetPropertyTypeName(Value.KeyProp);
        var ValueType = owner.TypeName.GetPropertyTypeName(Value.ValueProp);
        ImGui.Text($"Key is {KeyType}, Value is {ValueType}");
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        var regionAvailable = new Vector2(regionAvail.x, regionAvail.y);
        if (ImGui.BeginTable("##UEToolkitUnitTests", 3, (int)flags, ImGui.ImVec2ImVec2Float(0, 0), 0))
        {
            // var columnFlags = (int)ImGuiTableColumnFlags.WidthFixed;
            var columnFlags = 0;
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column.Name, columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
            ImGui.TableHeadersRow();
            unsafe
            {
                /*
                var ArrayRepr = (TArray<byte>*)BaseAddress;
                for (var i = 0; i < ArrayRepr->ArrayNum; i++)
                {
                    var Address = BaseAddress + i * Value.Inner.ElementSize;
                    new ArrayPropertyViewer(Address, Value.Inner, i).Draw(owner, window);
                }
                */
            }
            ImGui.EndTable();
        }
    }
    
    public override string ToString() => $"{GetBaseAddress():X}";

    public override nint GetBaseAddress() => BaseAddress;
}