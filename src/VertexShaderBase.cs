using System;
using System.IO;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

namespace Ensoftener
{
    /// <summary>A more or less functional vertex shader effect inherited from <see cref="CustomEffectBase"/>. You can modify its input count, constant buffer, its nodes,
    /// splittable data stream designed for passing vertices and the shader's semantics. <br/>The shader is still WIP -
    /// passing texcoords to other pixel shaders is not supported and 100% functionality isn't guaranteed.</summary>
    [CustomEffect("A shader that inherits from VertexShaderBase", "VSB inheritors", "Ensoftener"), CustomEffectInput("Source")]
    public class VertexShaderBase : CustomEffectBase, DrawTransform
    {
        Guid shaderGUID, bufferGUID; public DrawInformation dInfo; VertexBuffer vertexBuffer; public TransformGraph transformGraph;
        //EffectContext _effectContext;
        /// <summary>Initializes a new custom effect.</summary>
        /// <param name="shaderGuid">The GUID of the vertex shader.</param>
        /// <param name="bufferGuid">The GUID of the vertex buffer. The vertex buffer contains values that will be passed to the shader, not the shader itself.</param>
        public VertexShaderBase(Guid shaderGuid, Guid bufferGuid) { shaderGUID = shaderGuid; bufferGUID = bufferGuid; }
        [PropertyBinding(-1, "(0,0,0,0)", "(0,0,0,0)", "(0,0,0,0)")] public Vector4 BorderExpansion { get; set; } = new(0, 0, 0, 0);
        public int VertexCount { get; set; } public int InputCount { get; set; } = 1;
        public override void Initialize(EffectContext effectContext, TransformGraph transformGraph)
        {
            //WARNING : as soon as TransformGraph.SetSingleTransformNode is called it chain calls the SetDrawInformation via a callback.
            //Unfortunately this is too early because the code below within this method is used to populate stateful data needed by the SetDrawInformation call. 
            //transformGraph.SetSingleTransformNode(this);
            byte[] vxS = File.ReadAllBytes(Global.ShaderFile); effectContext.LoadVertexShader(shaderGUID, vxS);
            vertexBuffer = effectContext.FindVertexBuffer(bufferGUID);
            if (vertexBuffer == null)
            {
                //InitializeVertexBuffer(effectContext);
                // Updating geometry every time the effect is rendered can be costly, so it is 
                // recommended that vertex buffer remain static if possible (which it is in this sample effect).
                var buffer = VsInBuffer;
                // The GUID is optional, and is provided here to register the geometry globally.
                // As mentioned above, this avoids duplication if multiple versions of the effect are created.
                InputElement[] ie = InputElements;
                vertexBuffer = new VertexBuffer(effectContext, bufferGUID, new(ie.Length, VertexUsage.Static, buffer.Item1), new(vxS, ie, buffer.Item2));
                buffer.Item1?.Dispose();
            }
            PrepareForRender(ChangeType.Properties | ChangeType.Context); SetGraph(transformGraph);
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
        public override void SetGraph(TransformGraph transformGraph)
        { this.transformGraph = transformGraph; InputCount = transformGraph.InputCount; transformGraph.SetSingleTransformNode(this); }
        public void SetDrawInformation(DrawInformation drawInfo)
        { dInfo = drawInfo; dInfo.SetVertexProcessing(vertexBuffer, VertexOptions.UseDepthBuffer, null, new VertexRange(0, VertexCount), shaderGUID); }
        public virtual RawRectangle MapInvalidRect(int inputIndex, RawRectangle invalidInputRect) => invalidInputRect;
        public virtual RawRectangle MapInputRectanglesToOutputRectangle(RawRectangle[] inputRects, RawRectangle[] inputOpaqueSubRects, out RawRectangle outputOpaqueSubRect)
        { outputOpaqueSubRect = default; return inputRects[0]; }
        public virtual void MapOutputRectangleToInputRectangles(RawRectangle outputRect, RawRectangle[] inputRects)
        {
            for (int i = 0; i < inputRects.Length; i++) inputRects[i] = new(outputRect.Left - (int)BorderExpansion.X,
                outputRect.Top - (int)BorderExpansion.Y, outputRect.Right + (int)BorderExpansion.Z, outputRect.Bottom + (int)BorderExpansion.W);
        }
    }
}