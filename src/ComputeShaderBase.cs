using System;
using System.IO;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

namespace Ensoftener
{
    /// <summary>A compute shader effect inherited from <seealso cref="CustomEffectBase"/>. You can modify its input count, constant buffer and its nodes.</summary>
    /// <remarks><b>Requires EnsoftenerCpp32.dll or EnsoftenerCpp64.dll in to be put in your program directory.</b></remarks>
    [CustomEffect("A shader that inherits from ComputeShaderBase", "CSB inheritors", "Ensoftener"), CustomEffectInput("Source")]
    public class ComputeShaderBase : CustomEffectBase, ComputeTransform
    {
        public Guid GUID; public int borderExpansion = 0; public ComputeInformation cInfo; public TransformGraph transformGraph;
        public SamplingFilter ScaleDownSampling { get; set; } = SamplingFilter.Point;
        public SamplingFilter ScaleUpSampling { get; set; } = SamplingFilter.Bilinear;
        public SamplingFilter MipmapSampling { get; set; } = SamplingFilter.Point;
        public bool AnisotropicFiltering { get; set; } = true;
        [PropertyBinding(-1, "(0,0,0,0)", "(0,0,0,0)", "(0,0,0,0)")] public Vector4 BorderExpansion { get; set; } = new(0, 0, 0, 0);
        /// <summary>Initializes a new custom effect.</summary>
        /// <param name="guid">The GUID of the effect. Each GUID has one compute shader assigned to it.</param>
        public ComputeShaderBase(Guid guid) { GUID = guid; }
        public override void Initialize(EffectContext effectContext, TransformGraph transformGraph)
        { effectContext.LoadComputeShader(GUID, File.ReadAllBytes(Global.ShaderFile)); SetGraph(transformGraph); }
        public override void SetGraph(TransformGraph transformGraph)
        { this.transformGraph = transformGraph; InputCount = transformGraph.InputCount; transformGraph.SetSingleTransformNode(this); }
        public void SetComputeInformation(ComputeInformation computeInfo)
        {
            cInfo = computeInfo; cInfo.SetComputeShader(GUID);
            cInfo.SetOutputBuffer(BufferPrecision.PerChannel32Float, ChannelDepth.Four);
            cInfo.SetInputDescription(0, new InputDescription(GetSampling, 1));
        }
        public virtual RawRectangle MapInvalidRect(int inputIndex, RawRectangle invalidInputRect) => invalidInputRect;
        public virtual RawRectangle MapInputRectanglesToOutputRectangle(RawRectangle[] inputRects, RawRectangle[] inputOpaqueSubRects, out RawRectangle outputOpaqueSubRect)
        { outputOpaqueSubRect = default; return inputRects[0]; }
        public virtual void MapOutputRectangleToInputRectangles(RawRectangle outputRect, RawRectangle[] inputRects)
        {
            for (int i = 0; i < inputRects.Length; ++i) inputRects[i] = new(outputRect.Left - (int)BorderExpansion.X,
                outputRect.Top - (int)BorderExpansion.Y, outputRect.Right + (int)BorderExpansion.Z, outputRect.Bottom + (int)BorderExpansion.W);
        }
        /// <summary>The amount of threads that will run on the GPU. Maximum is [X, Y, 1] (where X * Y = 768) on Shader Model 4
        /// and [X, Y, 64] (where X * Y * Z = 1024) on Shader Model 5 and newer.</summary>
        /// <param name="outputRect">The image rectangle.</param>
        /// <remarks>Imagine the GPU as a 3D cube formed by smaller processors, where by <see cref="RawInt3(int, int, int)"/>
        /// you split the cube into X, Y and Z amount of threads. For example, you have a 200x200 texture and you split the shader into [10, 10, 1] threads.
        /// In this case, each thread can process a 20x20 image cut from the original texture.</remarks>
        public virtual RawInt3 CalculateThreadgroups(RawRectangle outputRect) => new(1, 1, 1);
        public int InputCount { get; set; }
        public Filter GetSampling => AnisotropicFiltering ? Filter.Anisotropic : ScaleDownSampling == SamplingFilter.Bilinear ?
            ScaleUpSampling == SamplingFilter.Bilinear ?
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumMagMipLinear : Filter.MinimumMagLinearMipPoint :
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumLinearMagPointMipLinear : Filter.MinimumLinearMagMipPoint :
            ScaleUpSampling == SamplingFilter.Bilinear ?
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumPointMagMipLinear : Filter.MinimumPointMagLinearMipPoint :
                MipmapSampling == SamplingFilter.Bilinear ? Filter.MinimumMagPointMipLinear : Filter.MinimumMagMipPoint;
    }
}
