extern alias imgui;
using System.Numerics;
using imgui::p4rpc.trip2.ImGui;
using UE.Toolkit.Core.Types.Unreal.Factories;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public interface IPropertyStructRowProvider
{
    public void Draw();
}

public class BasePropertyStructRowProvider<TProperty>(TProperty inner, IUnrealClasses unrealClasses) 
    : IPropertyStructRowProvider where TProperty: IFProperty
{
    protected TProperty Property { get; } = inner;
    protected IUnrealClasses IUnrealClasses { get; } = unrealClasses;
    
    public virtual void Draw()
    {
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{IUnrealClasses.GetPropertyTypeName(Property)}");
        // ImGui.Text($"{Property.ClassPrivate.Name}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"0x{Property.Offset_Internal:x}");
        ImGui.TableSetColumnIndex(3);
    }
}

public class BoolPropertyStructRowProvider(IFBoolProperty inner, IUnrealClasses unrealClasses) 
    : BasePropertyStructRowProvider<IFBoolProperty>(inner, unrealClasses)
{
    public override void Draw()
    {
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{IUnrealClasses.GetPropertyTypeName(Property)}");
        // ImGui.Text($"{Property.ClassPrivate.Name}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"0x{Property.Offset_Internal:x}:{Property.FieldMask}");
        ImGui.TableSetColumnIndex(3);
    }
}

public class BytePropertyStructRowProvider(IFByteProperty inner, IUnrealClasses unrealClasses) 
    : BasePropertyStructRowProvider<IFByteProperty>(inner, unrealClasses)
{
    public override void Draw()
    {
        ImGui.TableSetColumnIndex(0);
        var PropType = Property.Enum?.CppType ?? IUnrealClasses.GetPropertyTypeName(Property);
        ImGui.Text($"{PropType}");
        // ImGui.Text($"{Property.ClassPrivate.Name}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"0x{Property.Offset_Internal:x}");
        ImGui.TableSetColumnIndex(3);
    }   
}

public class EnumPropertyStructRowProvider(IFEnumProperty inner, IUnrealClasses unrealClasses) 
    : BasePropertyStructRowProvider<IFEnumProperty>(inner, unrealClasses)
{
    public override void Draw()
    {
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{Property.Enum.CppType}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"0x{Property.Offset_Internal:x}");
        ImGui.TableSetColumnIndex(3);
    }
}

public class StructPropertyViewer(nint baseAddress, IFProperty property) 
    : BasePropertyViewer(baseAddress, property)
{
    public override void Draw(App owner, UObjectWindow window)
    {
        ImGui.TableNextRow(0, 0);
        var Factory = owner.Context.UnrealFactory;
        switch (Inner.Property.ClassPrivate.Name)
        {
            case "BoolProperty":
                new BoolPropertyStructRowProvider( Factory.CreateFBoolProperty(property.Ptr), owner.Context.UnrealClasses).Draw();
                break;
            case "ByteProperty":
                new BytePropertyStructRowProvider(Factory.CreateFByteProperty(property.Ptr), owner.Context.UnrealClasses).Draw();
                break;
            case "EnumProperty":
                new EnumPropertyStructRowProvider(Factory.CreateFEnumProperty(property.Ptr), owner.Context.UnrealClasses).Draw();
                break;
            default:
                new BasePropertyStructRowProvider<IFProperty>(property, owner.Context.UnrealClasses).Draw();
                break;
        }
        Inner.GetViewer(owner, window).Draw();
    }
}

public class StructListViewBase<TUStruct>(PropertyListView? parent, nint baseAddress, TUStruct value) 
    : PropertyListView(parent) where TUStruct : IUStruct
{
    protected readonly nint BaseAddress = baseAddress;
    protected readonly TUStruct Value = value;
    
    protected static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Type", _ => 0),
        new("Name", _ => 0),
        new("Offset", _ => 80),
        new("Value", _ => 400),
    ];

    protected virtual List<IFProperty> GetPropertyList(App owner)
    {
        var PropertyList = Value.PropertyLink.ToList();
        PropertyList.Sort((x, y) =>
        {
            if (x.ClassPrivate.Name != "BoolProperty") return x.Offset_Internal.CompareTo(y.Offset_Internal);
            var bx = owner.Context.UnrealFactory.CreateFBoolProperty(x.Ptr);
            var by = owner.Context.UnrealFactory.CreateFBoolProperty(y.Ptr);
            var OffsetCheck = bx.Offset_Internal.CompareTo(by.Offset_Internal);
            return OffsetCheck == 0 ? bx.FieldMask.CompareTo(by.FieldMask) : OffsetCheck;
        });
        return PropertyList;
    }

    protected Vector2 GetRegionAvailable()
    {
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        return new Vector2(regionAvail.x, regionAvail.y);
    }

    public override void Draw(App owner, UObjectWindow window)
    {
        var PropertyList = GetPropertyList(owner);
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        const ImGuiTableColumnFlags columnFlags = ImGuiTableColumnFlags.WidthFixed;
        var regionAvailable = GetRegionAvailable();
        if (ImGui.BeginTable($"##StructListView{BaseAddress:x}", TABLE_COLUMNS.Length, (int)flags, ImGui.ImVec2ImVec2Nil(), 0))
        {
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column.Name, (int)columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
            ImGui.TableHeadersRow();
            foreach (var Property in PropertyList)
                new StructPropertyViewer(BaseAddress, Property).Draw(owner, window);
            ImGui.EndTable();
        }
    }

    public override string ToString() => $"{GetKey():X}";

    public override PropertyListKey GetKey() => new(BaseAddress, Value.NamePrivate.ToString());
}

public class StructListView(PropertyListView? parent, nint baseAddress, IUStruct value) 
    : StructListViewBase<IUStruct>(parent, baseAddress, value);