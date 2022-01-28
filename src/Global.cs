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
        static SwapChainDescription1 dxgiScd;
        static readonly SharpDX.DXGI.Factory2 FinalFactory = new(); static Bitmap1 FinalTarget; static bool quit;
        /// <summary>The bitmap properties used for rendering. If you're creating a new bitmap, use these as a parameter.</summary>
        public static readonly BitmapProperties1 BitmapProperties = new(new(Format.R32G32B32A32_Float, AlphaMode.Premultiplied)) { BitmapOptions = BitmapOptions.Target };
        /// <summary>The final device context that renders on screen, and the only one that uses byte color depth.
        /// Updates after <b><see cref="EndRender"/></b> is called.</summary>
        /// <remarks>If you want to take a screenshot of the screen and convert it into a GDI bitmap, use <b><see cref="GetScreenCPURead"/></b> on this context
        /// for the fastest performance. If you were to convert any context from <b><see cref="Setups"/></b> to GDI, it would take much longer, as the other contexts use
        /// <b><see cref="Format.R32G32B32A32_Float"/></b>. In that case, the library would cast every individual float from the screen to a byte,
        /// which takes about half a second. Since the final context's pixel format is <b><see cref="Format.B8G8R8A8_UNorm"/></b>,
        /// it's the easiest and fastest to copy.</remarks>
        public static DeviceContext FinalDC { get; private set; }
        /// <summary><b><see cref="SwapChain"/></b>'s creation specs (in case you need them).</summary>
        public static SwapChainDescription1 SwapChainDescription => dxgiScd;
        /// <summary>A .cso file that will be loaded by every <b>pixel or compute shader</b> created from now on.
        /// One shader class can have different pixel shaders for every instance.</summary>
        public static string ShaderFile { get; set; }
        /// <summary>A .cso file that will be loaded by every <b>vertex shader</b> created from now on.
        /// One shader class can have different vertex shaders for every instance.</summary>
        public static string VertexShaderFile { get; set; }
        public static SharpDX.Windows.RenderForm Form { get; set; }
        /// <summary>The class used for registering your custom effects.</summary><remarks>Based on D2DDevice.</remarks>
        public static SharpDX.Direct2D1.Factory1 D2DFactory { get; private set; }
        /// <remarks>Based on D3DDevice.</remarks>
        public static SharpDX.Direct2D1.Device D2DDevice { get; private set; }
        /// <remarks>Based either on a DXGIFactory or can be freely created.</remarks>
        public static SharpDX.Direct3D11.Device D3DDevice { get; private set; }
        /// <remarks>Based on D3DDevice and a DXGIFactory.</remarks>
        public static SwapChain1 SwapChain { get; private set; }
        public class DrawingSetup
        {
            bool resize, useOnlyOneTile; Bitmap1 rt;
            /// <summary>The DeviceContext used for rendering everything, probably the most used class of them all.</summary><remarks>Based on D2DDevice.</remarks>
            public DeviceContext DC { get; private set; }
            /// <summary>The render target of the DeviceContext. Any changes of the target will apply to the device context as well.</summary>
            /// <remarks>Based on DeviceContext.</remarks>
            public Bitmap1 RenderTarget { get => rt; set => DC.Target = rt = value; }
            /// <summary>Set this context to be resizable with the screen.</summary>
            public bool ResizeWithScreen => resize;
            /// <summary>The context's tile size will be equal to the context's size. The setting will take effect after the window is moved or resized.</summary>
            public bool UseOnlyOneTile => useOnlyOneTile;
            /// <summary>Changes settings of the setup and applies them. Set values to null to leave them as they are.</summary>
            /// <param name="resizeWithScreen">The <see cref="ResizeWithScreen"/> property.</param>
            /// <param name="oneTile">The <see cref="UseOnlyOneTile"/> property.</param>
            public void ApplySettings(bool? resizeWithScreen, bool? oneTile)
            {
                useOnlyOneTile = oneTile ?? UseOnlyOneTile; resize = resizeWithScreen ?? resize;
                Resize(resize ? RenderTarget.PixelSize : new(Form.ClientSize.Width, Form.ClientSize.Height));
            }
            /// <summary>Adds a new rendering setup.</summary>
            /// <param name="size">The size of the context's target</param>
            /// <param name="tileSize">The <see cref="TileSize"/> property. By default it's 64×64 or the window size if <paramref name="oneTile"/> is set to true.</param>
            /// <param name="resizeWithScreen">The <see cref="ResizeWithScreen"/> property.</param>
            /// <param name="oneTile">The <see cref="UseOnlyOneTile"/> property.</param>
            /// <param name="useFloats">Create the context with 32-bit float color depth (128bpp) instead of 8-bit byte color depth (32bpp).
            /// <br/><br/>Graphics-wise, floats are more useful, as they allow for colors to be "whiter than white" (or more than 1) and "blacker than black" (negative),
            /// which is useful for pixel shaders. <br/>Performance-wise, bytes are faster if you're converting to GDI bitmaps often, and require 4 times less memory.
            /// <br/>You won't need to use bytes unless you need to solve one of these two issues.</param>
            public DrawingSetup(Size2? size, Size2? tileSize, bool resizeWithScreen, bool oneTile, bool useFloats)
            {
                Size2 screen = new(Form.ClientSize.Width, Form.ClientSize.Height);
                resize = resizeWithScreen; TileSize = tileSize ?? TileSize; useOnlyOneTile = oneTile;
                DC = new(D2DDevice.QueryInterface<SharpDX.Direct2D1.Device>(), DeviceContextOptions.EnableMultithreadedOptimizations)
                {
                    RenderingControls = new()
                    {
                        BufferPrecision = useFloats ? BufferPrecision.PerChannel32Float : BufferPrecision.PerChannel8UNorm,
                        TileSize = TileSize
                    },
                    UnitMode = UnitMode.Pixels
                };
                RenderTarget = new(DC, size ?? screen, BitmapProperties);
            }
            /// <summary>The maximum allowed size of tiles the shader will be split into.
            /// <b>The tiles will not use this exact size, but they will never exceed this size.</b></summary>
            /// <remarks>Setting the tile size both to a small amount or a large amount has disadvantages. Since texture coordinates in shaders are determined by tiles,
            /// the position in both pixel and compute shaders starts at the top left corner of each tile. Setting <see cref="UseOnlyOneTile"/> 
            /// to true solves this issue, but rendering shaders will be slower, as the GPU tries to render the whole image at once.</remarks>
            public Size2 TileSize { get; set; } = new(64, 64);
            public void Resize(Size2 size)
            {
                DC.RenderingControls = new() { BufferPrecision = DC.RenderingControls.BufferPrecision,
                    TileSize = UseOnlyOneTile ? new(Form.ClientSize.Width, Form.ClientSize.Height) : TileSize };
                RenderTarget.Dispose(); RenderTarget = new(DC, size, BitmapProperties);
            }
            public void Dispose() { RenderTarget.Dispose(); DC.Dispose(); }
            /// <summary>Batch renders an array of effects applied to the entire screen.</summary>
            /// <param name="tileCorrection"></param>
            /// <param name="effects">The array of effects to render.</param>
            public DeviceContext RenderScreenShaders(bool tileCorrection, params Effect[] effects)
            {
                if (tileCorrection)
                {
                    Bitmap1 result = new(DC, new Size2(Form.ClientSize.Width, Form.ClientSize.Height), BitmapProperties), save, origin = RenderTarget;
                    for (int i = 0; i < effects.Length; ++i)
                    { save = RenderTarget; effects[i].SetInput(0, save, false); RenderTarget = result; DC.DrawImage(effects[i]); result = save; }
                    if (RenderTarget != origin) { result = RenderTarget; RenderTarget = origin; DC.DrawImage(result); }
                    result?.Dispose();
                }
                else
                {
                    Bitmap result = DC.GetScreenGPURead(); effects[0].SetInput(0, result, false);
                    for (int i = 1; i < effects.Length; ++i) effects[i].SetInputEffect(0, effects[i - 1], false);
                    DC.DrawImage(effects[^1]); result?.Dispose();
                }
                return DC;
            }
        }
        public static List<DrawingSetup> Setups { get; } = new();
        /// <summary>The class passed as a parameter when rendering text.</summary><remarks>Can be freely created.</remarks>
        public static DWFactory DWriteFactory { get; private set; }
        /// <summary>The class used for creating SVG's.</summary><remarks>Can be freely created.</remarks>
        public static WICFactory WICFactory { get; } = new();
        /// <summary>A list of all effects that were created.</summary> 
        public static List<CustomEffectBase> ExistingEffects { get; } = new();
        static DrawingSetup outSetup;
        /// <summary>The setup that will serve as the output and present its contents to the screen.</summary>
        public static DrawingSetup OutputSetup { get => outSetup; set { outSetup = value; outSetup.ApplySettings(true, outSetup.UseOnlyOneTile); } }
        /// <summary>Creates all the stuff needed for a basic SharpDX setup. The first device (0th) will be set as output.</summary>
        /// <param name="parallelDevices">The amount of parallel Direct2D setups to create (for multirendering).
        /// All components will be accessible from their lists. Cannot be less than 1.</param>
        /// <param name="crashIfLag">Crashes if a shader takes more than 2 seconds to execute. Useful when testing out shaders with loops.</param>
        /// <param name="sizes">The default size of all device contexts, except for the outputting one. By default it's the window size.</param>
        public static void Initialize(int parallelDevices = 1, bool crashIfLag = false, Size2? sizes = null)
        {
            if (parallelDevices < 1) parallelDevices = 1;
            D3DDevice = new(DriverType.Hardware, DeviceCreationFlags.BgraSupport | (crashIfLag ? DeviceCreationFlags.None : DeviceCreationFlags.DisableGpuTimeout));
            D2DDevice = new(D3DDevice.QueryInterface<SharpDX.DXGI.Device1>());
            D2DFactory = D2DDevice.Factory.QueryInterface<SharpDX.Direct2D1.Factory1>(); D2DFactory.RegisterEffect<CloneablePixelShader>();
            dxgiScd = new()
            {
                Width = Form.ClientSize.Width, Height = Form.ClientSize.Height, Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new(1, 0), Scaling = Scaling.Stretch, BufferCount = 1,
                Usage = Usage.RenderTargetOutput | Usage.ShaderInput | Usage.BackBuffer,
                SwapEffect = SwapEffect.Sequential, Flags = SwapChainFlags.AllowModeSwitch
            };
            Form.ResizeEnd += Resize; Input.Input.Initialize();
            try { Sound.SoundGlobal.Initialize(); } catch (System.IO.FileNotFoundException) { }
            Form.FormClosing += (s, e) =>
            {
                foreach (var setup in Setups) setup.Dispose();
                FinalDC.Dispose(); FinalTarget.Dispose(); FinalFactory.Dispose();
                SwapChain.ContainingOutput.Dispose(); SwapChain.GetBackBuffer<Surface>(0).Dispose(); SwapChain.Dispose();
                foreach (var effect in ExistingEffects) effect.Dispose();
                foreach (var effect in D2DFactory.RegisteredEffects) D2DFactory.GetEffectProperties(effect).Dispose();
                D2DDevice.ClearResources(int.MaxValue); D2DDevice.Dispose();
                D3DDevice.ImmediateContext.Dispose(); D3DDevice.Dispose();
                DWriteFactory.GdiInterop.Dispose(); DWriteFactory.Dispose();
                WICFactory.Dispose(); Form.Dispose();
            };
            DWriteFactory = new(SharpDX.DirectWrite.FactoryType.Isolated);
            SwapChain = new(FinalFactory, D3DDevice, Form.Handle, ref dxgiScd);
            FinalDC = new(D2DDevice.QueryInterface<Device5>(), DeviceContextOptions.EnableMultithreadedOptimizations)
            { RenderingControls = new() { TileSize = new(Form.ClientSize.Width, Form.ClientSize.Height) } };
            FinalDC.Target = FinalTarget = new(FinalDC, SwapChain.GetBackBuffer<Surface>(0), null);
            for (int i = 0; i < parallelDevices; ++i) Setups.Add(new(sizes, null, false, false, true));
            OutputSetup = Setups[0];
        }
        /// <summary>Starts displaying the window and runs the rendering loop.</summary>
        /// <param name="RenderMethod">The method that will be called on each monitor refresh.</param>
        /// <remarks><b>Do not use this for updating game logic!</b> The <b><paramref name="RenderMethod"/></b> is dependent on your monitor refresh rate
        /// (60x or 144x per second). Instead, create a second thread that's called every 1/60th of a second.</remarks>
        public static void Run(Action RenderMethod)
        {
            if (RenderMethod == null) return; Form.Show();
            using SharpDX.Windows.RenderLoop renderLoop = new(Form);
            while (renderLoop.NextFrame() && !quit) RenderMethod(); Form.Close();
        }
        /// <summary>Schedules a shutdown of the window. After the current rendering method ends, all sounds are stopped and components deleted.</summary>
        /// <remarks>Windows doesn't allow for different threads to close a single window, which means the closure needs to be scheduled for later
        /// and then executed by the window thread.</remarks>
        public static void Quit() => quit = true;
        /// <summary>Creates a SharpDX Bitmap off of an image file.</summary>
        public static Bitmap LoadBitmapFromFile(this DeviceContext deviceContext, string filename)
        {
            using var bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(filename);
            var bmp2 = deviceContext.ConvertGDIToD2DBitmap(bmp); return bmp2;
        }
        /// <summary>Creates a SharpDX Bitmap off of a GDI+ bitmap.</summary>
        public static Bitmap ConvertGDIToD2DBitmap(this DeviceContext deviceContext, System.Drawing.Bitmap bmp)
        {
            BitmapData bmpData = bmp.LockBits(new(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using DataStream stream = new(bmpData.Scan0, bmpData.Stride * bmpData.Height, true, false);
            Bitmap result = new(deviceContext, new(bmp.Width, bmp.Height), stream, bmpData.Stride, new(new(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)));
            bmp.UnlockBits(bmpData); return result;
        }
        /// <summary>Creates a GDI+ bitmap off of a SharpDX Bitmap. Very slow if using float colors.</summary>
        /// <remarks>If you're taking a screenshot, use <b><see cref="FinalDC"/></b> to get the final rendering result.
        /// <b><see cref="FinalDC"/></b> always uses byte colors, which means the screenshot process is fairly quick.</remarks>
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
        public static DeviceContext5 DrawSvgDocument(this DeviceContext5 d2dc, SvgImage svg) { d2dc.DrawSvgDocument(svg.Document); return d2dc; }
        public static void Resize(object sender = null, EventArgs e = null)
        {
            Size2 screen = new(Form.ClientSize.Width, Form.ClientSize.Height); dxgiScd.Width = screen.Width; dxgiScd.Height = screen.Height;
            for (int i = 0; i < Setups.Count; ++i) if (Setups[i].ResizeWithScreen) Setups[i].Resize(screen);
            SwapChain.ContainingOutput.Dispose(); SwapChain.GetBackBuffer<Surface>(0).Dispose();
            SwapChain.Dispose(); SwapChain = new(FinalFactory, D3DDevice, Form.Handle, ref dxgiScd);
            //SwapChain.ResizeBuffers(dxgiScd.BufferCount, Form.ClientSize.Width, Form.ClientSize.Height, dxgiScd.PixelFormat, dxgiScd.Flags);
            FinalDC.RenderingControls = new() { TileSize = screen };
            FinalTarget.Dispose(); FinalTarget = new(FinalDC, SwapChain.GetBackBuffer<Surface>(0), null);
        }
        /// <summary>Presents the final output on screen. Put this at the end of your render method.</summary>
        public static void EndRender()
        {
            FinalDC.Target = FinalTarget; FinalDC.ChainBeginDraw().ChainDrawImage(OutputSetup.RenderTarget, compositeMode: CompositeMode.SourceCopy).EndDraw();
            SwapChain.Present(1, PresentFlags.None, new()); Input.Input.Update();
        }
        /// <summary>Disposes of a setup and removes it from the lists.</summary>
        public static void RemoveSetup(int index) { if (Setups[index] == OutputSetup) OutputSetup = Setups[^1]; Setups[index].Dispose(); Setups.RemoveAt(index); }
        /// <summary>Adds an object to the end of the <seealso cref="List{T}"/> only if the object isn't already present.</summary>
        public static void AddIfMissing<T>(this List<T> list, T item) { if (!list.Contains(item)) list.Add(item); }
        /// <summary>Copies all contents of a <seealso cref="List{T}"/> into a new <seealso cref="List{T}"/>.</summary>
        public static List<T> Clone<T>(this List<T> list) { List<T> newList = new(); foreach (var item in list) newList.Add(item); return newList; }
        /// <summary>Clears a <seealso cref="List{T}"/> and adds one item.</summary>
        public static void OneItem<T>(this List<T> list, T item) { list.Clear(); list.Add(item); }
        /// <summary>Gets the device context's drawing rectangle.</summary>
        public static RectangleF ScreenRectangle(this DeviceContext d2dc) => new(0, 0, d2dc.Size.Width, d2dc.Size.Height);
        public static Size2 ToSize2(this Size2F size) => new((int)size.Width, (int)size.Height);
        /// <summary>Sets the data of a vertex buffer after the <see cref="VertexBuffer.Map(byte[], int)"/> method has been called.</summary>
        /// <param name="originalData">The array received from the <see cref="VertexBuffer.Map(byte[], int)"/> method.</param>
        /// <param name="offset">The offset of the value to modify, in bytes.</param>
        /// <param name="value">The value to set.</param>
        public static unsafe void SetMappedData<T>(this VertexBuffer vb, byte[] originalData, int offset, T value) where T : unmanaged
        { fixed (void* pointer = &originalData[0]) *(T*)(*(IntPtr*)pointer + offset) = value; }
        /// <summary>Gets the data of a vertex buffer after the <see cref="VertexBuffer.Map(byte[], int)"/> method has been called.</summary>
        /// <param name="originalData">The array received from the <see cref="VertexBuffer.Map(byte[], int)"/> method.</param>
        /// <param name="offset">The offset of the value to modify, in bytes.</param>
        public static unsafe T GetMappedData<T>(this VertexBuffer vb, byte[] originalData, int offset) where T : unmanaged
        { fixed (void* pointer = &originalData[0]) return *(T*)(*(IntPtr*)pointer + offset); }
    }
}