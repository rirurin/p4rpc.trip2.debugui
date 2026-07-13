using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer;

public class TypeName(IUnrealFactory factory)
{
    private IUnrealFactory Factory { get; } = factory;

    /// <summary>
    /// Gets the property type name, such as <c>byte</c> for <c>ByteProperty</c>.
    /// </summary>
    /// <param name="prop"></param>
    /// <returns></returns>
    internal string GetPropertyTypeName(IFProperty prop)
    {
        var className = prop.ClassPrivate.Name;
        switch (className)
        {
            case "BoolProperty":
                return "bool";
            case "ByteProperty" or "Int8Property":
                return "byte";
            case "Int16Property":
                return "short";
            case "UInt16Property":
                return "ushort";
            case "IntProperty":
                return "int";
            case "UInt32Property":
                return "uint";
            case "Int64Property":
                return "long";
            case "UInt64Property":
                return "ulong";
            case "FloatProperty":
                return "float";
            case "DoubleProperty":
                return "double";
            case "NameProperty":
                return "FName";
            case "StrProperty":
                return "FString";
            case "TextProperty":
                return "FText";
            case "DataTableRowHandle":
                return "FDataTableRowHandle";
            case "ObjectProperty":
                return $"{Factory.Cast<IFObjectProperty>(prop).PropertyClass.NamePrivate}*";
            case "SoftObjectProperty":
                return $"TSoftObjectPtr<{Factory.Cast<IFObjectProperty>(prop).PropertyClass.NamePrivate}>";
            case "SoftClassProperty":
                return $"TSoftClassPtr<{Factory.Cast<IFSoftClassProperty>(prop).MetaClass.NamePrivate}>";
            case "StructProperty":
                return Factory.Cast<IFStructProperty>(prop).Struct.NamePrivate.ToString();
            case "ClassProperty":
            case "ClassPtrProperty":
                return Factory.Cast<IFClassProperty>(prop).MetaClass!.NamePrivate.ToString();
            case "EnumProperty":
                return Factory.Cast<IFEnumProperty>(prop).Enum.NamePrivate.ToString();
            case "MapProperty":
                var mapProp = Factory.Cast<IFMapProperty>(prop);
                var mapPropKeyType = GetPropertyTypeName(mapProp.KeyProp);
                var mapPropValueType = GetPropertyTypeName(mapProp.ValueProp);
                return $"TMap<{mapPropKeyType}, {mapPropValueType}>";
            case "InterfaceProperty":
                return $"TScriptInterface<{Factory.Cast<IFInterfaceProperty>(prop).NamePrivate}>";
            case "ArrayProperty":
                return $"TArray<{GetPropertyTypeName(Factory.Cast<IFArrayProperty>(prop).Inner)}>";
            case "SetProperty":
                return $"TSet<{GetPropertyTypeName(Factory.Cast<IFSetProperty>(prop).ElementProp)}>";
            case "DelegateProperty":
                return "FScriptDelegate";
            case "MulticastInlineDelegateProperty":
            case "MulticastSparseDelegateProperty":
                return "FMulticastScriptDelegate";
            case "WeakObjectProperty":
                return "FWeakObjectPtr";
            case "FieldPathProperty":
                return "FFieldPath";
            case "Utf8StrProperty":
                return "FUtf8String";
            case "AnsiStrProperty":
                return "FAnsiString";
            default:
                Log.Warning($"Unknown Property: {className}");
                return className;
        }
    }
}