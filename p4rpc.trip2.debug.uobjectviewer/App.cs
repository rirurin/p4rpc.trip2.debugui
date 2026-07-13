extern alias imgui;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using System.Numerics;
using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer;

public class App : GUIApp
{
    public override string Name => "UObject Viewer";

    internal Context Context { get; }

    internal readonly Dictionary<nint, IUObject> ListOfObjects;
    
    internal ObjectSearch ObjectSearch { get; }
    
    internal TypeName TypeName { get; }

    internal bool InitialLoad = false;

    public override void Tick(float DeltaTime)
    {
        
    }

    public App(Context context) : base(context.GUIState)
    {
        Context = context;
        TypeName = new(Context.UnrealFactory);
        ListOfObjects = [];
        ObjectSearch = new(new(this));
        Context.UnrealObjects.OnObjectLoaded += uobject =>
        {
            unsafe
            {
                var Instance = Context.UnrealFactory.CreateUObject((nint)uobject.Self);
                if (ObjectSearch.SearchMatches(Instance.NamePrivate.ToString()))
                    ListOfObjects[Instance.Ptr] = Instance;
            }
        };
        Context.UnrealObjects.OnObjectBeginDestroy += uobject =>
        {
            unsafe
            {
                var Instance = (nint)uobject.Self;
                if (!ListOfObjects.Remove(Instance))
                {
                    // Log.Error($"UObject::BeginDestroy was called on object at 0x{Instance:x} but is not in the UObject registry!");
                }
            }
        };

        var UObjectArrayWindow = () =>
        {
            if (Windows.Count == 0)
                Windows.Add(new GUObjectArrayWindow(this));
        };
        UObjectArrayWindow();
        Buttons.Add("Object Array", UObjectArrayWindow);
    }
}

public class GUObjectArrayWindow(App owner) : GUIWindow<App>(owner)
{
    public override string Title => "All Loaded UObjects";

    private static string[] TABLE_COLUMNS = ["Type", "Name", "Address"];

    public override Vector2 StartSize
    {
        get
        {
            if (!Owner.TryGetTarget(out var App)) return Vector2.Zero; 
            var SurfaceSize = App.State!.GetSurfaceSize();
            return new Vector2(SurfaceSize.X / 2, SurfaceSize.Y * 3 / 4);
        }
    }
    
    public override Vector2 StartPos
    {
        get
        {
            if (!Owner.TryGetTarget(out var App)) return Vector2.Zero;
            var SurfaceSize = App.State!.GetSurfaceSize();
            return new Vector2(15, 30);
        }
    }

    public override void Draw(App owner)
    {
        if (!owner.InitialLoad)
        {
            owner.ObjectSearch.OnSearchClear();
            owner.InitialLoad = true;
        }
        ImGui.Text($"{owner.ListOfObjects.Count} objects (GUObjectArray has {owner.Context.UnrealObjects.GUObjectArray.NumElements} elements)");
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        owner.ObjectSearch.DrawPanel();
        if (ImGui.BeginTable("##UObject List", 3, (int)flags, ImGui.ImVec2ImVec2Float(0, 0), 0))
        {
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column, 0, 0, (uint)Index);
            ImGui.TableHeadersRow();
            var Entries = owner.ListOfObjects.ToList();
            unsafe
            {
                var clipper = new ImGuiListClipper.__Internal();
                ImGui.__Internal.ImGuiListClipperBegin((nint)(&clipper), Entries.Count, 0);
                while (ImGui.__Internal.ImGuiListClipperStep((nint)(&clipper)))
                {
                    for (var k = clipper.DisplayStart; k < clipper.DisplayEnd; k++)
                    {
                        var Entry = Entries[k];
                        ImGui.TableNextRow(0, 0);
                        ImGui.TableSetColumnIndex(0);
                        if (ImGui.SelectableBool($"{Entry.Value.NamePrivate}", false,
                                (int)ImGuiSelectableFlags.SpanAllColumns, ImGui.ImVec2ImVec2Nil()))
                            owner.Windows.Add(new UObjectWindow(Entry.Value, owner));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text($"{Entry.Value.ClassPrivate.NamePrivate}");
                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text($"0x{Entry.Value.Ptr:X}");
                    }
                }
                ImGui.__Internal.ImGuiListClipperEnd((nint)(&clipper));
            }
            ImGui.EndTable();
        }
    }
}

public abstract class BasePropertyViewer<TProperty>(IUObject uobject, TProperty property, TypeName typeName)
where TProperty: IFProperty
{
    protected IUObject UObject { get; } = uobject;
    protected TProperty Property { get; } = property;
    protected TypeName TypeName { get; } = typeName;

    protected void DrawTypeAndName()
    {
        ImGui.TableSetColumnIndex(0);
        ImGui.Text($"{TypeName.GetPropertyTypeName(Property)}");
        // ImGui.Text($"{Property.ClassPrivate.Name}");
        ImGui.TableSetColumnIndex(1);
        ImGui.Text($"{Property.NamePrivate}");
        ImGui.TableSetColumnIndex(2);
    }

    public virtual void Draw()
    {
        DrawTypeAndName();
        ImGui.Text($"0x{Property.Offset_Internal:x}");
        ImGui.TableSetColumnIndex(3);
    }
}

public class UntypedPropertyViewer(IUObject uobject, IFProperty property, TypeName typeName)
    : BasePropertyViewer<IFProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        base.Draw();
        var Pointer = UObject.Ptr + Property.Offset_Internal;
        var Bytes = new string[Property.ElementSize];
        for (var i = 0; i < Bytes.Length; i++)
        {
            unsafe
            {
                Bytes[i] = $"{*(byte*)(Pointer + i):X2}";
            }
        }
        ImGui.Text(string.Join(" ", Bytes));
    }
}

public class BoolPropertyViewer(IUObject uobject, IFBoolProperty property, TypeName typeName) 
    : BasePropertyViewer<IFBoolProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        DrawTypeAndName();
        ImGui.Text($"0x{Property.Offset_Internal:x}:{Property.FieldMask}");
        ImGui.TableSetColumnIndex(3);
        unsafe
        {
            var Value = (*(byte*)(UObject.Ptr + Property.Offset_Internal) & Property.FieldMask) != 0;
            if (ImGui.Checkbox($"##Property_{Property.Ptr:x}", ref Value))
            {
                if (Value) *(byte*)(UObject.Ptr + Property.Offset_Internal) |= Property.FieldMask;
                else *(byte*)(UObject.Ptr + Property.Offset_Internal) &= (byte)~Property.FieldMask;
            }
        }
    }
}

public class BytePropertyViewer(IUObject uobject, IFByteProperty property, TypeName typeName) 
    : BasePropertyViewer<IFByteProperty>(uobject, property, typeName)
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
        unsafe
        {
            if (Property.Enum != null)
            {
                var Value = *(byte*)(UObject.Ptr + Property.Offset_Internal);
                Dictionary<byte, string> NamesDict = new();
                for (var i = 0; i < Property.Enum.Names.ArrayNum; i++)
                {
                    var CurrentName = &Property.Enum.Names.AllocatorInstance[i];
                    NamesDict.Add((byte)CurrentName->Value, CurrentName->Key.ToString());
                }
                var FieldAddress = UObject.Ptr + Property.Offset_Internal;
                if (ImGui.BeginCombo(
                        $"##0x{FieldAddress:x}", 
                        NamesDict.TryGetValue(Value, out var Name) ? Name : $"{Value}", 
                        0))
                {
                    for (var i = 0; i < Property.Enum.Names.ArrayNum; i++)
                    {
                        var CurrentName = &Property.Enum.Names.AllocatorInstance[i];
                        var NameKey = CurrentName->Key.ToString();
                        if (NameKey.EndsWith("_MAX")) continue;
                        if (ImGui.SelectableBool(
                                NameKey, CurrentName->Value == Value, 
                                0, ImGui.ImVec2ImVec2Nil()))
                        {
                            *(byte*)(UObject.Ptr + Property.Offset_Internal) = (byte)CurrentName->Value;
                        }
                        if (CurrentName->Value == Value)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }
            else
            {
                var Value = (int)*(byte*)(UObject.Ptr + Property.Offset_Internal);
                if (ImGui.InputInt(
                        $"##Property_{Property.Ptr:x}",
                        ref Value,
                        1, 1, 0))
                    *(byte*)(UObject.Ptr + Property.Offset_Internal) = (byte)Value;   
            }
        }
    }
}

public class Int8PropertyViewer(IUObject uobject, IFProperty property, TypeName typeName)
    : BasePropertyViewer<IFProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        base.Draw();
        unsafe
        {
            var Value = (int)*(byte*)(UObject.Ptr + Property.Offset_Internal);
            if (ImGui.InputInt(
                    $"##Property_{Property.Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(byte*)(UObject.Ptr + Property.Offset_Internal) = (byte)Value;
        }
    }
}

public class Int16PropertyViewer(IUObject uobject, IFProperty property, TypeName typeName)
    : BasePropertyViewer<IFProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        base.Draw();
        unsafe
        {
            var Value = (int)*(short*)(UObject.Ptr + Property.Offset_Internal);
            if (ImGui.InputInt(
                    $"##Property_{Property.Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(short*)(UObject.Ptr + Property.Offset_Internal) = (short)Value;
        }
    }
}

public class IntPropertyViewer(IUObject uobject, IFProperty property, TypeName typeName)
    : BasePropertyViewer<IFProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        base.Draw();
        unsafe
        {
            ImGui.InputInt(
                $"##Property_{Property.Ptr:x}", 
                ref *(int*)(UObject.Ptr + Property.Offset_Internal), 
                1, 1, 0);
        }
    }
}

public class UInt16PropertyViewer(IUObject uobject, IFProperty property, TypeName typeName)
    : BasePropertyViewer<IFProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        base.Draw();
        unsafe
        {
            var Value = (int)*(ushort*)(UObject.Ptr + Property.Offset_Internal);
            if (ImGui.InputInt(
                    $"##Property_{Property.Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(ushort*)(UObject.Ptr + Property.Offset_Internal) = (ushort)Value;
        }
    }
}

public class FloatPropertyViewer(IUObject uobject, IFProperty property, TypeName typeName)
    : BasePropertyViewer<IFProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        base.Draw();
        unsafe
        {
            ImGui.InputFloat(
                $"##Property_{Property.Ptr:x}", 
                ref *(float*)(UObject.Ptr + Property.Offset_Internal), 
                1, 1, "%f", 0);
        }
    }
}

public class DoublePropertyViewer(IUObject uobject, IFProperty property, TypeName typeName)
    : BasePropertyViewer<IFProperty>(uobject, property, typeName)
{
    public override void Draw()
    {
        base.Draw();
        unsafe
        {
            ImGui.InputDouble(
                $"##Property_{Property.Ptr:x}", 
                ref *(double*)(UObject.Ptr + Property.Offset_Internal), 
                1, 1, "%f", 0);
        }
    }
}

public class EnumPropertyViewer(IUObject uobject, IFEnumProperty property, TypeName typeName) 
    : BasePropertyViewer<IFEnumProperty>(uobject, property, typeName)
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
        
        unsafe
        {
            if (Property.Enum != null)
            {
                var Value = Property.ElementSize switch
                {
                    1 => *(byte*)(UObject.Ptr + Property.Offset_Internal),
                    2 => *(short*)(UObject.Ptr + Property.Offset_Internal),
                    4 => *(int*)(UObject.Ptr + Property.Offset_Internal),
                    _ => *(long*)(UObject.Ptr + Property.Offset_Internal),
                };
                Dictionary<long, string> NamesDict = new();
                for (var i = 0; i < Property.Enum.Names.ArrayNum; i++)
                {
                    var CurrentName = &Property.Enum.Names.AllocatorInstance[i];
                    NamesDict.Add(CurrentName->Value, CurrentName->Key.ToString());
                }
                var FieldAddress = UObject.Ptr + Property.Offset_Internal;
                if (ImGui.BeginCombo(
                        $"##0x{FieldAddress:x}", 
                        NamesDict.TryGetValue(Value, out var Name) ? Name : $"{Value}", 
                        0))
                {
                    for (var i = 0; i < Property.Enum.Names.ArrayNum; i++)
                    {
                        var CurrentName = &Property.Enum.Names.AllocatorInstance[i];
                        var NameKey = CurrentName->Key.ToString();
                        if (NameKey.EndsWith("_MAX")) continue;
                        if (ImGui.SelectableBool(
                                NameKey, CurrentName->Value == Value, 
                                0, ImGui.ImVec2ImVec2Nil()))
                        {
                            switch (Property.ElementSize)
                            {
                                case 1:
                                    *(byte*)(UObject.Ptr + Property.Offset_Internal) = (byte)Value;
                                    break;
                                case 2:
                                    *(short*)(UObject.Ptr + Property.Offset_Internal) = (short)Value;
                                    break;
                                case 4:
                                    *(int*)(UObject.Ptr + Property.Offset_Internal) = (int)Value;
                                    break;
                                default:
                                    *(long*)(UObject.Ptr + Property.Offset_Internal) = (long)Value;
                                    break;
                            }
                        }
                        if (CurrentName->Value == Value)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }
            else
            {
                var Value = (int)*(byte*)(UObject.Ptr + Property.Offset_Internal);
                if (ImGui.InputInt(
                        $"##Property_{Property.Ptr:x}",
                        ref Value,
                        1, 1, 0))
                    *(byte*)(UObject.Ptr + Property.Offset_Internal) = (byte)Value;   
            }
        }
    }
}

public class UObjectWindowColumn(string name, Func<Vector2, float> getWidth)
{
    public string Name { get; } = name;
    public Func<Vector2, float> GetWidth { get; } = getWidth;
}

public class UObjectWindow : GUIWindow<App>
{
    private IUObject Object;
    public override string Title { get; }

    private static UObjectWindowColumn[] TABLE_COLUMNS =
    [
        new("Type", _ => 0),
        new("Name", _ => 0),
        new("Offset", _ => 80),
        new("Value", _ => 400),
        // new("Offset", w => w.X / 10),
        // new("Value", w => w.X / 2),
    ];
    // private static string[] TABLE_COLUMNS = ["Type", "Name", "Offset", "Value"];
    // private static Func<float> GET_COLUMN_WIDTH

    public override void Draw(App owner)
    {
        if (!owner.ListOfObjects.ContainsKey(Object.Ptr))
        {
            Close();
            return;
        }

        var Factory = owner.Context.UnrealFactory;
        var PropertyList = Object.ClassPrivate.PropertyLink.ToList();
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
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column.Name, (int)ImGuiTableColumnFlags.WidthFixed, Column.GetWidth(regionAvailable), (uint)Index);
            ImGui.TableHeadersRow();
            foreach (var Property in PropertyList)
            {
                ImGui.TableNextRow(0, 0);
                
                switch (Property.ClassPrivate.Name)
                {
                    case "BoolProperty":
                        new BoolPropertyViewer(Object, Factory.CreateFBoolProperty(Property.Ptr), owner.TypeName).Draw();
                        break;
                    case "ByteProperty":
                        new BytePropertyViewer(Object, Factory.CreateFByteProperty(Property.Ptr), owner.TypeName).Draw();
                        break;
                    case "Int8Property":
                        new Int8PropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "Int16Property":
                        new Int16PropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "UInt16Property":
                        new UInt16PropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "IntProperty":
                        new IntPropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "UInt32Property":
                        new UntypedPropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "Int64Property":
                        new UntypedPropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "UInt64Property":
                        new UntypedPropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "FloatProperty":
                        new FloatPropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "DoubleProperty":
                        new DoublePropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                    case "EnumProperty":
                        new EnumPropertyViewer(Object, Factory.CreateFEnumProperty(Property.Ptr), owner.TypeName).Draw();
                        break;
                    default:
                        new UntypedPropertyViewer(Object, Property, owner.TypeName).Draw();
                        break;
                }
            }
            ImGui.EndTable();
        }
    }

    public UObjectWindow(IUObject uobject, App owner) : base(owner)
    {
        Object = uobject;
        var Class = Object.ClassPrivate;
        Title = $"{Object.NamePrivate} @ 0x{Object.Ptr:x}";
    }
}