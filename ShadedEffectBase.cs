using System;
using System.IO;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

namespace Ensoftener
{
    public enum SamplingFilter { Bilinear, Point }
    /// <summary>
    /// A prefabricated effect inherited from <seealso cref="CustomEffectBase"/>. You can modify its input count, constant buffer and its nodes.
    /// <br/>You can even nest more effects inside it, which then can work with 32-bit float color depth.
    /// </summary>
    [CustomEffect("A shader that inherits from ShadedEffectBase", "SEB inheritors", "Ensoftener"), CustomEffectInput("Source")]
    public class ShadedEffectBase : CustomEffectBase, DrawTransform
    {
        public Guid GUID; public int borderExpansion = 0; public DrawInformation dInfo; public TransformGraph transformGraph;
        public SamplingFilter ScaleDownSampling { get; set; } = SamplingFilter.Point;
        public SamplingFilter ScaleUpSampling { get; set; } = SamplingFilter.Bilinear;
        public SamplingFilter MipmapSampling { get; set; } = SamplingFilter.Point;
        public bool AnisotropicFiltering { get; set; } = true;
        [PropertyBinding(-1, "(0, 0, 0, 0)", "(0, 0, 0, 0)", "(0, 0, 0, 0)", Type = PropertyType.Vector4)]
        public Vector4 BorderExpansion { get; set; } = new(0, 0, 0, 0);
        /// <summary>Initializes a new custom effect.</summary>
        /// <param name="guid">The GUID of the effect. Each GUID has one pixel shader assigned to it,
        /// hence the <see cref="CloneablePixelShader"/>'s ability to have different pixel shaders.</param>
        public ShadedEffectBase(Guid guid) { GUID = guid; Global.RegisteredEffects.Add(this); }
        public override void Initialize(EffectContext effectContext, TransformGraph transformGraph)
        { effectContext.LoadPixelShader(GUID, File.ReadAllBytes(Global.ShaderFile)); SetGraph(transformGraph); }
        public override void PrepareForRender(ChangeType changeType) => UpdateConstants();
        public override void SetGraph(TransformGraph transformGraph)
        { this.transformGraph = transformGraph; InputCount = transformGraph.InputCount; transformGraph.SetSingleTransformNode(this); }
        public void SetDrawInformation(DrawInformation drawInfo)
        {
            dInfo = drawInfo; dInfo.SetPixelShader(GUID, PixelOptions.None); dInfo.SetOutputBuffer(BufferPrecision.PerChannel32Float, ChannelDepth.Four);
            dInfo.SetInputDescription(0, new InputDescription(GetSampling, 1));
        }
        public RawRectangle MapInvalidRect(int inputIndex, RawRectangle invalidInputRect) => invalidInputRect;
        public RawRectangle MapInputRectanglesToOutputRectangle(RawRectangle[] inputRects, RawRectangle[] inputOpaqueSubRects, out RawRectangle outputOpaqueSubRect)
        { outputOpaqueSubRect = default(Rectangle); return inputRects[0]; }
        public void MapOutputRectangleToInputRectangles(RawRectangle outputRect, RawRectangle[] inputRects)
        {
            for (int i = 0; i < inputRects.Length; i++) inputRects[i] = new(outputRect.Left - (int)BorderExpansion.X,
                outputRect.Top - (int)BorderExpansion.Y, outputRect.Right + (int)BorderExpansion.Z, outputRect.Bottom + (int)BorderExpansion.W);
        }
        public Filter GetSampling => AnisotropicFiltering ? Filter.Anisotropic : ScaleDownSampling == SamplingFilter.Bilinear ?
            ScaleUpSampling == SamplingFilter.Bilinear ?
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumMagMipLinear : Filter.MinimumMagLinearMipPoint :
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumLinearMagPointMipLinear : Filter.MinimumLinearMagMipPoint :
            ScaleUpSampling == SamplingFilter.Bilinear ?
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumPointMagMipLinear : Filter.MinimumPointMagLinearMipPoint :
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumMagPointMipLinear : Filter.MinimumMagMipPoint;
        public int InputCount { get; set; }
        public virtual void UpdateConstants() { }
    }
    /// <summary>A shader with no constant buffer, no inside effects and 1 texture input.
    /// Its GUID is different for every instance, which means it can load a different shader every time.</summary>
    public class CloneablePixelShader : ShadedEffectBase { public CloneablePixelShader() : base(Guid.NewGuid()) { } }
    public class EffectTransformer
    {
        float x, y, angle, scaleX = 1, scaleY = 1;
        public SharpDX.Direct2D1.Effects.AffineTransform2D Handle { get; private set; }
        public float X { get => x; set { x = value; HandleSetM(); } }
        public float Y { get => y; set { y = value; HandleSetM(); } }
        public float ScaleX { get => scaleX; set { scaleX = value; HandleSetM(); } }
        public float ScaleY { get => scaleY; set { scaleY = value; HandleSetM(); } }
        public float Angle { get => angle; set { angle = value; HandleSetM(); } }
        private void HandleSetM() => Handle.TransformMatrix = Matrix3x2.Transformation(scaleX, scaleY, angle, x, y);
        public EffectTransformer(DeviceContext d2dc, Effect originalEffect)
        { Handle = new(d2dc); if (originalEffect != null) Handle.SetInputEffect(0, originalEffect, true); }
    }
}
