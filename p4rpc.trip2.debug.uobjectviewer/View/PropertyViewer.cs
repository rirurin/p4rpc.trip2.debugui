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
                owner.Context.UnrealFactory.CreateFBoolProperty(Property.Ptr), owner.Context.UnrealClasses),
            "ByteProperty" => new BytePropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFByteProperty(Property.Ptr), owner.Context.UnrealClasses),
            "Int8Property" => new Int8PropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "Int16Property" => new Int16PropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "UInt16Property" => new UInt16PropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "IntProperty" => new IntPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "UInt32Property" => new UntypedPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "Int64Property" => new UntypedPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "UInt64Property" => new UntypedPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "FloatProperty" => new FloatPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "DoubleProperty" => new DoublePropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "NameProperty" => new NamePropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "StrProperty" => new StringPropertyValueViewer(BaseAddress, owner.Context.UnrealStrings,
                owner.Context.UnrealMemory, Property, owner.Context.UnrealClasses),
            "TextProperty" => new TextPropertyValueViewer(BaseAddress, owner.Context.UnrealStrings, 
                owner.Context.UnrealMemory, Property, owner.Context.UnrealClasses, owner.Context.UnrealObjects),
            "ObjectProperty" => new ObjectPropertyValueViewer(BaseAddress, owner.Context.UnrealFactory, owner,
                owner.Context.UnrealFactory.CreateFObjectProperty(Property.Ptr), owner.Context.UnrealClasses),
            "SoftObjectProperty" => // TODO
                new UntypedPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "SoftClassProperty" => // TODO
                new UntypedPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            "StructProperty" => new StructPropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFStructProperty(Property.Ptr), owner.Context.UnrealClasses, window),
            "EnumProperty" => new EnumPropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFEnumProperty(Property.Ptr), owner.Context.UnrealClasses),
            "MapProperty" => new MapPropertyValueViewer(BaseAddress, 
                owner.Context.UnrealFactory.CreateFMapProperty(Property.Ptr), owner.Context.UnrealClasses, window),
            "ArrayProperty" => new ArrayPropertyValueViewer(BaseAddress,
                owner.Context.UnrealFactory.CreateFArrayProperty(Property.Ptr), owner.Context.UnrealClasses, window),
            "SetProperty" => // TODO
                new UntypedPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses),
            _ => new UntypedPropertyValueViewer(BaseAddress, Property, owner.Context.UnrealClasses)
        };
    }
    
    public IPropertyValueViewer GetViewerDataTable(App owner, UObjectWindow window)
    {
        return Property.ClassPrivate.Name switch
        {
            "ObjectProperty" => new DataTableRowPropertyValueViewer(BaseAddress, owner.Context.UnrealFactory, 
                owner.Context.UnrealFactory.CreateFObjectProperty(Property.Ptr), owner.Context.UnrealClasses, window),
            _ => GetViewer(owner, window)
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