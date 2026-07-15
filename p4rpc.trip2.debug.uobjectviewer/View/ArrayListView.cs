extern alias imgui;
using System.Numerics;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class ArrayPropertyViewer(nint baseAddress, IFProperty property, int index) 
    : BasePropertyViewer(baseAddress, property)
{
    private int Index => index;
    
    public override void Draw(App owner, UObjectWindow window)
    {
        ImGui.TableNextRow(0, 0);
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{Index}");
        ImGui.TableSetColumnIndex(1);
        Inner.GetViewer(owner, window).Draw();
        ImGui.TableSetColumnIndex(2);
        ImGui.BeginDisabled(true);
        if (ImGui.Button($"Remove##{Inner.BaseAddress:X}", ImGui.ImVec2ImVec2Nil())) {}
        ImGui.TableSetColumnIndex(3);
        if (ImGui.Button($"Insert Before##{Inner.BaseAddress:X}", ImGui.ImVec2ImVec2Nil())) {}
        ImGui.EndDisabled();
    }
}

public class ArrayListView(PropertyListView? parent, nint baseAddress, IFArrayProperty value) : PropertyListView(parent)
{
    
    protected readonly nint BaseAddress = baseAddress;
    protected readonly IFArrayProperty Value = value;
    
    private static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Index", _ => 0),
        new("Value", _ => 0),
        new("Remove", _ => 0),
        new("Insert", _ => 0),
    ];
    
    public override void Draw(App owner, UObjectWindow window)
    {
        ImGui.Text($"Data Type is {owner.TypeName.GetPropertyTypeName(Value.Inner)}");
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        var regionAvailable = new Vector2(regionAvail.x, regionAvail.y);
        if (ImGui.BeginTable("##UEToolkitUnitTests", 4, (int)flags, ImGui.ImVec2ImVec2Float(0, 0), 0))
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
                    new ArrayPropertyViewer(Address, Value.Inner, i).Draw(owner, window);
                }
            }
            ImGui.EndTable();
        }
    }
    
    public override string ToString() => $"{GetBaseAddress():X}";

    public override nint GetBaseAddress() => BaseAddress;
}