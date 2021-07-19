using System;
using System.Drawing.Imaging;
using System.Collections.Generic;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct2D1;
using DWFactory = SharpDX.DirectWrite.Factory;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using DriverType = SharpDX.Direct3D.DriverType;
using WICFactory = SharpDX.WIC.ImagingFactory;
using DeviceCreationFlags = SharpDX.Direct3D11.DeviceCreationFlags;

namespace Ensoftener
{
    /// <summary>The class containing everything necessary, from Direct2D components to new and useful DeviceContext methods.</summary>
    public static class Global
    {
        static SwapChainDescription1 dxgiScd; static BitmapProperties1 d2b;
        public static Format PixelFormat { get; } = Format.B8G8R8A8_UNorm;
        static readonly List<int> resizables = new() { 0 };
        public static SwapChainDescription1 SwapChainDescription { get => dxgiScd; private set => dxgiScd = value; }
        public static SharpDX.Windows.RenderForm Form { get; set; }
        /// <summary>The class used for registering your custom effects.</summary><remarks>Based on D2DDevice.</remarks>
        public static SharpDX.Direct2D1.Factory2 D2DFactory { get; private set; }
        /// <remarks>Based on D3DDevice.</remarks>
        public static SharpDX.Direct2D1.Device D2DDevice { get; private set; }
        /// <remarks>Based either on a DXGIFactory or can be freely created.</remarks>
        public static SharpDX.Direct3D11.Device D3DDevice { get; private set; }
        /// <summary>This is where it all starts. The factories from which everything else is derived.</summary>
        public static List<SharpDX.DXGI.Factory2> DXGIFactories { get; } = new();
        /// <remarks>Based on D3DDevice and a DXGIFactory.</remarks>
        public static List<SwapChain1> SwapChains { get; } = new();
        /// <summary>The DeviceContexts used for rendering everything, probably the most used class of them all.</summary><remarks>Based on D2DDevice.</remarks>
        public static List<DeviceContext> DCs { get; } = new();
        /// <summary>The render target of the DeviceContext.</summary><remarks>Based on a DeviceContext and a SwapChain.</remarks>
        public static List<Bitmap1> RenderTargets { get; } = new();
        /// <summary>The class passed as a parameter when rendering text.</summary><remarks>Can be freely created.</remarks>
        public static DWFactory DWriteFactory { get; private set; }
        /// <summary>The class used for creating SVG's.</summary><remarks>Can be freely created.</remarks>
        public static WICFactory WICFactory { get; } = new();
        public static List<ShadedEffectBase> RegisteredEffects { get; private set; } = new();
        /// <summary>The index of the setup that will serve as the output and present its contents to the screen.</summary>
        public static int OutputDevice { get; private set; }
        /// <summary>Creates all the stuff needed for a basic SharpDX setup. The first device (0th) will be set as output.</summary>
        /// <param name="DeviceDpi">Set this to your form's DeviceDpi property.</param>
        /// <param name="parallelDevices">The amount of parallel Direct2D setups to create (for multirendering).
        /// All components will be accessible from their lists. Cannot be less than 1.</param>
        /// <param name="sizes">The default size of all device contexts, except for the outputting one. By default it's the window size.</param>
        public static void Initialize(int parallelDevices = 1, Size2? sizes = null)
        {
            if (parallelDevices < 1) parallelDevices = 1;
            D3DDevice = new(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.SingleThreaded);
            D2DDevice = new(D3DDevice.QueryInterface<SharpDX.DXGI.Device1>());
            D2DFactory = D2DDevice.Factory.QueryInterface<SharpDX.Direct2D1.Factory2>();
            SwapChainDescription = new()
            {
                Width = Form.ClientSize.Width, Height = Form.ClientSize.Height, Format = PixelFormat,
                SampleDescription = new(1, 0), Scaling = Scaling.Stretch, BufferCount = 1,
                Usage = Usage.RenderTargetOutput | Usage.ShaderInput | Usage.BackBuffer,
                SwapEffect = SwapEffect.Sequential, Flags = SwapChainFlags.AllowModeSwitch
            };
            //d2b = new(new(PixelFormat, AlphaMode.Premultiplied));
            Form.ResizeEnd += Resize; Input.Initialize();
            DWriteFactory = new(SharpDX.DirectWrite.FactoryType.Isolated);
            AddSetups(parallelDevices, sizes);
        }
        /// <summary> Creates a SharpDX Bitmap off of an image file.</summary>
        public static Bitmap LoadBitmapFromFile(this DeviceContext deviceContext, string filename)
        {
            using var bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(filename);
            var bmp2 = deviceContext.ConvertGDIToD2DBitmap(bmp); return bmp2;
        }
        /// <summary> Creates a SharpDX Bitmap off of a GDI+ bitmap.</summary>
        public static Bitmap ConvertGDIToD2DBitmap(this DeviceContext deviceContext, System.Drawing.Bitmap bmp)
        {
            BitmapData bmpData = bmp.LockBits(new(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using DataStream stream = new(bmpData.Scan0, bmpData.Stride * bmpData.Height, true, false);
            Bitmap result = new(deviceContext, new(bmp.Width, bmp.Height), stream, bmpData.Stride, new(new(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)));
            bmp.UnlockBits(bmpData); return result;
        }
        /// <summary> Creates a GDI+ bitmap off of a SharpDX Bitmap. (very slow)</summary>
        public static System.Drawing.Bitmap ToGDIBitmap(this Bitmap1 bmp)
        {
            System.Drawing.Bitmap bmp2 = new((int)bmp.Size.Width, (int)bmp.Size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bmpData = bmp2.LockBits(new(0, 0, bmp2.Width, bmp2.Height), ImageLockMode.WriteOnly, bmp2.PixelFormat);
            var rect = bmp.Map(MapOptions.Read);
            unsafe
            {
                byte* pointer = (byte*)bmpData.Scan0, pointer2;
                for (int i = 0; i < bmp2.Height; ++i)
                {
                    pointer2 = (byte*)rect.DataPointer + rect.Pitch * i;
                    for (int j = 0; j < bmp2.Width; ++j)
                    {
                        pointer[0] = pointer2[2]; pointer[2] = pointer2[0]; //rb -> br
                        pointer[1] = pointer2[1]; pointer[3] = pointer2[3]; //ga
                        pointer += 4; pointer2 += 4;
                    };
                }
            }
            /*unsafe
            {
                byte* pointer;
                for (int i = 0; i < bmp2.Height; i++) for (int j = 0; j < bmp2.Width; j++)
                    {
                        pointer = (byte*)rect.DataPointer + rect.Pitch * i + j * 4;
                        pointer[0] ^= pointer[2]; pointer[2] ^= pointer[0]; pointer[0] ^= pointer[2]; //rgb -> bgr
                    }
            }
            DataStream stream = new(bmpData.Scan0, bmp2.Height * bmp2.Width * 4, false, true);
            var data = new byte[rect.Pitch * bmpData.Height * 4];
            for (int i = 0; i < bmp2.Height; i++) stream.WriteRange(rect.DataPointer + rect.Pitch * i, bmpData.Stride);
            stream.Dispose();*/
            bmp.Unmap(); bmp2.UnlockBits(bmpData); return bmp2;
        }
        /// <summary>Copies the device context's render target for further CPU processing (such as saving to a file).</summary>
        /// <returns>The render target's bitmap. Requires a newer version of DirectX and cannot be read by the GPU.</returns>
        public static Bitmap1 GetScreenCPURead(this DeviceContext d2dc, Rectangle? source = null, Point? destination = null)
        {
            Rectangle sourceRect = source ?? new(0, 0, Form.ClientSize.Width, Form.ClientSize.Height);
            Bitmap1 result = new(d2dc, new Size2(sourceRect.Width, sourceRect.Height), new(new(PixelFormat, AlphaMode.Premultiplied))
            { BitmapOptions = BitmapOptions.CannotDraw | BitmapOptions.CpuRead });
            result.CopyFromRenderTarget(d2dc, destination ?? new(0, 0), sourceRect); return result;
        }
        /// <summary>Copies the device context's render target for further GPU processing (such as shaders).</summary>
        /// <returns>The render target's bitmap. The bitmap cannot be read by the CPU.</returns>
        public static Bitmap GetScreenGPURead(this DeviceContext d2dc, Rectangle? source = null, Point? destination = null)
        {
            Rectangle sourceRect = source ?? new(0, 0, Form.ClientSize.Width, Form.ClientSize.Height);
            Bitmap result = new(d2dc, new(sourceRect.Width, sourceRect.Height), new(new(PixelFormat, AlphaMode.Premultiplied)));
            result.CopyFromRenderTarget(d2dc, destination ?? new(0, 0), sourceRect); return result;
        }
        /// <summary>Batch renders an array of effects applied go the entire screen.</summary>
        /// <param name="transform">An additional transorm to apply at the end.</param>
        /// <param name="uvPrecision">When off, the screen size will round to a power of 2 and any shader using texture coordinates will become practically unusable.
        /// <br/>When on, the coordinates will display correctly but the performance of shaders will decrease.</param>
        /// <param name="effects">The array of effects to render.</param>
        public static DeviceContext RenderScreenShaders(this DeviceContext d2dc, EffectTransformer transform = null, bool uvPrecision = false, params Effect[] effects)
        {
            Bitmap result = d2dc.GetScreenGPURead(); effects[0].SetInput(0, result, true);
            if (uvPrecision)
            {
                using Bitmap background = result;
                for (int i = 0; i < effects.Length; ++i)
                {
                    result = d2dc.GetScreenGPURead(); effects[i].SetInput(0, result, true);
                    d2dc.DrawImage(effects[i]); result.Dispose();
                }
                if (transform != null)
                {
                    transform.Handle.SetInputEffect(0, effects[^1]);
                    d2dc.DrawBitmap(background, 1, BitmapInterpolationMode.NearestNeighbor);
                    d2dc.DrawEffectTransform(transform);
                }
            }
            else
            {
                for (int i = 1; i < effects.Length; ++i) effects[i].SetInputEffect(0, effects[i - 1], true);
                if (transform != null) { transform?.Handle.SetInputEffect(0, effects[^1]); d2dc.DrawEffectTransform(transform); } 
                else d2dc.DrawImage(effects[^1]); result?.Dispose();
            }
            return d2dc;
        }
        public static DeviceContext DrawEffectTransform(this DeviceContext d2dc, EffectTransformer transform) { d2dc.DrawImage(transform.Handle); return d2dc; }
        public static DeviceContext5 DrawSvgDocument(this DeviceContext5 d2dc, SvgImage svg) { d2dc.DrawSvgDocument(svg.Document); return d2dc; }
        public static void Resize(object sender = null, EventArgs e = null)
        { for (int i = 0; i < DCs.Count; ++i) if (resizables.Contains(i)) ResizeSetup(i, new(Form.ClientSize.Width, Form.ClientSize.Height)); }
        /// <summary>Adds a new amount of parallel rendering setups.</summary>
        /// <param name="amount">The amount of setups to create.</param>
        /// <param name="Sizes">The default size of the device contexts about to be created. By default it's the window size.</param>
        public static void AddSetups(int amount, Size2? sizes)
        {
            for (int i = 0; i < amount; ++i)
            {
                dxgiScd.Width = resizables.Contains(i) ? Form.ClientSize.Width : sizes?.Width ?? Form.ClientSize.Width;
                dxgiScd.Height = resizables.Contains(i) ? Form.ClientSize.Height : sizes?.Height ?? Form.ClientSize.Height;
                DXGIFactories.Add(new());
                SwapChains.Add(new(DXGIFactories[^1], D3DDevice, Form.Handle, ref dxgiScd));
                DCs.Add(new(D2DDevice.QueryInterface<Device5>(), DeviceContextOptions.EnableMultithreadedOptimizations)
                { RenderingControls = new() { BufferPrecision = BufferPrecision.PerChannel32Float, TileSize = resizables.Contains(i) ?
                new(Form.ClientSize.Width, Form.ClientSize.Height) : sizes ?? new(Form.ClientSize.Width, Form.ClientSize.Height) } });
                RenderTargets.Add(new(DCs[^1], SwapChains[^1].GetBackBuffer<Surface>(0), d2b));
            }
        }
        /// <summary>Resizes a setup.</summary>
        /// <param name="index">The index of the setup in the list.</param>
        /// <param name="size">New rendering size.</param>
        public static void ResizeSetup(int index, Size2 size)
        {
            dxgiScd.Width = size.Width; dxgiScd.Height = size.Height;
            DCs[index].RenderingControls = new() { BufferPrecision = BufferPrecision.PerChannel32Float, TileSize = size };
            SwapChains[index].Dispose(); SwapChains[index] = new(DXGIFactories[index], D3DDevice, Form.Handle, ref dxgiScd);
            //SwapChain.ResizeBuffers(dxgiScd.BufferCount, Form.ClientSize.Width, Form.ClientSize.Height, PixelFormat, dxgiScd.Flags);
            RenderTargets[index].Dispose(); RenderTargets[index] = new(DCs[index], SwapChains[index].GetBackBuffer<Surface>(0), d2b);
        }
        /// <summary>Put this at the beginning of your render method.</summary>
        public static void BeginRender() { for (int i = 0; i < DCs.Count; ++i) DCs[i].Target = RenderTargets[i]; }
        /// <summary>Put this at the end of your render method.</summary>
        public static void EndRender() { SwapChains[OutputDevice].Present(1, PresentFlags.None, new()); Input.Update(); }
        /// <summary>Disposes of a setup and removes it from the lists.</summary>
        public static void RemoveSetup(int index)
        {
            RenderTargets[index].Dispose(); DCs[index].Dispose(); SwapChains[index].Dispose(); DXGIFactories[index].Dispose();
            RenderTargets.RemoveAt(index);  DCs.RemoveAt(index);  SwapChains.RemoveAt(index);  DXGIFactories.RemoveAt(index);
            OutputDevice = Math.Min(RenderTargets.Count, OutputDevice);
            if (OutputDevice >= DCs.Count || OutputDevice == index) { OutputDevice = DCs.Count; Resize(); }
            if (OutputDevice >= index) OutputDevice--;
        }
        /// <summary>Set this context to be resizable with the screen.</summary>
        public static void ResizeWithScreen(this DeviceContext d2dc, bool set)
        {
            int index = DCs.IndexOf(d2dc);
            if (set) { if (!resizables.Contains(index)) resizables.Add(index); } else resizables.Remove(index);
        }
        //static Size2 Tileify(Size2 size) => new((int)Math.Pow(2, Math.Ceiling(Math.Log2(size.Width))), (int)Math.Pow(2, Math.Ceiling(Math.Log2(size.Height))));
    }
}