extern alias imgui;
using System.Diagnostics.CodeAnalysis;
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

public static class MapKeyFactory
{
    // From UE Toolkit
    internal static bool CreateMapKey(
        IFMapProperty property, IUnrealFactory factory, [NotNullWhen(true)] out IDynamicMapKeyType? MapKey)
    {
        var Key = property.KeyProp;
        MapKey = Key.ClassPrivate.Name switch
        {
            "Int8Property" => new Int8DynamicMapKeyType(property, factory),
            "Int16Property" or "UInt16Property" => new Int16DynamicMapKeyType(property, factory),
            "IntProperty" or "UInt32Property" => new IntDynamicMapKeyType(property, factory),
            "Int64Property" or "UInt64Property" => new Int64DynamicMapKeyType(property, factory),
            "NameProperty" => new NameDynamicMapKeyType(property, factory),
            // Can't use these since they're defined in Toolkit.Reloaded
            /*
            "StrProperty" => new StringDynamicMapKeyType(property, factory.Factory, factory.Objects, factory.Memory),
            "StructProperty" => StructDynamicMapKeyType.Create(
                property, factory.Factory.CreateFStructProperty(Key.Ptr), 
                factory.Factory, factory.Objects, factory.Memory),
            */
            _ => null
        };
        return MapKey != null;
    }
}

public class MapPropertyViewer(nint baseAddress, IFMapProperty property) 
    // : BasePropertyViewer(baseAddress, property)
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
        Key.GetViewer(owner, window).Draw();
        ImGui.TableSetColumnIndex(1);
        Value.GetViewer(owner, window).Draw();
        ImGui.TableSetColumnIndex(2);
        ImGui.BeginDisabled(true);
        if (ImGui.Button($"Remove##{BaseAddress:X}", ImGui.ImVec2ImVec2Nil())) {}
        ImGui.EndDisabled();
    }
}

public class MapListView(PropertyListView? parent, nint baseAddress, IFMapProperty value, 
    IUnrealMemory memory, IUnrealFactory factory, IUnrealClasses classes) : PropertyListView(parent)
{

    protected readonly nint BaseAddress = baseAddress;
    protected readonly IFMapProperty Value = value;
    protected IUnrealMemory Memory => memory;
    protected IUnrealFactory Factory => factory;
    protected IUnrealClasses Classes => classes;
    
    private static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Key", _ => 0),
        new("Value", _ => 0),
        new("Remove", _ => 0)
    ];
    
    public override void Draw(App owner, UObjectWindow window)
    {
        var KeyType = owner.Context.UnrealClasses.GetPropertyTypeName(Value.KeyProp);
        var ValueType = owner.Context.UnrealClasses.GetPropertyTypeName(Value.ValueProp);
        ImGui.Text($"Key is {KeyType}, Value is {ValueType}");
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        var regionAvailable = new Vector2(regionAvail.x, regionAvail.y);
        if (MapKeyFactory.CreateMapKey(Value, factory, out var MapKey))
        {
            if (ImGui.BeginTable($"##MapListView{BaseAddress:x}", TABLE_COLUMNS.Length, (int)flags, ImGui.ImVec2ImVec2Nil(), 0))
            {
                // var columnFlags = (int)ImGuiTableColumnFlags.WidthFixed;
                var columnFlags = 0;
                foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                    ImGui.TableSetupColumn(Column.Name, columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
                ImGui.TableHeadersRow();
                var Dict = new TMapDynamicDictionary(BaseAddress, MapKey, 
                    new DynamicMapValueUnrealProperty(Value.ValueProp), Memory);
                foreach (var Entry in Dict.Keys)
                {
                    if (Dict.TryGetValue(Entry, out var entryAddress))
                    {
                        new MapPropertyViewer(entryAddress - MapKey.DynSizeOf(), Value).Draw(owner, window);   
                    }
                }
                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.Text($"Maps with the key type {Value.KeyProp.ClassPrivate.Name} are not currently supported");   
        }
    }
    
    public override string ToString() => $"{GetKey():X}";

    public override PropertyListKey GetKey() => new(BaseAddress, Classes.GetPropertyTypeName(Value));
}