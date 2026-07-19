extern alias imgui;
using System.Numerics;
using imgui::p4rpc.trip2.ImGui;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Common.DynamicMap;
using UE.Toolkit.Core.Types.Unreal.Factories;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class DataTablePropertyViewer(nint baseAddress, IFMapProperty property) 
    : IPropertyViewer
{
    private IFMapProperty Property => property;
    private nint BaseAddress => baseAddress;

    private PropertyViewer Key { get; } = new(baseAddress, property.KeyProp);
    private PropertyViewer Value { get; } = new(baseAddress, property.ValueProp);
    
    public void Draw(App owner, UObjectWindow window)
    {
        ImGui.TableNextRow(0, 0);
        ImGui.TableSetColumnIndex(0);
        Key.GetViewerDataTable(owner, window).Draw();
        ImGui.TableSetColumnIndex(1);
        Value.GetViewerDataTable(owner, window).Draw();
        ImGui.TableSetColumnIndex(2);
        ImGui.BeginDisabled(true);
        if (ImGui.Button($"Remove##{BaseAddress:X}", ImGui.ImVec2ImVec2Nil())) {}
        ImGui.EndDisabled();
    }
}

public class DataTableListView : PropertyListView
{
    private readonly nint BaseAddress;
    private readonly IUStruct Value;

    private readonly IFProperty? RowStruct;
    private IFMapProperty? Map;
    private bool? CreatedMapProperty;

    public DataTableListView(PropertyListView? parent, nint baseAddress, IUStruct value) : base(parent)
    {
        BaseAddress = baseAddress;
        Value = value;
        RowStruct = Value.PropertyLink.FirstOrDefault(x => x.NamePrivate == "RowStruct");
    }

    private static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Key", _ => 0),
        new("Value", _ => 0),
        new("Actions", _ => 0),
    ];

    private unsafe IUScriptStruct GetRowStruct(IUnrealFactory Factory) => 
        Factory.CreateUScriptStruct(*(nint*)(BaseAddress + RowStruct!.Offset_Internal));

    private bool CreateMapProperty(string RowName, IUnrealClasses Classes)
    {
        // DataTable will always be a TMap<FName, FDataTableRow>
        if (!Classes.AddNameProperty("Key", 0, out var KeyProp)) return false;
        if (!Classes.AddStructProperty_DataTableSpecial("Value", RowName, 8, out var ValueProp)) return false;
        return Classes.AddMapProperty(
            "Map", RowStruct.Offset_Internal + 8,
            KeyProp, ValueProp, out Map);
    }
    
    public override void Draw(App owner, UObjectWindow window)
    {
        var Factory = owner.Context.UnrealFactory;
        var Classes = owner.Context.UnrealClasses;
        var Memory = owner.Context.UnrealMemory;
        if (RowStruct == null)
        {
            ImGui.Text("ERROR: RowStruct could not be found in the DataTable. Can not display the contents.");
            return;
        }
        var StructName = GetRowStruct(Factory).NamePrivate.ToString();
        CreatedMapProperty ??= CreateMapProperty(StructName, Classes);
        if (!CreatedMapProperty.Value)
        {
            ImGui.Text($"ERROR: Could not find the struct {StructName}. Can not display the contents.");
            return;
        }
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        var regionAvailable = new Vector2(regionAvail.x, regionAvail.y);
        if (MapKeyFactory.CreateMapKey(Map, Factory, out var MapKey))
        {
            if (ImGui.BeginTable($"##DataTableListView{BaseAddress:x}", 3, (int)flags, ImGui.ImVec2ImVec2Nil(), 0))
            {
                var columnFlags = 0;
                foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                    ImGui.TableSetupColumn(Column.Name, columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
                ImGui.TableHeadersRow();
                var Dict = new TMapDynamicDictionary(BaseAddress + Map.Offset_Internal, MapKey,
                    new DynamicMapValueUnrealProperty(Map.ValueProp), Memory);
                foreach (var Entry in Dict.Keys)
                {
                    if (Dict.TryGetValue(Entry, out var entryAddress))
                    {
                        new DataTablePropertyViewer(entryAddress - MapKey.DynSizeOf(), Map).Draw(owner, window);   
                    }
                }
                ImGui.EndTable();       
            }
        }
        else
        {
            ImGui.Text($"Maps with the key type {Map.KeyProp.ClassPrivate.Name} are not currently supported");
        }
    }
    
    public override string ToString() => $"{GetKey():X}";

    public override PropertyListKey GetKey() => new(BaseAddress, Value.NamePrivate.ToString());
}