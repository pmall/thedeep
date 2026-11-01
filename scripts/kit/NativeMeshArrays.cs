using System;
using Godot;
using Godot.NativeInterop;

namespace TheDeep;

/// <summary>
/// A Godot mesh-array (<see cref="Mesh.ArrayType"/> slots) whose slots are written in place, for a mesh rebuilt every
/// frame (a chain, <see cref="KitMesh.Buffers"/>). Setting a slot through <see cref="Godot.Collections.Array"/>'s indexer
/// wraps each packed array in a <see cref="Variant"/> that registers a finalizable weak reference for disposal: seven per
/// chain per frame, the last steady garbage of a room full of machines. Here each slot's old packed array is released and
/// the new one written straight into the engine array, so nothing managed is allocated.
/// <para>
/// <c>unsafe</c> is allowed in the game assembly only here and in <see cref="FrameGrabber"/> (docs/principles.md#performance).
/// </para>
/// </summary>
public sealed unsafe class NativeMeshArrays : IDisposable
{
    /// <summary>
    /// A reference to the same engine array as <see cref="Managed"/>, kept for writing. <c>godot_array</c> is a ref struct
    /// (no class may hold one), so it lives in native memory.
    /// </summary>
    private godot_array* _native;

    /// <summary>The array to hand to <see cref="ArrayMesh.AddSurfaceFromArrays"/>.</summary>
    public Godot.Collections.Array Managed { get; } = new();

    public NativeMeshArrays()
    {
        Managed.Resize((int)Mesh.ArrayType.Max);
        // Once: a second reference to the managed array's engine array (Godot arrays are shared by reference).
        Variant wrapped = Variant.CreateFrom(Managed);
        godot_variant variant = wrapped.CopyNativeVariant();
        _native = (godot_array*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(godot_array));
        *_native = VariantUtils.ConvertToNativeArray(variant);
        variant.Dispose();
        wrapped.Dispose();
    }

    ~NativeMeshArrays() => Release();

    public void Set(Mesh.ArrayType slot, Span<Vector3> values) => Write(slot, VariantUtils.CreateFromPackedVector3Array(values));

    public void Set(Mesh.ArrayType slot, Span<Vector2> values) => Write(slot, VariantUtils.CreateFromPackedVector2Array(values));

    public void Set(Mesh.ArrayType slot, Span<float> values) => Write(slot, VariantUtils.CreateFromPackedFloat32Array(values));

    public void Set(Mesh.ArrayType slot, Span<Color> values) => Write(slot, VariantUtils.CreateFromPackedColorArray(values));

    public void Set(Mesh.ArrayType slot, Span<int> values) => Write(slot, VariantUtils.CreateFromPackedInt32Array(values));

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Write(Mesh.ArrayType slot, godot_variant value)
    {
        if (_native is null)
        {
            value.Dispose();
            throw new ObjectDisposedException(nameof(NativeMeshArrays));
        }

        godot_variant* item = NativeFuncs.godotsharp_array_ptrw(ref *_native) + (int)slot;
        item->Dispose();
        *item = value;
    }

    private void Release()
    {
        if (_native is not null)
        {
            _native->Dispose();
            System.Runtime.InteropServices.NativeMemory.Free(_native);
            _native = null;
        }
    }
}
