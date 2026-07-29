extern alias imgui;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Common.FunctionParam;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;
using UE.Toolkit.Core.Types.Unreal.UE5_6_1;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class FunctionParamListView(
    PropertyListView? parent,
    nint baseAddress,
    IUObject owner,
    IUFunction function,
    IUnrealFactory factory,
    IUnrealMemory memory,
    IUnrealClasses classes)
    : StructListViewBase<IUFunction>(parent, baseAddress, function), IDisposable
{
    private IUObject Owner { get; } = owner;

    internal IUnrealFactory Factory { get; } = factory;
    internal IUnrealMemory Memory { get; } = memory;
    internal IUnrealClasses Classes { get; } = classes;
    
    private static IFunctionParam CreateStructParam(IFStructProperty property, nint Address) => new StructParam(Address, property.ElementSize);
    private static unsafe IFunctionParam CreateBoolParam(IFBoolProperty property, nint Address) => new BoolParam(new((bool*)Address), property.FieldMask);

    public override void Draw(App owner, UObjectWindow window)
    {
        base.Draw(owner, window);
        if (ImGui.Button($"Call Function##0x{BaseAddress:x}", ImGui.ImVec2ImVec2Nil()))
        {
            List<IFunctionParam> Params = [];
            int? ReturnValueOffset = null;
            foreach (var Param in Value.PropertyLink)
            {
                if (Param.PropertyFlags.HasFlag(EPropertyFlags.CPF_ReturnParm))
                {
                    ReturnValueOffset = Param.Offset_Internal;
                    break;
                }
                var Address = BaseAddress + Param.Offset_Internal;
                unsafe
                {
                    Params.Add(Param.ClassPrivate.Name switch
                    {
                        "BoolProperty" => CreateBoolParam(factory.CreateFBoolProperty(Param.Ptr), Address),
                        "Int8Property" => new Int8Param(new((byte*)Address)),
                        "ByteProperty" => new ByteParam(new((byte*)Address)),
                        "Int16Property" => new Int16Param(new((short*)Address)),
                        "Int32Property" => new Int32Param(new((int*)Address)),
                        "IntProperty" => new IntParam(new((int*)Address)),
                        "Int64Property" => new Int64Param(new((long*)Address)),
                        "UInt16Property" => new UInt16Param(new((ushort*)Address)),
                        "UInt32Property" => new UInt32Param(new((uint*)Address)),
                        "UInt64Property" => new UInt64Param(new((ulong*)Address)),
                        "FloatProperty" => new FloatParam(new((float*)Address)),
                        "DoubleProperty" => new DoubleParam(new((double*)Address)),
                        "NameProperty" => new NameParam(new((FName*)Address)),
                        "StrProperty" => new StringParam(new((FString*)Address)),
                        "StructProperty" => CreateStructParam(factory.CreateFStructProperty(Param.Ptr), Address),
                        "TextProperty" => new TextParam(Address, Classes.GetFTextSize()),
                        "ObjectProperty" or "ClassProperty" or "ClassPtrProperty" => new ObjectParam(new((nint*)Address)),
                        "ArrayProperty" => new ArrayParam(new((TArray<int>*)Address)),
                        "EnumProperty" => new EnumParam(Address, Param.ElementSize),
                        "MapProperty" => new MapParam(new((TMap<int, int>*)Address)),
                        "SetProperty" => new SetParam(new((TSet<int>*)Address)),
                        "InterfaceProperty" => new InterfaceParam(new((TScriptInterface<int>*)Address)),
                        "SoftClassProperty" => new SoftClassParam(new((TSoftClassPtr<int>*)Address)),
                        "SoftObjectProperty" => new SoftObjectParam(new((TSoftObjectPtr<int>*)Address)),
                        "Utf8StrProperty" => new Utf8StringParam(new((FUtf8String*)Address)),
                        "AnsiStrProperty" => new AnsiStringParam(new((FAnsiString*)Address)),
                        "DelegateProperty" => new DelegateParam(new((FScriptDelegate*)Address)),
                        "MulticastInlineDelegateProperty" => new MulticastInlineDelegateParam(new((FMulticastScriptDelegate*)Address)),
                        "MulticastSparseDelegateProperty" => new MulticastSparseDelegateParam(new((FMulticastSparseDelegateProperty*)Address)),
                        _ => throw new NotSupportedException($"CreateParam with property {Param.ClassPrivate.Name}")
                    });
                }
            }
            var Result = Owner.ProcessEvent(Value.NamePrivate.ToString(), Params, out var ReturnValue);
            if (Result != ProcessEventResult.Success)
                Log.Error($"{nameof(FunctionParamListView)} || Could not call {Owner.NamePrivate}->{Value.NamePrivate}: {Result}");
            if (ReturnValueOffset.HasValue)
                ReturnValue.Write(BaseAddress + ReturnValueOffset.Value);
        }
    }

    // protected override List<IFProperty> GetPropertyList(App owner)
    //     => base.GetPropertyList(owner).Where(x => !x.PropertyFlags.HasFlag(EPropertyFlags.CPF_ReturnParm)).ToList();

    #region IDisposable Interface

    private bool IsDisposed;

    protected virtual void Dispose(bool _disposing)
    {
        if (IsDisposed) return;
        Memory.Free(BaseAddress);
        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~FunctionParamListView() => Dispose(false);

    #endregion
}