using System;
using System.IO;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

namespace Ensoftener
{
    public enum SamplingFilter { Bilinear, Point }
    /// <summary>A more or less functional shader effect inherited from <see cref="CustomEffectBase"/>. You can modify its input count, constant buffer, its nodes,
    /// splittable data stream designed for passing vertices and the shader's semantics.</summary>
    /// <remarks>The effect consists of a vertex shader and a pixel shader. The vertex shader stage deforms the bitmap via a triangular mesh and the pixel shader stage
    /// can create additional color filters on top of the deformed triangles. Either can be turned off in the constructor, but disabling both may result in errors.
    /// <br/>The vertex shader is still WIP - 100% functionality isn't guaranteed.</remarks>
    [CustomEffect("A shader that inherits from PVShaderBase", "PVSB inheritors", "Ensoftener"), CustomEffectInput("Source")]
    public class PVShaderBase : CustomEffectBase, DrawTransform
    {
        Guid psGUID, vsGUID, vbGUID; public DrawInformation dInfo; public VertexBuffer vertexBuffer; public TransformGraph transformGraph;
        public bool vertexShader, pixelShader; string psPath, vsPath; public byte[] psFile, vsFile; public EffectContext effectContext;
        public virtual VertexBuffer GetVertexBuffer(VertexUsage vertexUsage)
        {
            //InitializeVertexBuffer(effectContext);
            // Updating geometry every time the effect is rendered can be costly, so it is 
            // recommended that vertex buffer remain static if possible (which it is in this sample effect).
            (DataStream, int) buffer = VsInBuffer;
            // The GUID is optional, and is provided here to register the geometry globally.
            // As mentioned above, this avoids duplication if multiple versions of the effect are created.
            InputElement[] ie = InputElements;
            VertexBuffer vb = new(effectContext, vbGUID, new(ie.Length, vertexUsage, buffer.Item1), new(vsFile, ie, buffer.Item2));
            buffer.Item1?.Dispose();
            return vb;
        }
        public string PixelShaderFilePath { get => psPath; set { psPath = value; psFile = File.ReadAllBytes(value); } }
        public string VertexShaderFilePath { get => vsPath; set { vsPath = value; vsFile = File.ReadAllBytes(value); } }
        public SamplingFilter ScaleDownSampling { get; set; } = SamplingFilter.Point;
        public SamplingFilter ScaleUpSampling { get; set; } = SamplingFilter.Bilinear;
        public SamplingFilter MipmapSampling { get; set; } = SamplingFilter.Point;
        public bool AnisotropicFiltering { get; set; } = true;
        //EffectContext _effectContext;
        /// <summary>Initializes a new custom effect.</summary>
        /// <param name="psGuid">The GUID of the effect. Each GUID has one pixel shader assigned to it,
        /// hence the <see cref="CloneablePixelShader"/>'s ability to have different pixel shaders.</param>
        /// <param name="vsGuid">The GUID of the vertex shader.</param>
        /// <param name="vbGuid">The GUID of the vertex buffer. The vertex buffer contains values that will be passed to the shader, not the shader itself.</param>
        /// <param name="usePS">Enable the pixel shader stage in this effect.</param>
        /// <param name="useVS">Enable the vertex shader stage in this effect.</param>
        public PVShaderBase(Guid? psGuid = null, Guid? vsGuid = null, Guid? vbGuid = null, bool usePS = true, bool useVS = false)
        { pixelShader = usePS; vertexShader = useVS; psGUID = psGuid ?? Guid.Empty; vsGUID = vsGuid ?? Guid.Empty; vbGUID = vbGuid ?? Guid.Empty; }
        [PropertyBinding(-1, "(0,0,0,0)", "(0,0,0,0)", "(0,0,0,0)")] public Vector4 BorderExpansion { get; set; } = new(0, 0, 0, 0);
        /// <summary>The amount of vertices processed by the vertex shader. The amount must be a multiple of 3, as every 3 vertices form a single face.</summary>
        public int VertexCount { get; set; } public int InputCount { get; set; } = 1;
        public override void Initialize(EffectContext eC, TransformGraph tg)
        {
            //WARNING : as soon as TransformGraph.SetSingleTransformNode is called it chain calls the SetDrawInformation via a callback.
            //Unfortunately this is too early because the code below within this method is used to populate stateful data needed by the SetDrawInformation call. 
            //transformGraph.SetSingleTransformNode(this);
            effectContext = eC;
            if (pixelShader) { PixelShaderFilePath = Global.ShaderFile; effectContext.LoadPixelShader(psGUID, psFile); }
            if (vertexShader)
            {
                VertexShaderFilePath = Global.VertexShaderFile; effectContext.LoadVertexShader(vsGUID, vsFile);
                vertexBuffer = effectContext.FindVertexBuffer(vbGUID);
                if (vertexBuffer == null) ReloadVertexBuffer(true);
            }
            PrepareForRender(ChangeType.Properties | ChangeType.Context); SetGraph(tg);
        }
        /// <summary>A stream of structs assigned to semantics in vertex shaders. Streams can be created off of arrays via
        /// <see cref="DataStream.Create{T}(T[], bool, bool, int, bool)"/> (set <b>canRead</b> and <b>canWrite</b> to true!).
        /// The extra int indicates the size of each struct.</summary>
        /// <remarks>A vertex buffer in vertex shaders works differently than a constant buffer in pixel shaders (if you're looking for an eqiuvalent, that's <b>b1</b>).
        /// Here, each vertex will get one struct in the array and recieve it via semantics in its main method. <see cref="InputElements"/> decides what parts
        /// of the struct will be assigned to each semantic.<br/><br/>While the struct can be literally anything, certain restrictions apply:
        /// <list type="number"><item>The struct must contain only value types (such as <see cref="Vector2"/>).</item>
        /// <item>All structs must be the same size, because the stream progresses by a fixed integer (second output) and would become offset.</item></list></remarks>
        public virtual (DataStream, int) VsInBuffer => (null, 0);
        /// <summary>Returns an array of semantics that will be assigned to a part of each struct from <see cref="VsInBuffer"/>. The offset of an element specifies
        /// the offset of a struct's s region that's assigned, in bytes. The format specifies what type will be assigned to the semantic.</summary>
        /// <remarks>For example, if <see cref="VsInBuffer"/> contains structs with 8 floats each and there's an
        /// <see cref="InputElement"/> named "ABC" with an offset of 16 and format of <see cref="SharpDX.DXGI.Format.R32G32B32_Float"/>,
        /// the vertex shader will recieve the struct's 5th, 6th and 7th float as a float3 under the ABC semantic.</remarks>
        public virtual InputElement[] InputElements => Array.Empty<InputElement>();
        public override void SetGraph(TransformGraph tg) { transformGraph?.Dispose(); transformGraph = tg; InputCount = tg.InputCount; tg.SetSingleTransformNode(this); }
        public Filter GetSampling => AnisotropicFiltering ? Filter.Anisotropic : ScaleDownSampling == SamplingFilter.Bilinear ?
            ScaleUpSampling == SamplingFilter.Bilinear ?
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumMagMipLinear : Filter.MinimumMagLinearMipPoint :
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumLinearMagPointMipLinear : Filter.MinimumLinearMagMipPoint :
            ScaleUpSampling == SamplingFilter.Bilinear ?
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumPointMagMipLinear : Filter.MinimumPointMagLinearMipPoint :
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumMagPointMipLinear : Filter.MinimumMagMipPoint;
        public void SetDrawInformation(DrawInformation drawInfo)
        {
            dInfo?.Dispose();
            dInfo = drawInfo;
            if (pixelShader)
            {
                dInfo.SetPixelShader(psGUID, PixelOptions.None);
                dInfo.SetOutputBuffer(BufferPrecision.PerChannel32Float, ChannelDepth.Four);
                dInfo.SetInputDescription(0, new InputDescription(GetSampling, 0));
            }
            if (vertexShader) dInfo.SetVertexProcessing(vertexBuffer, VertexOptions.UseDepthBuffer, null, new VertexRange(0, VertexCount), vsGUID);
        }
        /// <summary>Updates the vertex buffer according to <see cref="VsInBuffer"/>.</summary>
        /// <param name="sameShader">Reloads the same .cso file as before (otherwise uses <see cref="Global.VertexShaderFile"/>.</param>
        public void ReloadVertexBuffer(bool sameShader = true)
        {
            if (!sameShader) VertexShaderFilePath = Global.VertexShaderFile;
            vertexBuffer?.Dispose(); //vbGUID = Guid.NewGuid();
            vertexBuffer = GetVertexBuffer(VertexUsage.Static);
        }
        public virtual RawRectangle MapInvalidRect(int inputIndex, RawRectangle invalidInputRect) => invalidInputRect;
        public virtual RawRectangle MapInputRectanglesToOutputRectangle(RawRectangle[] inputRects, RawRectangle[] inputOpaqueSubRects, out RawRectangle outputOpaqueSubRect)
        { outputOpaqueSubRect = default; return inputRects[0]; }
        public virtual void MapOutputRectangleToInputRectangles(RawRectangle outputRect, RawRectangle[] inputRects)
        {
            for (int i = 0; i < inputRects.Length; i++) inputRects[i] = new(outputRect.Left - (int)BorderExpansion.X,
                outputRect.Top - (int)BorderExpansion.Y, outputRect.Right + (int)BorderExpansion.Z, outputRect.Bottom + (int)BorderExpansion.W);
        }
        public new virtual void Dispose() { base.Dispose(); dInfo?.Dispose(); transformGraph?.Dispose(); vertexBuffer?.Dispose(); }
        ~PVShaderBase() => Dispose();
    }
    /// <summary>A shader with no constant buffer, no inside effects and 1 texture input.
    /// Its GUID is different for every instance, which means it can load a different shader every time.</summary>
    public class CloneablePixelShader : PVShaderBase { public CloneablePixelShader() : base(Guid.NewGuid()) { } }
}