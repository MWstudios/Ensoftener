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
        static SwapChainDescription1 dxgiScd; static BitmapProperties1 d2bFinal,// = new(new(PixelFormat, AlphaMode.Premultiplied));
            d2bI = new(new(Format.R32G32B32A32_Float, AlphaMode.Premultiplied)) { BitmapOptions = BitmapOptions.Target };
        static readonly SharpDX.DXGI.Factory2 FinalFactory = new(); static Bitmap1 FinalTarget;
        /// <summary>The final device context that renders on screen, and the only one that uses byte color depth.
        /// Updates after <b><see cref="EndRender"/></b> is called.</summary>
        /// <remarks>If you want to take a screenshot of the screen and convert it into a GDI bitmap, use <b><see cref="GetScreenCPURead"/></b> on this context
        /// for the fastest performance. If you were to convert any context from <b><see cref="DCs"/></b> to GDI, it would take much longer, as the other contexts use
        /// <b><see cref="Format.R32G32B32A32_Float"/></b>. In that case, the library would cast every individual float from the screen to a byte,
        /// which takes about half a second. Since the final context's pixel format is <b><see cref="Format.B8G8R8A8_UNorm"/></b>, it's the easiest and fastest to copy.</remarks>
        public static DeviceContext FinalDC { get; private set; }
        static readonly List<int> resizables = new() { 0 };
        public static Format PixelFormat { get => d2bI.PixelFormat.Format; set => d2bI.PixelFormat.Format = value; }
        /// <summary><b><see cref="SwapChain"/></b>'s creation specs (in case you need them).</summary>
        public static SwapChainDescription1 SwapChainDescription => dxgiScd;
        /// <summary>A .cso file that will be loaded by every effect that's created from now on. One shader class can have different pixel shaders for every instance.</summary>
        public static string ShaderFile { get; set; }
        public static SharpDX.Windows.RenderForm Form { get; set; }
        /// <summary>The class used for registering your custom effects.</summary><remarks>Based on D2DDevice.</remarks>
        public static SharpDX.Direct2D1.Factory2 D2DFactory { get; private set; }
        /// <remarks>Based on D3DDevice.</remarks>
        public static SharpDX.Direct2D1.Device D2DDevice { get; private set; }
        /// <remarks>Based either on a DXGIFactory or can be freely created.</remarks>
        public static SharpDX.Direct3D11.Device D3DDevice { get; private set; }
        /// <remarks>Based on D3DDevice and a DXGIFactory.</remarks>
        public static SwapChain1 SwapChain { get; private set; }
        /// <summary>The DeviceContexts used for rendering everything, probably the most used class of them all.</summary><remarks>Based on D2DDevice.</remarks>
        public static List<DeviceContext> DCs { get; } = new();
        /// <summary>The render targets of the DeviceContexts. All use 32-bit float color depth.</summary><remarks>Based on DeviceContext.</remarks>
        public static List<Bitmap1> RenderTargets { get; } = new();
        /// <summary>The class passed as a parameter when rendering text.</summary><remarks>Can be freely created.</remarks>
        public static DWFactory DWriteFactory { get; private set; }
        /// <summary>The class used for creating SVG's.</summary><remarks>Can be freely created.</remarks>
        public static WICFactory WICFactory { get; } = new();
        /// <summary>A list of all effects that were created.</summary> 
        public static List<ShadedEffectBase> RegisteredEffects { get; } = new();
        /// <summary>The index of the setup that will serve as the output and present its contents to the screen.</summary>
        public static int OutputDevice { get; private set; }
        /// <summary>Creates all the stuff needed for a basic SharpDX setup. The first device (0th) will be set as output.</summary>
        /// <param name="parallelDevices">The amount of parallel Direct2D setups to create (for multirendering).
        /// All components will be accessible from their lists. Cannot be less than 1.</param>
        /// <param name="sizes">The default size of all device contexts, except for the outputting one. By default it's the window size.</param>
        public static void Initialize(int parallelDevices = 1, Size2? sizes = null)
        {
            if (parallelDevices < 1) parallelDevices = 1;
            D3DDevice = new(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            D2DDevice = new(D3DDevice.QueryInterface<SharpDX.DXGI.Device1>());
            D2DFactory = D2DDevice.Factory.QueryInterface<SharpDX.Direct2D1.Factory2>(); D2DFactory.RegisterEffect<CloneablePixelShader>();
            dxgiScd = new()
            {
                Width = Form.ClientSize.Width, Height = Form.ClientSize.Height, Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new(1, 0), Scaling = Scaling.Stretch, BufferCount = 1,
                Usage = Usage.RenderTargetOutput | Usage.ShaderInput | Usage.BackBuffer,
                SwapEffect = SwapEffect.Sequential, Flags = SwapChainFlags.AllowModeSwitch
            };
            Form.ResizeEnd += Resize; Input.Initialize();
            DWriteFactory = new(SharpDX.DirectWrite.FactoryType.Isolated);
            SwapChain = new(FinalFactory, D3DDevice, Form.Handle, ref dxgiScd);
            FinalDC = new(D2DDevice.QueryInterface<Device5>(), DeviceContextOptions.EnableMultithreadedOptimizations)
            { RenderingControls = new() { TileSize = new(Form.ClientSize.Width, Form.ClientSize.Height) } };
            FinalTarget = new(FinalDC, SwapChain.GetBackBuffer<Surface>(0), d2bFinal);
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
        /// <summary> Creates a GDI+ bitmap off of a SharpDX Bitmap. Very slow if using float colors.</summary>
        public static unsafe System.Drawing.Bitmap ToGDIBitmap(this Bitmap1 bmp)
        {
            System.Drawing.Bitmap bmp2 = new((int)bmp.Size.Width, (int)bmp.Size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bmpData = bmp2.LockBits(new(0, 0, bmp2.Width, bmp2.Height), ImageLockMode.WriteOnly, bmp2.PixelFormat); var rect = bmp.Map(MapOptions.Read);
            switch (bmp.PixelFormat.Format)
            {
                case Format.R32G32B32A32_Float:
                    byte* dp = (byte*)bmpData.Scan0; float* sp;
                    for (int i = 0; i < bmp2.Height; ++i)
                    {
                        sp = (float*)(rect.DataPointer + rect.Pitch * i);
                        for (int j = 0; j < bmp2.Width * 4; j += 4) //rgbaF -> bgraB
                        { *dp++ = (byte)(sp[j + 2] * 255); *dp++ = (byte)(sp[j + 1] * 255); *dp++ = (byte)(sp[j] * 255); *dp++ = (byte)(sp[j + 3] * 255); }
                    }
                    break;
                case Format.B8G8R8A8_UNorm: for (int i = 0; i < bmp2.Height; ++i)
                        Utilities.CopyMemory(bmpData.Scan0 + bmpData.Stride * i, rect.DataPointer + rect.Pitch * i, bmpData.Stride); break;
                default: break;
            }
            bmp.Unmap(); bmp2.UnlockBits(bmpData); return bmp2;
        }
        /// <summary>Copies the device context's render target for further CPU processing (such as saving to a file).</summary>
        /// <returns>The render target's bitmap. Requires a newer version of DirectX and cannot be read by the GPU.</returns>
        public static Bitmap1 GetScreenCPURead(this DeviceContext d2dc, Rectangle? source = null, Point? destination = null)
        {
            Rectangle sourceRect = source ?? new(0, 0, Form.ClientSize.Width, Form.ClientSize.Height);
            Bitmap1 result = new(d2dc, new Size2(sourceRect.Width, sourceRect.Height), new(d2dc.PixelFormat)
            { BitmapOptions = BitmapOptions.CannotDraw | BitmapOptions.CpuRead });
            result.CopyFromRenderTarget(d2dc, destination ?? new(0, 0), sourceRect); return result;
        }
        /// <summary>Copies the device context's render target for further GPU processing (such as shaders).</summary>
        /// <returns>The render target's bitmap. The bitmap cannot be read by the CPU.</returns>
        public static Bitmap GetScreenGPURead(this DeviceContext d2dc, Rectangle? source = null, Point? destination = null)
        {
            Rectangle sourceRect = source ?? new(0, 0, Form.ClientSize.Width, Form.ClientSize.Height);
            Bitmap result = new(d2dc, new(sourceRect.Width, sourceRect.Height), new(d2dc.PixelFormat));
            result.CopyFromRenderTarget(d2dc, destination ?? new(0, 0), sourceRect); return result;
        }
        /// <summary>Batch renders an array of effects applied to the entire screen.</summary>
        /// <param name="transform">An additional transorm to apply at the end.</param>
        /// <param name="effects">The array of effects to render.</param>
        public static DeviceContext RenderScreenShaders(this DeviceContext d2dc, EffectTransformer transform = null, params Effect[] effects)
        {
            Bitmap result = d2dc.GetScreenGPURead(); effects[0].SetInput(0, result, true);
            for (int i = 1; i < effects.Length; ++i) effects[i].SetInputEffect(0, effects[i - 1], true);
            if (transform != null) { transform?.Handle.SetInputEffect(0, effects[^1]); d2dc.DrawEffectTransform(transform); } 
            else d2dc.DrawImage(effects[^1]); result?.Dispose(); return d2dc;
        }
        public static DeviceContext DrawEffectTransform(this DeviceContext d2dc, EffectTransformer transform) { d2dc.DrawImage(transform.Handle); return d2dc; }
        public static DeviceContext5 DrawSvgDocument(this DeviceContext5 d2dc, SvgImage svg) { d2dc.DrawSvgDocument(svg.Document); return d2dc; }
        public static void Resize(object sender = null, EventArgs e = null)
        {
            Size2 screen = new(Form.ClientSize.Width, Form.ClientSize.Height); dxgiScd.Width = screen.Width; dxgiScd.Height = screen.Height;
            for (int i = 0; i < DCs.Count; ++i) if (resizables.Contains(i)) ResizeSetup(i, screen);
            SwapChain.Dispose(); SwapChain = new(FinalFactory, D3DDevice, Form.Handle, ref dxgiScd);
            //SwapChain.ResizeBuffers(dxgiScd.BufferCount, Form.ClientSize.Width, Form.ClientSize.Height, PixelFormat, dxgiScd.Flags);
            FinalDC.RenderingControls = new() { TileSize = screen };
            FinalTarget.Dispose(); FinalTarget = new(FinalDC, SwapChain.GetBackBuffer<Surface>(0), d2bFinal);
        }
        /// <summary>Adds a new amount of parallel rendering setups.</summary>
        /// <param name="amount">The amount of setups to create.</param>
        /// <param name="sizes">The default size of the device contexts about to be created. By default it's the window size.</param>
        /// <param name="useFloats">Create the contexts with 32-bit float color depth (128bpp) instead of 8-bit byte color depth (32bpp).
        /// <br/><br/>Graphics-wise, floats are more useful, as they allow for colors to be "whiter than white" (or more than 1) and "blacker than black" (negative),
        /// which is useful for pixel shaders. <br/>Performance-wise, bytes are faster if you're converting to GDI bitmaps often, and require 4 times less memory.
        /// <br/>You won't need to use bytes unless you need to solve one of these two issues.</param>
        public static void AddSetups(int amount, Size2? sizes = null, bool useFloats = true)
        {
            PixelFormat = useFloats ? Format.R32G32B32A32_Float : Format.B8G8R8A8_UNorm;
            Size2 screen = new(Form.ClientSize.Width, Form.ClientSize.Height);
            for (int i = 0; i < amount + 1; ++i)
            {
                DCs.Add(new(D2DDevice.QueryInterface<Device5>(), DeviceContextOptions.EnableMultithreadedOptimizations)
                { RenderingControls = new() { BufferPrecision = useFloats ? BufferPrecision.PerChannel32Float : BufferPrecision.PerChannel8UNorm,
                    TileSize = resizables.Contains(i) ? screen : sizes ?? screen } });
                RenderTargets.Add(new(DCs[^1], DCs[^1].RenderingControls.TileSize, d2bI));
            }
        }
        /// <summary>Resizes a setup.</summary>
        /// <param name="index">The index of the setup in the list.</param>
        /// <param name="size">New rendering size.</param>
        public static void ResizeSetup(int index, Size2 size)
        {
            DCs[index].RenderingControls = new() { BufferPrecision = DCs[index].RenderingControls.BufferPrecision, TileSize = size };
            PixelFormat = RenderTargets[index].PixelFormat.Format; RenderTargets[index].Dispose(); RenderTargets[index] = new(DCs[index], size, d2bI);
        }
        /// <summary>Put this at the beginning of your render method.</summary>
        public static void BeginRender() { for (int i = 0; i < DCs.Count; ++i) DCs[i].Target = RenderTargets[i]; FinalDC.Target = FinalTarget; }
        /// <summary>Put this at the end of your render method.</summary>
        public static void EndRender()
        {
            FinalDC.ChainBeginDraw().ChainClear(new(0, 0, 0, 1)).ChainDrawImage(RenderTargets[OutputDevice]).EndDraw();
            SwapChain.Present(1, PresentFlags.None, new()); Input.Update();
        }
        /// <summary>Disposes of a setup and removes it from the lists.</summary>
        public static void RemoveSetup(int index)
        {
            RenderTargets[index].Dispose(); RenderTargets.RemoveAt(index); DCs[index].Dispose(); DCs.RemoveAt(index);
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
        /// <summary>Adds an object to the end of the <seealso cref="List{T}"/> only if the object isn't already present.</summary>
        public static void AddIfMissing<T>(this List<T> list, T item) { if (!list.Contains(item)) list.Add(item); }
        /// <summary>Copies all contents of a <seealso cref="List{T}"/> into a new <seealso cref="List{T}"/>.</summary>
        public static List<T> Clone<T>(this List<T> list) { List<T> newList = new(); foreach (var item in list) newList.Add(item); return newList; }
        /// <summary>Clears a <seealso cref="List{T}"/> and adds one item.</summary>
        public static void OneItem<T>(this List<T> list, T item) { list.Clear(); list.Add(item); }
        /// <summary>Gets the device context's drawing rectangle.</summary>
        public static RectangleF ScreenRectangle(this DeviceContext d2dc) => new(0, 0, d2dc.Size.Width, d2dc.Size.Height);
        public static Size2 ToSize2(this Size2F size) => new((int)size.Width, (int)size.Height);
    }
}