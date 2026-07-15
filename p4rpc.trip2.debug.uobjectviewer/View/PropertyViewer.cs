extern alias imgui;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class PropertyViewer(nint baseAddress, IFProperty inner)
{
    public IFProperty Property => inner;
    public nint BaseAddress => baseAddress;
    
    public IPropertyValueViewer GetViewer(App owner, UObjectWindow window)
    {
        return Property.ClassPrivate.Name switch
        {
            "BoolProperty" => new BoolPropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFBoolProperty(Property.Ptr), owner.TypeName),
            "ByteProperty" => new BytePropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFByteProperty(Property.Ptr), owner.TypeName),
            "Int8Property" => new Int8PropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "Int16Property" => new Int16PropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "UInt16Property" => new UInt16PropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "IntProperty" => new IntPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "UInt32Property" => new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "Int64Property" => new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "UInt64Property" => new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "FloatProperty" => new FloatPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "DoubleProperty" => new DoublePropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "NameProperty" => new NamePropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "StrProperty" => new StringPropertyValueViewer(BaseAddress, owner.Context.UnrealStrings,
                owner.Context.UnrealMemory, Property, owner.TypeName),
            "TextProperty" => new TextPropertyValueViewer(BaseAddress, owner.Context.UnrealStrings, 
                owner.Context.UnrealMemory, Property, owner.TypeName, owner.Context.UnrealObjects),
            "ObjectProperty" => new ObjectPropertyValueViewer(BaseAddress, owner.Context.UnrealFactory, owner,
                owner.Context.UnrealFactory.CreateFObjectProperty(Property.Ptr), owner.TypeName),
            "SoftObjectProperty" => // TODO
                new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "SoftClassProperty" => // TODO
                new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "StructProperty" => new StructPropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFStructProperty(Property.Ptr), owner.TypeName, window),
            "EnumProperty" => new EnumPropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFEnumProperty(Property.Ptr), owner.TypeName),
            "MapProperty" => // TODO
                new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            "ArrayProperty" => new ArrayPropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFArrayProperty(Property.Ptr), owner.TypeName, window),
            "SetProperty" => // TODO
                new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName),
            _ => new UntypedPropertyValueViewer(BaseAddress, Property, owner.TypeName)
        };
    }
}

public interface IPropertyViewer
{
    void Draw(App owner, UObjectWindow window);
}

public abstract class BasePropertyViewer(nint baseAddress, IFProperty inner) : IPropertyViewer
{
    protected PropertyViewer Inner { get; private init; } = new(baseAddress, inner);

    public abstract void Draw(App owner, UObjectWindow window);
}