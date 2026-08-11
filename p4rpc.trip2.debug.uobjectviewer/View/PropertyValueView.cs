extern alias imgui;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Factories;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public interface IPropertyValueViewer
{
    public nint BaseAddress { get; }
    
    public IFProperty Property { get; }
    
    public IUnrealClasses UnrealClasses { get; }
    
    public void Draw();
}

public abstract class BasePropertyValueViewer<TProperty>(nint baseAddress, TProperty propertyTyped, 
    IUnrealClasses unrealClasses) : IPropertyValueViewer where TProperty: IFProperty
{
    // start impl IPropertyValueViewer
    public nint BaseAddress => baseAddress;
    public IFProperty Property => PropertyTyped;
    public IUnrealClasses UnrealClasses => unrealClasses;
    public abstract void Draw();
    // end impl IPropertyValueViewer
    
    internal TProperty PropertyTyped => propertyTyped;
}

public class UntypedPropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        var Pointer = BaseAddress + PropertyTyped.Offset_Internal;
        var Bytes = new string[PropertyTyped.ElementSize];
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

public class BoolPropertyValueViewer(nint baseAddress, IFBoolProperty propertyTyped, IUnrealClasses unrealClasses) 
    : BasePropertyValueViewer<IFBoolProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        unsafe
        {
            var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
            var Value = (*(byte*)Ptr & PropertyTyped.FieldMask) != 0;
            if (ImGui.Checkbox($"##Property_{Ptr:x}_{PropertyTyped.FieldMask:x}", ref Value))
            {
                if (Value) *(byte*)Ptr |= PropertyTyped.FieldMask;
                else *(byte*)Ptr &= (byte)~PropertyTyped.FieldMask;
            }
        }
    }
}

public class BytePropertyValueViewer(nint baseAddress, IFByteProperty propertyTyped, IUnrealClasses unrealClasses) 
    : BasePropertyValueViewer<IFByteProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        unsafe
        {
            if (PropertyTyped.Enum != null)
            {
                var Value = *(byte*)(BaseAddress + PropertyTyped.Offset_Internal);
                Dictionary<byte, string> NamesDict = new();
                for (var i = 0; i < PropertyTyped.Enum.Names.ArrayNum; i++)
                {
                    var CurrentName = &PropertyTyped.Enum.Names.AllocatorInstance[i];
                    NamesDict.Add((byte)CurrentName->Value, CurrentName->Key.ToString());
                }
                var FieldAddress = BaseAddress + PropertyTyped.Offset_Internal;
                if (ImGui.BeginCombo(
                        $"##0x{FieldAddress:x}", 
                        NamesDict.TryGetValue(Value, out var Name) ? Name : $"{Value}", 
                        0))
                {
                    for (var i = 0; i < PropertyTyped.Enum.Names.ArrayNum; i++)
                    {
                        var CurrentName = &PropertyTyped.Enum.Names.AllocatorInstance[i];
                        var NameKey = CurrentName->Key.ToString();
                        if (NameKey.EndsWith("_MAX")) continue;
                        if (ImGui.SelectableBool(
                                NameKey, CurrentName->Value == Value, 
                                0, ImGui.ImVec2ImVec2Nil()))
                        {
                            *(byte*)(BaseAddress + PropertyTyped.Offset_Internal) = (byte)CurrentName->Value;
                        }
                        if (CurrentName->Value == Value)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }
            else
            {
                var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
                var Value = (int)*(byte*)Ptr;
                if (ImGui.InputInt(
                        $"##Property_{Ptr:x}",
                        ref Value,
                        1, 1, 0))
                    *(byte*)Ptr = (byte)Value;   
            }
        }
    }
}

public class Int8PropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        unsafe
        {
            var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
            var Value = (int)*(byte*)Ptr;
            if (ImGui.InputInt(
                    $"##Property_{Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(byte*)Ptr = (byte)Value;
        }
    }
}

public class Int16PropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        
        unsafe
        {
            var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
            var Value = (int)*(short*)Ptr;
            if (ImGui.InputInt(
                    $"##Property_{Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(short*)Ptr = (short)Value;
        }
    }
}

public class IntPropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        
        unsafe
        {
            var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
            ImGui.InputInt(
                $"##Property_{Ptr:x}", 
                ref *(int*)Ptr,
                1, 1, 0);
        }
    }
}

public class UInt16PropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        
        unsafe
        {
            var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
            var Value = (int)*(ushort*)Ptr;
            if (ImGui.InputInt(
                    $"##Property_{Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(ushort*)Ptr = (ushort)Value;
        }
    }
}

public class FloatPropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        
        unsafe
        {
            var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
            ImGui.InputFloat(
                $"##Property_{Ptr:x}", 
                ref *(float*)Ptr,
                1, 1, "%f", 0);
        }
    }
}

public class DoublePropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        unsafe
        {
            var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
            ImGui.InputDouble(
                $"##Property_{Ptr:x}", 
                ref *(double*)Ptr,
                1, 1, "%f", 0);
        }
    }
}

public class NamePropertyValueViewer : BasePropertyValueViewer<IFProperty>
{
    protected ResizableTextInput TextInput;

    public NamePropertyValueViewer(nint baseAddress, IFProperty propertyTyped, IUnrealClasses unrealClasses) 
        : base(baseAddress, propertyTyped, unrealClasses)
    {
        var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
        TextInput = new($"##Name @ 0x{Ptr:X}");
        unsafe { TextInput.ReplaceBuffer(((FName*)Ptr)->ToString() + '\0'); }
    }

    public override void Draw()
    {
        if (TextInput.Draw(ImGuiInputTextFlags.EnterReturnsTrue))
        {
            unsafe { *(FName*)(BaseAddress + PropertyTyped.Offset_Internal) = 
                new(Marshal.PtrToStringAnsi((nint)TextInput.GetBuffer()) + '\0'); }
        }
    }
}

public class StringPropertyValueViewer : BasePropertyValueViewer<IFProperty>
{
    protected ResizableTextInput TextInput;
    private IUnrealStrings UnrealStrings;
    private IUnrealMemory UnrealMemory;

    public StringPropertyValueViewer(nint baseAddress, IUnrealStrings unrealStrings, IUnrealMemory unrealMemory, 
        IFProperty propertyTyped, IUnrealClasses unrealClasses) : base(baseAddress, propertyTyped, unrealClasses)
    {
        var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
        TextInput = new($"##String @ 0x{Ptr:X}");
        UnrealStrings = unrealStrings;
        UnrealMemory = unrealMemory;
        unsafe
        {
            var strValue = (FString*)Ptr;
            if (strValue->Data.ArrayNum > 0)
                TextInput.ReplaceBuffer(strValue->ToString() + '\0');
        }
    }

    public override void Draw()
    {
        if (TextInput.Draw(ImGuiInputTextFlags.EnterReturnsTrue))
        {
            unsafe
            {
                var CurrentString = (FString*)(BaseAddress + PropertyTyped.Offset_Internal);
                if (CurrentString->Data.AllocatorInstance != null)
                    UnrealMemory.Free((nint)CurrentString->Data.AllocatorInstance);
                // CurrentString->Data.AllocatorInstance
                *(FString*)(BaseAddress + PropertyTyped.Offset_Internal) = 
                    *UnrealStrings.CreateFString(Marshal.PtrToStringAnsi((nint)TextInput.GetBuffer()) + '\0');
            }
        }
    }
}

public class TextPropertyValueViewer : BasePropertyValueViewer<IFProperty>
{
    protected ResizableTextInput TextInput;
    private IUnrealStrings UnrealStrings;
    private IUnrealMemory UnrealMemory;
    private IUnrealObjects UnrealObjects;

    public TextPropertyValueViewer(IntPtr baseAddress, IUnrealStrings unrealStrings, IUnrealMemory unrealMemory,
        IFProperty property, IUnrealClasses unrealClasses, IUnrealObjects unrealObjects) : base(baseAddress, property, unrealClasses)
    {
        var Ptr = BaseAddress + PropertyTyped.Offset_Internal;
        TextInput = new($"##Text @ 0x{Ptr:X}");
        UnrealStrings = unrealStrings;
        UnrealMemory = unrealMemory;
        UnrealObjects = unrealObjects;
        unsafe
        {
            var textValue = (FText*)Ptr;
            TextInput.ReplaceBuffer(UnrealStrings.FTextToString(textValue) + '\0');
        }
    }

    public override void Draw()
    {
        if (TextInput.Draw(ImGuiInputTextFlags.EnterReturnsTrue))
        {
            unsafe
            {
                // TODO: Deallocate previous FText (THIS CURRENTLY LEAKS MEMORY)
                *(FText*)(BaseAddress + Property.Offset_Internal) =
                    *UnrealObjects.CreateFText(Marshal.PtrToStringAnsi((nint)TextInput.GetBuffer()) + '\0');
            }
        }
    }
}

public class StructPropertyValueViewer(nint baseAddress, IFStructProperty propertyTyped, IUnrealClasses unrealClasses,
    UObjectWindow window) : BasePropertyValueViewer<IFStructProperty>(baseAddress, propertyTyped, unrealClasses)
{
    protected UObjectWindow Window => window;
    
    public override void Draw()
    {
        var Address = BaseAddress + PropertyTyped.Offset_Internal;
        if (ImGui.Button(
                $"View Struct##{Address:X}", 
                ImGui.ImVec2ImVec2Nil()))
        {
            Window.AddView(
                new(Address, Window.UnrealClasses!.GetPropertyTypeName(PropertyTyped)), 
                new StructListView(Window.GetCurrentView(), Address, PropertyTyped.Struct));
        }
        ImGui.SameLine(0, 10);
        ImGui.Text($"0x{Address:X}");
    }
}

public class DataTableRowPropertyValueViewer(nint baseAddress, IUnrealFactory factory, 
    IFObjectProperty propertyTyped, IUnrealClasses unrealClasses, UObjectWindow window)
    : BasePropertyValueViewer<IFObjectProperty>(baseAddress, propertyTyped, unrealClasses)
{
    private IUnrealFactory Factory = factory;
    protected UObjectWindow Window => window;
    
    public override void Draw()
    {
        unsafe
        {
            var Pointer = *(nint*)(BaseAddress + PropertyTyped.Offset_Internal);
            if (Pointer == nint.Zero)
            {
                ImGui.Text("NULL");
                return;
            }
            if (ImGui.Button(
                    $"View Row##{BaseAddress + PropertyTyped.Offset_Internal:X}", 
                    ImGui.ImVec2ImVec2Nil()))
            {
                Window.AddView(
                    new(Pointer, Window.UnrealClasses!.GetPropertyTypeName(PropertyTyped)), 
                    new StructListView(Window.GetCurrentView(), Pointer, PropertyTyped.PropertyClass));
            }
        }
    }
}

public class ObjectPropertyValueViewer(nint baseAddress, IUnrealFactory factory, App owner, IFObjectProperty propertyTyped, IUnrealClasses unrealClasses)
    : BasePropertyValueViewer<IFObjectProperty>(baseAddress, propertyTyped, unrealClasses)
{
    private IUnrealFactory Factory = factory;
    private App Owner = owner;
    
    public override void Draw()
    {
        unsafe
        {
            var Pointer = *(nint*)(BaseAddress + PropertyTyped.Offset_Internal);
            if (Pointer == nint.Zero)
            {
                ImGui.Text("NULL");
                return;
            }
            if (ImGui.Button(
                    $"View Object##{Pointer:X}", 
                    ImGui.ImVec2ImVec2Nil()))
            {
                owner.TryAddWindow(Factory.CreateUObject(Pointer));
            }
        }
    }
}

public class EnumPropertyValueViewer(nint baseAddress, IFEnumProperty propertyTyped, IUnrealClasses unrealClasses) 
    : BasePropertyValueViewer<IFEnumProperty>(baseAddress, propertyTyped, unrealClasses)
{
    public override void Draw()
    {
        unsafe
        {
            if (PropertyTyped.Enum != null)
            {
                var Value = PropertyTyped.ElementSize switch
                {
                    1 => *(byte*)(BaseAddress + PropertyTyped.Offset_Internal),
                    2 => *(short*)(BaseAddress + PropertyTyped.Offset_Internal),
                    4 => *(int*)(BaseAddress + PropertyTyped.Offset_Internal),
                    _ => *(long*)(BaseAddress + PropertyTyped.Offset_Internal),
                };
                Dictionary<long, string> NamesDict = new();
                for (var i = 0; i < PropertyTyped.Enum.Names.ArrayNum; i++)
                {
                    var CurrentName = &PropertyTyped.Enum.Names.AllocatorInstance[i];
                    NamesDict.Add(CurrentName->Value, CurrentName->Key.ToString());
                }
                var FieldAddress = BaseAddress + PropertyTyped.Offset_Internal;
                if (ImGui.BeginCombo(
                        $"##0x{FieldAddress:x}", 
                        NamesDict.TryGetValue(Value, out var Name) ? Name : $"{Value}", 
                        0))
                {
                    for (var i = 0; i < PropertyTyped.Enum.Names.ArrayNum; i++)
                    {
                        var CurrentName = &PropertyTyped.Enum.Names.AllocatorInstance[i];
                        var NameKey = CurrentName->Key.ToString();
                        if (NameKey.EndsWith("_MAX")) continue;
                        if (ImGui.SelectableBool(
                                NameKey, CurrentName->Value == Value, 
                                0, ImGui.ImVec2ImVec2Nil()))
                        {
                            switch (PropertyTyped.ElementSize)
                            {
                                case 1:
                                    *(byte*)(BaseAddress + PropertyTyped.Offset_Internal) = (byte)CurrentName->Value;
                                    break;
                                case 2:
                                    *(short*)(BaseAddress + PropertyTyped.Offset_Internal) = (short)CurrentName->Value;
                                    break;
                                case 4:
                                    *(int*)(BaseAddress + PropertyTyped.Offset_Internal) = (int)CurrentName->Value;
                                    break;
                                default:
                                    *(long*)(BaseAddress + PropertyTyped.Offset_Internal) = CurrentName->Value;
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
                var Value = (int)*(byte*)(BaseAddress + PropertyTyped.Offset_Internal);
                if (ImGui.InputInt(
                        $"##Property_{PropertyTyped.Ptr:x}",
                        ref Value,
                        1, 1, 0))
                    *(byte*)(BaseAddress + PropertyTyped.Offset_Internal) = (byte)Value;   
            }
        }
    }
}

public class ArrayPropertyValueViewer(nint baseAddress, IFArrayProperty propertyTyped, IUnrealClasses unrealClasses, 
    UObjectWindow window) : BasePropertyValueViewer<IFArrayProperty>(baseAddress, propertyTyped, unrealClasses)
{
    protected UObjectWindow Window => window;
    
    public override void Draw()
    {
        var Address = BaseAddress + PropertyTyped.Offset_Internal;
        if (ImGui.Button(
                $"View Array##{Address:X}", 
                ImGui.ImVec2ImVec2Nil()))
        {
            Window.AddView(
                new(Address, Window.UnrealClasses!.GetPropertyTypeName(PropertyTyped)), 
                new ArrayListView(Window.GetCurrentView(), 
                Address, PropertyTyped, Window.UnrealClasses!, Window.UnrealMemory!)
                );
        }
        ImGui.SameLine(0, 10);
        unsafe
        {
            ImGui.Text($"Length: {((TArray<byte>*)Address)->ArrayNum}");
        }
        
    }
}

public class MapPropertyValueViewer(nint baseAddress, IFMapProperty propertyTyped, IUnrealClasses unrealClasses, 
    UObjectWindow window) : BasePropertyValueViewer<IFMapProperty>(baseAddress, propertyTyped, unrealClasses)
{
    protected UObjectWindow Window => window;
    
    public override void Draw()
    {
        var Address = BaseAddress + PropertyTyped.Offset_Internal;
        if (ImGui.Button(
                $"View Map##{Address:X}", 
                ImGui.ImVec2ImVec2Nil()))
        {
            Window.AddView(
                new(Address, Window.UnrealClasses!.GetPropertyTypeName(PropertyTyped)),  
                new MapListView(Window.GetCurrentView(), Address, 
                PropertyTyped, Window.UnrealMemory!, Window.UnrealFactory!, Window.UnrealClasses!)
                );
        }
        ImGui.SameLine(0, 10);
        unsafe
        {
            ImGui.Text($"Length: {((TArray<byte>*)Address)->ArrayNum}");
        }
        
    }
}