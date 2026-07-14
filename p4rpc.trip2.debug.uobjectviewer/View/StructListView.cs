extern alias imgui;
using System.Numerics;
using imgui::p4rpc.trip2.ImGui;
using UE.Toolkit.Core.Types.Unreal.Factories;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class BasePropertyStructRowProvider<TProperty>(TProperty inner, TypeName typeName) 
    : IPropertyStructRowProvider where TProperty: IFProperty
{
    protected TProperty Property { get; } = inner;
    protected TypeName TypeName { get; } = typeName;
    
    public virtual void Draw()
    {
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{TypeName.GetPropertyTypeName(Property)}");
        // ImGui.Text($"{Property.ClassPrivate.Name}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"0x{Property.Offset_Internal:x}");
        ImGui.TableSetColumnIndex(3);
    }
}

public class BoolPropertyStructRowProvider(IFBoolProperty inner, TypeName typeName) 
    : BasePropertyStructRowProvider<IFBoolProperty>(inner, typeName)
{
    public override void Draw()
    {
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{TypeName.GetPropertyTypeName(Property)}");
        // ImGui.Text($"{Property.ClassPrivate.Name}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"0x{Property.Offset_Internal:x}:{Property.FieldMask}");
        ImGui.TableSetColumnIndex(3);
    }
}

public class BytePropertyStructRowProvider(IFByteProperty inner, TypeName typeName) 
    : BasePropertyStructRowProvider<IFByteProperty>(inner, typeName)
{
    public override void Draw()
    {
        ImGui.TableSetColumnIndex(0);
        var PropType = Property.Enum?.CppType ?? TypeName.GetPropertyTypeName(Property);
        ImGui.Text($"{PropType}");
        // ImGui.Text($"{Property.ClassPrivate.Name}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"0x{Property.Offset_Internal:x}");
        ImGui.TableSetColumnIndex(3);
    }   
}

public class EnumPropertyStructRowProvider(IFEnumProperty inner, TypeName typeName) 
    : BasePropertyStructRowProvider<IFEnumProperty>(inner, typeName)
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
        switch (Property.ClassPrivate.Name)
        {
            case "BoolProperty":
                new BoolPropertyStructRowProvider( Factory.CreateFBoolProperty(property.Ptr), owner.TypeName).Draw();
                break;
            case "ByteProperty":
                new BytePropertyStructRowProvider(Factory.CreateFByteProperty(property.Ptr), owner.TypeName).Draw();
                break;
            case "EnumProperty":
                new EnumPropertyStructRowProvider(Factory.CreateFEnumProperty(property.Ptr), owner.TypeName).Draw();
                break;
            default:
                new BasePropertyStructRowProvider<IFProperty>(property, owner.TypeName).Draw();
                break;
        }
        GetViewer(owner, window).Draw();
    }
}

public class StructListView(PropertyListView? parent, nint baseAddress, IUStruct value) : PropertyListView(parent)
{
    protected readonly nint BaseAddress = baseAddress;
    protected readonly IUStruct Value = value;
    
    private static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Type", _ => 0),
        new("Name", _ => 0),
        new("Offset", _ => 80),
        new("Value", _ => 400),
        // new("Offset", w => w.X / 10),
        // new("Value", w => w.X / 2),
    ];

    public override void Draw(App owner, UObjectWindow window)
    {
        var Factory = owner.Context.UnrealFactory;
        var PropertyList = Value.PropertyLink.ToList();
        PropertyList.Sort((x, y) =>
        {
            if (x.ClassPrivate.Name != "BoolProperty") return x.Offset_Internal.CompareTo(y.Offset_Internal);
            var bx = owner.Context.UnrealFactory.CreateFBoolProperty(x.Ptr);
            var by = owner.Context.UnrealFactory.CreateFBoolProperty(y.Ptr);
            var OffsetCheck = bx.Offset_Internal.CompareTo(by.Offset_Internal);
            return OffsetCheck == 0 ? bx.FieldMask.CompareTo(by.FieldMask) : OffsetCheck;
        });
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        var regionAvailable = new Vector2(regionAvail.x, regionAvail.y);
        if (ImGui.BeginTable("##UEToolkitUnitTests", 4, (int)flags, ImGui.ImVec2ImVec2Float(0, 0), 0))
        {
            var columnFlags = (int)ImGuiTableColumnFlags.WidthFixed;
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column.Name, columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
            ImGui.TableHeadersRow();
            foreach (var Property in PropertyList)
                new StructPropertyViewer(BaseAddress, Property).Draw(owner, window);
            ImGui.EndTable();
        }
    }

    public override string ToString() => $"{GetBaseAddress():X}";

    public override nint GetBaseAddress() => BaseAddress;
}