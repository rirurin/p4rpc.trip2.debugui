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
    
    public TypeName TypeName { get; }
    
    public void Draw();
}

public abstract class BasePropertyValueViewer<TProperty>(nint baseAddress, TProperty propertyTyped, TypeName typeName)
    : IPropertyValueViewer where TProperty: IFProperty
{
    // start impl IPropertyValueViewer
    public nint BaseAddress => baseAddress;
    public IFProperty Property => PropertyTyped;
    public TypeName TypeName => typeName;
    public abstract void Draw();
    // end impl IPropertyValueViewer
    
    internal TProperty PropertyTyped => propertyTyped;
}

public class UntypedPropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, typeName)
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

public class BoolPropertyValueViewer(nint baseAddress, IFBoolProperty propertyTyped, TypeName typeName) 
    : BasePropertyValueViewer<IFBoolProperty>(baseAddress, propertyTyped, typeName)
{
    public override void Draw()
    {
        unsafe
        {
            var Value = (*(byte*)(BaseAddress + PropertyTyped.Offset_Internal) & PropertyTyped.FieldMask) != 0;
            if (ImGui.Checkbox($"##Property_{PropertyTyped.Ptr:x}", ref Value))
            {
                if (Value) *(byte*)(BaseAddress + PropertyTyped.Offset_Internal) |= PropertyTyped.FieldMask;
                else *(byte*)(BaseAddress + PropertyTyped.Offset_Internal) &= (byte)~PropertyTyped.FieldMask;
            }
        }
    }
}

public class BytePropertyValueViewer(nint baseAddress, IFByteProperty propertyTyped, TypeName typeName) 
    : BasePropertyValueViewer<IFByteProperty>(baseAddress, propertyTyped, typeName)
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

public class Int8PropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, typeName)
{
    public override void Draw()
    {
        
        unsafe
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

public class Int16PropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, typeName)
{
    public override void Draw()
    {
        
        unsafe
        {
            var Value = (int)*(short*)(BaseAddress + PropertyTyped.Offset_Internal);
            if (ImGui.InputInt(
                    $"##Property_{PropertyTyped.Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(short*)(BaseAddress + PropertyTyped.Offset_Internal) = (short)Value;
        }
    }
}

public class IntPropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, typeName)
{
    public override void Draw()
    {
        
        unsafe
        {
            ImGui.InputInt(
                $"##Property_{PropertyTyped.Ptr:x}", 
                ref *(int*)(BaseAddress + PropertyTyped.Offset_Internal), 
                1, 1, 0);
        }
    }
}

public class UInt16PropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, typeName)
{
    public override void Draw()
    {
        
        unsafe
        {
            var Value = (int)*(ushort*)(BaseAddress + PropertyTyped.Offset_Internal);
            if (ImGui.InputInt(
                    $"##Property_{PropertyTyped.Ptr:x}",
                    ref Value,
                    1, 1, 0))
                *(ushort*)(BaseAddress + PropertyTyped.Offset_Internal) = (ushort)Value;
        }
    }
}

public class FloatPropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, typeName)
{
    public override void Draw()
    {
        
        unsafe
        {
            ImGui.InputFloat(
                $"##Property_{PropertyTyped.Ptr:x}", 
                ref *(float*)(BaseAddress + PropertyTyped.Offset_Internal), 
                1, 1, "%f", 0);
        }
    }
}

public class DoublePropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFProperty>(baseAddress, propertyTyped, typeName)
{
    public override void Draw()
    {
        unsafe
        {
            ImGui.InputDouble(
                $"##Property_{PropertyTyped.Ptr:x}", 
                ref *(double*)(BaseAddress + PropertyTyped.Offset_Internal), 
                1, 1, "%f", 0);
        }
    }
}

public class NamePropertyValueViewer : BasePropertyValueViewer<IFProperty>
{
    protected ResizableTextInput TextInput;

    public NamePropertyValueViewer(nint baseAddress, IFProperty propertyTyped, TypeName typeName) : base(baseAddress, propertyTyped, typeName)
    {
        TextInput = new($"##Name @ 0x{BaseAddress:X}");
        unsafe { TextInput.ReplaceBuffer(((FName*)(BaseAddress + PropertyTyped.Offset_Internal))->ToString() + '\0'); }
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
        IFProperty propertyTyped, TypeName typeName) : base(baseAddress, propertyTyped, typeName)
    {
        TextInput = new($"##String @ 0x{BaseAddress:X}");
        UnrealStrings = unrealStrings;
        UnrealMemory = unrealMemory;
        unsafe
        {
            var strValue = (FString*)(BaseAddress + PropertyTyped.Offset_Internal);
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
        IFProperty property, TypeName typeName, IUnrealObjects unrealObjects) : base(baseAddress, property, typeName)
    {
        TextInput = new($"##Text @ 0x{BaseAddress:X}");
        UnrealStrings = unrealStrings;
        UnrealMemory = unrealMemory;
        UnrealObjects = unrealObjects;
        unsafe
        {
            var textValue = (FText*)(BaseAddress + Property.Offset_Internal);
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

public class StructPropertyValueViewer(nint baseAddress, IFStructProperty propertyTyped, TypeName typeName,
    UObjectWindow window) : BasePropertyValueViewer<IFStructProperty>(baseAddress, propertyTyped, typeName)
{
    protected UObjectWindow Window => window;
    
    public override void Draw()
    {
        var Address = BaseAddress + PropertyTyped.Offset_Internal;
        if (ImGui.Button(
                $"View Struct##{Address:X}", 
                ImGui.ImVec2ImVec2Nil()))
        {
            Window.AddView(Address, new StructListView(Window.GetCurrentView(), Address, PropertyTyped.Struct));
        }
        ImGui.SameLine(0, 10);
        ImGui.Text($"0x{Address:X}");
    }
}

public class ObjectPropertyValueViewer(nint baseAddress, IUnrealFactory factory, App owner, IFObjectProperty propertyTyped, TypeName typeName)
    : BasePropertyValueViewer<IFObjectProperty>(baseAddress, propertyTyped, typeName)
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
                    $"View Object##{BaseAddress + PropertyTyped.Offset_Internal:X}", 
                    ImGui.ImVec2ImVec2Nil()))
            {
                owner.Windows.Add(new UObjectWindow(Factory.CreateUObject(Pointer), owner));
            }
        }
    }
}

public class EnumPropertyValueViewer(nint baseAddress, IFEnumProperty propertyTyped, TypeName typeName) 
    : BasePropertyValueViewer<IFEnumProperty>(baseAddress, propertyTyped, typeName)
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
                                    *(byte*)(BaseAddress + PropertyTyped.Offset_Internal) = (byte)Value;
                                    break;
                                case 2:
                                    *(short*)(BaseAddress + PropertyTyped.Offset_Internal) = (short)Value;
                                    break;
                                case 4:
                                    *(int*)(BaseAddress + PropertyTyped.Offset_Internal) = (int)Value;
                                    break;
                                default:
                                    *(long*)(BaseAddress + PropertyTyped.Offset_Internal) = Value;
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

public class ArrayPropertyValueViewer(nint baseAddress, IFArrayProperty propertyTyped, TypeName typeName, 
    UObjectWindow window) : BasePropertyValueViewer<IFArrayProperty>(baseAddress, propertyTyped, typeName)
{
    protected UObjectWindow Window => window;
    
    public override void Draw()
    {
        var Address = BaseAddress + PropertyTyped.Offset_Internal;
        if (ImGui.Button(
                $"View Array##{Address:X}", 
                ImGui.ImVec2ImVec2Nil()))
        {
            Window.AddView(Address, new ArrayListView(Window.GetCurrentView(), Address, PropertyTyped));
        }
        ImGui.SameLine(0, 10);
        unsafe
        {
            ImGui.Text($"Length: {((TArray<byte>*)Address)->ArrayNum}");
        }
        
    }
}

public class MapPropertyValueViewer(nint baseAddress, IFMapProperty propertyTyped, TypeName typeName, 
    UObjectWindow window) : BasePropertyValueViewer<IFMapProperty>(baseAddress, propertyTyped, typeName)
{
    protected UObjectWindow Window => window;
    
    public override void Draw()
    {
        var Address = BaseAddress + PropertyTyped.Offset_Internal;
        if (ImGui.Button(
                $"View Map##{Address:X}", 
                ImGui.ImVec2ImVec2Nil()))
        {
            Window.AddView(Address, new MapListView(Window.GetCurrentView(), Address, PropertyTyped));
        }
        ImGui.SameLine(0, 10);
        unsafe
        {
            ImGui.Text($"Length: {((TArray<byte>*)Address)->ArrayNum}");
        }
        
    }
}