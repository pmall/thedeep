using System;
using System.Runtime.InteropServices;
using Godot;
using Godot.Collections;

namespace TheDeep;

/// <summary>
/// The game's painted signature (docs/art-direction.md#signature): one pass over every rendered frame, after the
/// transparent geometry and before glow and tone mapping, that makes the frame look laid in gouache. It runs six compute
/// shaders (assets/shaders/paint/, each header says its step): the frame at half resolution, the direction of its edges,
/// that direction smoothed, an anisotropic Kuwahara brush, the strokes laid over it (still at half resolution), then the
/// composite back at full resolution with the hand's wobble, pooling, value bands, the layer's pigments, paper grain and
/// the machines' script. The paper is a texture drawn once (paint_paper.glsl).
///
/// One instance is shared (resources/stage/paint_pass.tres, in the rooms' compositor): the room's biome gives it its
/// style and pigments (<see cref="Use"/>); <see cref="Override"/> replaces the biome's style (`--paint`, F9) and
/// <c>Enabled</c> switches the pass off to see the raw render.
/// </summary>
[GlobalClass]
public partial class PaintPass : CompositorEffect
{
    [Export] public RDShaderFile Prepare { get; set; } = null!;
    [Export] public RDShaderFile Tensor { get; set; } = null!;
    [Export] public RDShaderFile TensorBlur { get; set; } = null!;
    [Export] public RDShaderFile Kuwahara { get; set; } = null!;
    [Export] public RDShaderFile Strokes { get; set; } = null!;
    [Export] public RDShaderFile Composite { get; set; } = null!;

    /// <summary>Makes the paper texture, once.</summary>
    [Export] public RDShaderFile Paper { get; set; } = null!;

    /// <summary>The style the room's biome asks for; null paints nothing.</summary>
    public PaintStyle? BiomeStyle { get; private set; }

    /// <summary>A style used instead of the biome's, in every room until cleared (variants compared with `--paint` or F9).</summary>
    public PaintStyle? Override { get; set; }

    /// <summary>The style painting now.</summary>
    public PaintStyle? Style => Override ?? BiomeStyle;

    /// <summary>How many pigments the composite pulls hues toward (paint_params.glslinc's palette).</summary>
    public const int PigmentCount = 6;

    /// <summary>The steps in the order they run, as <see cref="StepMs"/> names them.</summary>
    public static readonly string[] StepNames = { "prepare", "tensor", "blur_x", "blur_y", "kuwahara", "strokes", "composite" };

    /// <summary>
    /// Debug only (THEDEEP_PAINT_TIMING=1, read by the perf recording): each step is bracketed by device timestamps,
    /// and <see cref="StepMs"/> holds its GPU time. Off, the pass captures nothing.
    /// </summary>
    public static bool Timed { get; set; }

    /// <summary>GPU ms of each step (<see cref="StepNames"/>) in the latest frame whose timestamps came back (a frame or two behind); zero while none did.</summary>
    public static double[] StepMs { get; } = new double[7];

    private static readonly string[] Stamps = { "paint:start", "paint:prepare", "paint:tensor", "paint:blur_x", "paint:blur_y", "paint:kuwahara", "paint:strokes", "paint:composite" };

    private static readonly StringName RenderBuffers = "render_buffers";
    private static readonly StringName ColorName = "color";
    private static readonly StringName DepthName = "depth";
    private static readonly StringName Context = "paint";
    private static readonly StringName HalfName = "half";
    private static readonly StringName TensorName = "tensor";
    private static readonly StringName TensorTempName = "tensor_temp";
    private static readonly StringName PaintedName = "painted";
    private static readonly StringName StrokedName = "stroked";

    /// <summary>Floats in the Params block: eleven vec4 and the pigments.</summary>
    private const int ParamFloats = 4 * (11 + PigmentCount);

    /// <summary>The paper texture's side (paint_paper.glsl's SIZE).</summary>
    private const int PaperSize = 512;

    private const uint Group = 8;

    private readonly float[] _params = new float[ParamFloats];
    private readonly Vector4[] _pigments = new Vector4[PigmentCount];

    private RenderingDevice? _rd;
    private Rid _ubo;
    private Rid _linear;
    private Rid _nearest;
    private Rid _repeat;
    private Rid _paper;
    private Stage _prepare;
    private Stage _tensor;
    private Stage _tensorBlur;
    private Stage _kuwahara;
    private Stage _strokes;
    private Stage _composite;

    /// <summary>The textures the cached uniform sets were built for; any change rebuilds them.</summary>
    private Rid _forColor;
    private Rid _forDepth;
    private Rid _forHalf;
    private Rid _prepareSet;
    private Rid _tensorSet;
    private Rid _blurXSet;
    private Rid _blurYSet;
    private Rid _kuwaharaSet;
    private Rid _strokesSet;
    private Rid _compositeSet;

    /// <summary>One compute shader and its pipeline.</summary>
    private readonly record struct Stage(Rid Shader, Rid Pipeline);

    public PaintPass()
    {
        EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
        AccessResolvedColor = true;
        AccessResolvedDepth = true;
    }

    /// <summary>
    /// The room's biome paints with <paramref name="style"/> (null: no paint) and pulls hues toward
    /// <paramref name="pigments"/> (sRGB colors, at most <see cref="PigmentCount"/>).
    /// </summary>
    public void Use(PaintStyle? style, ReadOnlySpan<Color> pigments)
    {
        BiomeStyle = style;
        for (int i = 0; i < PigmentCount; i++)
        {
            if (i < pigments.Length)
            {
                Color c = pigments[i].SrgbToLinear();
                _pigments[i] = new Vector4(c.R / (1f + c.R), c.G / (1f + c.G), c.B / (1f + c.B), 1f);
            }
            else
            {
                _pigments[i] = Vector4.Zero;
            }
        }
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        if (effectCallbackType != (int)EffectCallbackTypeEnum.PostTransparent || Style is not { } style)
        {
            return;
        }

        if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD buffers || !EnsureDevice(out RenderingDevice rd))
        {
            return;
        }

        Vector2I size = buffers.GetInternalSize();
        if (size.X < 2 || size.Y < 2)
        {
            return;
        }

        var half = new Vector2I((size.X + 1) / 2, (size.Y + 1) / 2);
        Rid halfTexture = Texture(buffers, HalfName, half);
        Rid tensor = Texture(buffers, TensorName, half);
        Rid tensorTemp = Texture(buffers, TensorTempName, half);
        Rid painted = Texture(buffers, PaintedName, half);
        Rid stroked = Texture(buffers, StrokedName, half);
        // The resolved buffers (AccessResolvedColor, AccessResolvedDepth): with MSAA, the layer views are not storage.
        Rid color = buffers.GetTexture(RenderBuffers, ColorName);
        Rid depth = buffers.GetTexture(RenderBuffers, DepthName);

        if (color != _forColor || depth != _forDepth || halfTexture != _forHalf || !rd.UniformSetIsValid(_compositeSet))
        {
            BuildSets(rd, color, depth, halfTexture, tensor, tensorTemp, painted, stroked);
        }

        WriteParams(rd, renderData.GetRenderSceneData(), style, size, half);

        uint fullX = ((uint)size.X + Group - 1) / Group;
        uint fullY = ((uint)size.Y + Group - 1) / Group;
        uint halfX = ((uint)half.X + Group - 1) / Group;
        uint halfY = ((uint)half.Y + Group - 1) / Group;

        if (Timed)
        {
            ReadStamps(rd);
        }

        // One compute list per step: the render graph puts the barriers between them.
        Stamp(rd, 0);
        Dispatch(rd, _prepare, _prepareSet, halfX, halfY);
        Stamp(rd, 1);
        Dispatch(rd, _tensor, _tensorSet, halfX, halfY);
        Stamp(rd, 2);
        Dispatch(rd, _tensorBlur, _blurXSet, halfX, halfY, 1f, 0f);
        Stamp(rd, 3);
        Dispatch(rd, _tensorBlur, _blurYSet, halfX, halfY, 0f, 1f);
        Stamp(rd, 4);
        Dispatch(rd, _kuwahara, _kuwaharaSet, halfX, halfY);
        Stamp(rd, 5);
        Dispatch(rd, _strokes, _strokesSet, halfX, halfY);
        Stamp(rd, 6);
        Dispatch(rd, _composite, _compositeSet, fullX, fullY);
        Stamp(rd, 7);
    }

    private static void Stamp(RenderingDevice rd, int i)
    {
        if (Timed)
        {
            rd.CaptureTimestamp(Stamps[i]);
        }
    }

    /// <summary>Turns the device's timestamps that came back (the frame's whose slot this is) into <see cref="StepMs"/>.</summary>
    private static void ReadStamps(RenderingDevice rd)
    {
        uint count = rd.GetCapturedTimestampsCount();
        for (uint i = 0; i + Stamps.Length <= count; i++)
        {
            if (rd.GetCapturedTimestampName(i) != Stamps[0])
            {
                continue;
            }

            for (int step = 0; step < StepMs.Length; step++)
            {
                // The device returns nanoseconds.
                ulong from = rd.GetCapturedTimestampGpuTime(i + (uint)step);
                ulong to = rd.GetCapturedTimestampGpuTime(i + (uint)step + 1);
                StepMs[step] = to > from ? (to - from) / 1e6 : 0.0;
            }

            return;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete && _rd is { } rd)
        {
            foreach (Rid set in new[] { _prepareSet, _tensorSet, _blurXSet, _blurYSet, _kuwaharaSet, _strokesSet, _compositeSet })
            {
                Free(rd, set);
            }

            foreach (Stage stage in new[] { _prepare, _tensor, _tensorBlur, _kuwahara, _strokes, _composite })
            {
                Free(rd, stage.Pipeline);
                Free(rd, stage.Shader);
            }

            Free(rd, _ubo);
            Free(rd, _paper);
            Free(rd, _linear);
            Free(rd, _nearest);
            Free(rd, _repeat);
        }
    }

    private static void Free(RenderingDevice rd, Rid rid)
    {
        if (rid.IsValid)
        {
            rd.FreeRid(rid);
        }
    }

    /// <summary>The device, shaders, samplers and parameter block, made on the first frame painted.</summary>
    private bool EnsureDevice(out RenderingDevice rd)
    {
        if (_rd is { } ready)
        {
            rd = ready;
            return _composite.Pipeline.IsValid;
        }

        rd = RenderingServer.GetRenderingDevice();
        _rd = rd;
        _prepare = Compile(rd, Prepare);
        _tensor = Compile(rd, Tensor);
        _tensorBlur = Compile(rd, TensorBlur);
        _kuwahara = Compile(rd, Kuwahara);
        _strokes = Compile(rd, Strokes);
        _composite = Compile(rd, Composite);
        Stage paper = Compile(rd, Paper);
        _ubo = rd.UniformBufferCreate(ParamFloats * sizeof(float), MemoryMarshal.AsBytes(_params.AsSpan()));
        _linear = rd.SamplerCreate(new RDSamplerState
        {
            MinFilter = RenderingDevice.SamplerFilter.Linear,
            MagFilter = RenderingDevice.SamplerFilter.Linear,
            RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge,
            RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge,
        });
        _nearest = rd.SamplerCreate(new RDSamplerState
        {
            MinFilter = RenderingDevice.SamplerFilter.Nearest,
            MagFilter = RenderingDevice.SamplerFilter.Nearest,
            RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge,
            RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge,
        });
        _repeat = rd.SamplerCreate(new RDSamplerState
        {
            MinFilter = RenderingDevice.SamplerFilter.Linear,
            MagFilter = RenderingDevice.SamplerFilter.Linear,
            RepeatU = RenderingDevice.SamplerRepeatMode.Repeat,
            RepeatV = RenderingDevice.SamplerRepeatMode.Repeat,
        });
        _paper = MakePaper(rd, paper);
        Free(rd, paper.Pipeline);
        Free(rd, paper.Shader);

        return _paper.IsValid && _prepare.Pipeline.IsValid && _tensor.Pipeline.IsValid && _tensorBlur.Pipeline.IsValid
            && _kuwahara.Pipeline.IsValid && _strokes.Pipeline.IsValid && _composite.Pipeline.IsValid;
    }

    /// <summary>The paper texture (paint_paper.glsl), drawn once.</summary>
    private static Rid MakePaper(RenderingDevice rd, Stage stage)
    {
        if (!stage.Pipeline.IsValid)
        {
            return default;
        }

        var format = new RDTextureFormat
        {
            Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
            Width = PaperSize,
            Height = PaperSize,
            UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit,
        };
        Rid paper = rd.TextureCreate(format, new RDTextureView());
        var image = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = 0 };
        image.AddId(paper);
        Rid set = rd.UniformSetCreate(new Array<RDUniform> { image }, stage.Shader, 0);
        Dispatch(rd, stage, set, PaperSize / Group, PaperSize / Group);
        rd.FreeRid(set);
        return paper;
    }

    private static Stage Compile(RenderingDevice rd, RDShaderFile file)
    {
        RDShaderSpirV spirv = file.GetSpirV();
        if (!string.IsNullOrEmpty(spirv.CompileErrorCompute))
        {
            GD.PushError($"paint pass: {file.ResourcePath}: {spirv.CompileErrorCompute}");
            return default;
        }

        Rid shader = rd.ShaderCreateFromSpirV(spirv, file.ResourcePath);
        return new Stage(shader, rd.ComputePipelineCreate(shader));
    }

    private static Rid Texture(RenderSceneBuffersRD buffers, StringName name, Vector2I size)
    {
        if (!buffers.HasTexture(Context, name))
        {
            const uint usage = (uint)(RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit);
            buffers.CreateTexture(Context, name, RenderingDevice.DataFormat.R16G16B16A16Sfloat, usage,
                RenderingDevice.TextureSamples.Samples1, size, 1, 1, true, false);
        }

        return buffers.GetTexture(Context, name);
    }

    /// <summary>Builds every pass's uniform set for these textures (only when the buffers change: it allocates).</summary>
    private void BuildSets(RenderingDevice rd, Rid color, Rid depth, Rid half, Rid tensor, Rid tensorTemp, Rid painted, Rid stroked)
    {
        foreach (Rid set in new[] { _prepareSet, _tensorSet, _blurXSet, _blurYSet, _kuwaharaSet, _strokesSet, _compositeSet })
        {
            if (set.IsValid && rd.UniformSetIsValid(set))
            {
                rd.FreeRid(set);
            }
        }

        _prepareSet = Set(rd, _prepare, Sampled(1, _linear, color), Sampled(2, _nearest, depth), Image(3, half));
        _tensorSet = Set(rd, _tensor, Sampled(1, _nearest, half), Image(2, tensorTemp));
        _blurXSet = Set(rd, _tensorBlur, Sampled(1, _nearest, tensorTemp), Image(2, tensor));
        _blurYSet = Set(rd, _tensorBlur, Sampled(1, _nearest, tensor), Image(2, tensorTemp));
        _kuwaharaSet = Set(rd, _kuwahara, Sampled(1, _nearest, half), Sampled(2, _nearest, tensorTemp), Image(3, painted));
        _strokesSet = Set(rd, _strokes, Sampled(1, _linear, painted), Sampled(2, _linear, tensorTemp), Sampled(3, _repeat, _paper),
            Image(4, stroked));
        _compositeSet = Set(rd, _composite, Image(1, color), Sampled(2, _nearest, depth), Sampled(3, _nearest, half),
            Sampled(4, _linear, stroked), Sampled(5, _linear, tensorTemp), Sampled(6, _repeat, _paper));
        _forColor = color;
        _forDepth = depth;
        _forHalf = half;
    }

    private Rid Set(RenderingDevice rd, Stage stage, params RDUniform[] uniforms)
    {
        var all = new Array<RDUniform>();
        var ubo = new RDUniform { UniformType = RenderingDevice.UniformType.UniformBuffer, Binding = 0 };
        ubo.AddId(_ubo);
        all.Add(ubo);
        foreach (RDUniform uniform in uniforms)
        {
            all.Add(uniform);
        }

        return rd.UniformSetCreate(all, stage.Shader, 0);
    }

    private static RDUniform Image(int binding, Rid texture)
    {
        var uniform = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = binding };
        uniform.AddId(texture);
        return uniform;
    }

    private static RDUniform Sampled(int binding, Rid sampler, Rid texture)
    {
        var uniform = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = binding };
        uniform.AddId(sampler);
        uniform.AddId(texture);
        return uniform;
    }

    /// <summary>Runs one step; <paramref name="dx"/>, <paramref name="dy"/> is the blur's direction (its push constant), unused by the others.</summary>
    private static void Dispatch(RenderingDevice rd, Stage stage, Rid set, uint x, uint y, float dx = 0f, float dy = 0f)
    {
        long list = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(list, stage.Pipeline);
        rd.ComputeListBindUniformSet(list, set, 0);
        if (dx != 0f || dy != 0f)
        {
            Span<float> direction = stackalloc float[] { dx, dy, 0f, 0f };
            rd.ComputeListSetPushConstant(list, MemoryMarshal.AsBytes(direction), (uint)(direction.Length * sizeof(float)));
        }

        rd.ComputeListDispatch(list, x, y, 1);
        rd.ComputeListEnd();
    }

    /// <summary>Fills the Params block (paint_params.glslinc) for this frame. Allocates nothing.</summary>
    private void WriteParams(RenderingDevice rd, RenderSceneData scene, PaintStyle style, Vector2I size, Vector2I half)
    {
        Projection projection = scene.GetCamProjection();
        Transform3D camera = scene.GetCamTransform();
        Vector3 right = camera.Basis.X;
        Vector3 up = camera.Basis.Y;
        Vector3 forward = -camera.Basis.Z;
        // The projection is the device's (reverse Z, depth 0..1, y maybe flipped): view depth = B / (d + A).
        float depthA = projection.Z.Z;
        float depthB = projection.W.Z;
        float tanHalfFov = 1f / Mathf.Abs(projection.Y.Y);
        float aspect = Mathf.Abs(projection.Y.Y / projection.X.X);
        // How far ahead the play plane (z = 0) is: where the brush is smallest and detail is kept.
        float plane = Mathf.Abs(forward.Z) > 1e-3f ? -camera.Origin.Z / forward.Z : 22f;

        Span<float> p = _params;
        Put(p, 0, size.X, size.Y, half.X, half.Y);
        Put(p, 1, depthA, depthB, tanHalfFov, aspect);
        Put(p, 2, right.X, right.Y, right.Z, plane);
        Put(p, 3, up.X, up.Y, up.Z, 0f);
        Put(p, 4, forward.X, forward.Y, forward.Z, 0f);
        Put(p, 5, camera.Origin.X, camera.Origin.Y, camera.Origin.Z, 0f);
        Put(p, 6, style.RadiusNear, style.RadiusFar, style.Sharpness, style.Anisotropy);
        Put(p, 7, style.Mechanical, style.Strength, style.Detail, style.Wobble);
        Put(p, 8, style.Pooling, style.Granulation, style.GrainCell, style.Glyphs);
        Put(p, 9, style.Bands, style.BandStrength, style.PalettePull, style.StrokeGrowth);
        Put(p, 10, style.Density, style.StrokeSize, style.Strokes, style.StrokeJitter);
        for (int i = 0; i < PigmentCount; i++)
        {
            Vector4 pigment = _pigments[i];
            Put(p, 11 + i, pigment.X, pigment.Y, pigment.Z, pigment.W);
        }

        rd.BufferUpdate(_ubo, 0, ParamFloats * sizeof(float), MemoryMarshal.AsBytes(p));
    }

    private static void Put(Span<float> p, int vec4, float x, float y, float z, float w)
    {
        int i = vec4 * 4;
        p[i] = x;
        p[i + 1] = y;
        p[i + 2] = z;
        p[i + 3] = w;
    }
}
