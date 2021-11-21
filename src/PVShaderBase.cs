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
        Guid psGUID, vsGUID, vbGUID; public DrawInformation dInfo; VertexBuffer vertexBuffer; public TransformGraph transformGraph;
        public bool vertexShader, pixelShader;
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
        public int VertexCount { get; set; } public int InputCount { get; set; } = 1;
        public override void Initialize(EffectContext effectContext, TransformGraph tg)
        {
            //WARNING : as soon as TransformGraph.SetSingleTransformNode is called it chain calls the SetDrawInformation via a callback.
            //Unfortunately this is too early because the code below within this method is used to populate stateful data needed by the SetDrawInformation call. 
            //transformGraph.SetSingleTransformNode(this);
            if (pixelShader) effectContext.LoadPixelShader(psGUID, File.ReadAllBytes(Global.ShaderFile));
            if (vertexShader)
            {
                byte[] vxS = File.ReadAllBytes(Global.VertexShaderFile); effectContext.LoadVertexShader(vsGUID, vxS);
                vertexBuffer = effectContext.FindVertexBuffer(vbGUID);
                if (vertexBuffer == null)
                {
                    //InitializeVertexBuffer(effectContext);
                    // Updating geometry every time the effect is rendered can be costly, so it is 
                    // recommended that vertex buffer remain static if possible (which it is in this sample effect).
                    var buffer = VsInBuffer;
                    // The GUID is optional, and is provided here to register the geometry globally.
                    // As mentioned above, this avoids duplication if multiple versions of the effect are created.
                    InputElement[] ie = InputElements;
                    vertexBuffer = new VertexBuffer(effectContext, vbGUID, new(ie.Length, VertexUsage.Static, buffer.Item1), new(vxS, ie, buffer.Item2));
                    buffer.Item1?.Dispose();
                }
            }
            PrepareForRender(ChangeType.Properties | ChangeType.Context); SetGraph(tg);
        }
        /// <summary>The stream of values passed to what is <b>VsIn</b> in vertex shaders. Streams can be created off of arrays via
        /// <see cref="DataStream.Create{T}(T[], bool, bool, int, bool)"/> (set <b>canRead</b> and <b>canWrite</b> to true!).
        /// The extra int indicates the size of each value.</summary>
        /// <remarks>A vertex buffer in vertex shaders works differently than a constant buffer in pixel shaders (if you're looking for an eqiuvalent, that's <b>b1</b>).
        /// Here, the stream will be split into elements using the second int as a "stride". For example, you pass an array of <see cref="Vector2"/>
        /// and <c>sizeof(Vector2)</c> as the integer. Direct2D will then split the stream using that integer and pass each value to the shader individually.</remarks>
        public virtual (DataStream, int) VsInBuffer => (null, 0);
        /// <summary>Returns an array of semantics that will then appear in the <b>VsIn</b> struct.</summary>
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
                dInfo.SetInputDescription(0, new InputDescription(GetSampling, 1));
            }
            if (vertexShader) dInfo.SetVertexProcessing(vertexBuffer, VertexOptions.UseDepthBuffer, null, new VertexRange(0, VertexCount), vsGUID);
        }
        public virtual RawRectangle MapInvalidRect(int inputIndex, RawRectangle invalidInputRect) => invalidInputRect;
        public virtual RawRectangle MapInputRectanglesToOutputRectangle(RawRectangle[] inputRects, RawRectangle[] inputOpaqueSubRects, out RawRectangle outputOpaqueSubRect)
        { outputOpaqueSubRect = default; return inputRects[0]; }
        public virtual void MapOutputRectangleToInputRectangles(RawRectangle outputRect, RawRectangle[] inputRects)
        {
            for (int i = 0; i < inputRects.Length; i++) inputRects[i] = new(outputRect.Left - (int)BorderExpansion.X,
                outputRect.Top - (int)BorderExpansion.Y, outputRect.Right + (int)BorderExpansion.Z, outputRect.Bottom + (int)BorderExpansion.W);
        }
    }
    /// <summary>A shader with no constant buffer, no inside effects and 1 texture input.
    /// Its GUID is different for every instance, which means it can load a different shader every time.</summary>
    public class CloneablePixelShader : PVShaderBase { public CloneablePixelShader() : base(Guid.NewGuid()) { } }
}