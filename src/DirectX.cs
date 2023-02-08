using System;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Collections.Generic;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;

using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using DeviceContext = SharpDX.Direct2D1.DeviceContext;

namespace Ensoftener.DirectX
{
    /// <summary>The class containing everything necessary, from Direct2D components to new and useful DeviceContext methods.</summary>
    public static class GDX
    {
        #region properties
        /// <summary>A .cso file that will be loaded by every <b>pixel or compute shader</b> created from now on.
        /// One shader class can have different pixel shaders for every instance.</summary>
        public static byte[] NextShader { get; set; }
        /// <summary>A .cso file that will be loaded by every <b>vertex shader</b> created from now on.
        /// One shader class can have different vertex shaders for every instance.</summary>
        public static byte[] NextVertexShader { get; set; }
        public static SharpDX.DXGI.Factory2 DXGIFactory { get; private set; }
        /// <summary>The class used for registering your custom effects.</summary><remarks>Depends on D2DDevice.</remarks>
        public static SharpDX.Direct2D1.Factory1 D2DFactory { get; private set; }
        /// <remarks>Depends on D3DDevice.</remarks>
        public static SharpDX.Direct2D1.Device D2DDevice { get; private set; }
        /// <remarks>Depends either on a DXGIFactory or can be freely created.</remarks>
        public static SharpDX.Direct3D11.Device D3DDevice { get; private set; }
        /// <summary>A list of DirectX windows that run within the program.</summary>
        /// <remarks><b>Do NOT remove windows directly from the list!</b> For safe removal, simply call <see cref="Form.Close()"/> on the window.</remarks>
        public static List<DXWindow> Windows { get; } = new();
        /// <summary>The window over which is the cursor hovering.</summary>
        public static DXWindow WindowWithCursor { get; private set; }
        /// <summary>A list of drawing setups. See <see cref="DrawingSetup"/> for more information.</summary>
        /// <remarks><b>Do NOT remove setups directly from the list!</b> Use <see cref="RemoveSetup(int)"/> for safe removal.</remarks>
        public static List<DrawingSetup> Setups { get; } = new();
        /// <summary>The class passed as a parameter when rendering text.</summary><remarks>Can be freely created.</remarks>
        public static SharpDX.DirectWrite.Factory DWriteFactory { get; private set; }
        /// <summary>The class used for creating SVG's.</summary><remarks>Can be freely created.</remarks>
        public static SharpDX.WIC.ImagingFactory WICFactory { get; private set; }
        /// <summary>A list of all effects that were created.</summary> 
        public static List<CustomEffectBase> ExistingEffects { get; } = new();
        /// <summary>The render method will be always tied to the monitor's refresh rate if false.</summary>
        public static bool NoVSyncSupported { get; private set; }
        #endregion
        /// <summary>Represents a DirectX window. Use <see cref="Windows"/> to add them to your program.</summary>
        public class DXWindow
        {
            Bitmap FinalTarget; bool resizing; SwapChainDescription1 dxgiScd;
            System.Drawing.Size fakeFullPrevSize = new(); FormBorderStyle fakeFullPrevBorder = FormBorderStyle.Sizable;
            FormWindowState fakeFullPrevState = FormWindowState.Normal;
            internal SharpDX.Windows.RenderLoop RenderLoop { get; private set; }
            public SharpDX.Windows.RenderForm Form { get; set; }
            /// <summary>The final device context that renders on screen, and the only one that uses byte color depth.</summary>
            /// <remarks>If you want to take a screenshot of the screen and convert it into a GDI bitmap, use <b><see cref="GetScreenCPURead"/></b> on this context
            /// for the fastest performance. If you were to convert any context from <b><see cref="Setups"/></b> to GDI, it would take much longer, as the other contexts use
            /// <b><see cref="Format.R32G32B32A32_Float"/></b>. In that case, the library would cast every individual float from the screen to a byte,
            /// which takes about half a second. Since the final context's pixel format is <b><see cref="Format.B8G8R8A8_UNorm"/></b>,
            /// it's the easiest and fastest to copy.</remarks>
            public DeviceContext FinalDC { get; private set; }
            /// <remarks>Based on D3DDevice and a DXGIFactory.</remarks>
            public SwapChain1 SwapChain { get; private set; }
            /// <summary><b><see cref="SwapChain"/></b>'s creation specs (in case you need them).</summary>
            public SwapChainDescription1 SwapChainDescription => dxgiScd;
            /// <summary>The setup that will serve as the output and present its contents to the screen. When null, the window output remains stationary.</summary>
            public DrawingSetup OutputSetup { get; set; }
            /// <summary>Updates mouse+keyboard input in the main render thread. This is very important to look after, as updating input while another thread
            /// is running can have consequences if the other thread relies on this input.</summary>
            public bool UpdateInputOnRender { get; set; } = true;
            /// <summary>The method that will run on render.</summary>
            /// <remarks><b>Do not use this for updating game logic!</b> The <b><paramref name="RenderMethod"/></b> is dependent on your monitor refresh rate (Hz),
            /// or instant if <b><see cref="VSync"/></b> is off. Instead, add a loop to <b><see cref="GShared.SideLoops"/></b> that runs at a fixed refresh rate.</remarks>
            public event Action RenderMethod;
            /// <summary>Runs after the render method ended and the output setup's contents were copied to the screen.</summary>
            public event EventHandler RenderFinal;
            public event EventHandler ResizeBegin;
            public event EventHandler ResizeEnd;
            /// <summary>Tie the render method to the monitor's refresh rate. Turning VSync off runs the render method as fast as possible.
            /// For VSync-less behaviour to fully take effect, VSync must be turned off for all windows.</summary>
            /// <remarks>This is (partially) why updating game logic within the render method is a bad idea and why these should run in <see cref="GShared.SideLoops"/>.</remarks>
            public bool VSync { get; set; } = true;
            /// <param name="legacySwapChain">Use a slower swapchain variant (copies before presenting to screen) for better backwards compatibility.
            /// <b><see cref="VSync"/></b> does not work on legacy swap chains.</param>
            public DXWindow(int width, int height, string title = null, bool legacySwapChain = false)
            {
                Form = new(title) { ClientSize = new(width, height) };
                dxgiScd = new()
                {
                    Width = width, Height = height, Format = Format.B8G8R8A8_UNorm, SampleDescription = new(1, 0), BufferCount = 2,
                    Usage = Usage.RenderTargetOutput | Usage.BackBuffer | Usage.ShaderInput, Scaling = Scaling.Stretch,
                    SwapEffect = legacySwapChain ? SwapEffect.Sequential : SwapEffect.FlipSequential,
                    Flags = legacySwapChain ? SwapChainFlags.AllowModeSwitch : SwapChainFlags.AllowModeSwitch | SwapChainFlags.AllowTearing
                };
                SwapChain = new(DXGIFactory, D3DDevice, Form.Handle, ref dxgiScd);
                using var device5 = D2DDevice.QueryInterface<SharpDX.Direct2D1.Device5>(); FinalDC = new(device5, DeviceContextOptions.EnableMultithreadedOptimizations);
                using var surface = SwapChain.GetBackBuffer<Surface>(0);
                FinalDC.Target = FinalTarget = new(FinalDC, surface, new() { PixelFormat = new(Format.B8G8R8A8_UNorm, AlphaMode.Ignore) });
                Form.ResizeEnd += Resize; try { Sound.SoundGlobal.Initialize(); } catch (System.IO.FileNotFoundException) { }
                Form.FormClosed += (s, e) => { Dispose(); if (Application.OpenForms.Count == 0) { GShared.Quit(); GDX.Dispose(); Form.Dispose(); } };
                Form.MouseMove += (s, e) => { WindowWithCursor = this; if (UpdateInputOnRender) Input.Input.Form_MouseMove(s, e); };
                Form.MouseWheel += (s, e) => { if (UpdateInputOnRender) Input.Input.Form_MouseWheel(s, e); };
                Form.KeyPress += (s, e) => { if (UpdateInputOnRender) Input.Input.Form_KeyPress(s, e); };
                D3DDevice.ImmediateContext.Rasterizer.SetViewport(0, 0, Form.ClientSize.Width, Form.ClientSize.Height, 0, float.PositiveInfinity);
            }
            void Dispose()
            {
                FinalDC.Dispose(); FinalTarget.Dispose(); SwapChain.Dispose(); RenderLoop?.Dispose(); Windows.Remove(this);
                foreach (var setup in Setups) if (setup.ResizeWithWindow == this) setup.ResizeWithWindow = null;
            }
            /// <summary>An event function to force update the window's context size (including setups).</summary>
            void Resize(object sender = null, EventArgs e = null)
            {
                if (resizing || (dxgiScd.Width == Form.ClientSize.Width && dxgiScd.Height == Form.ClientSize.Height && !SwapChain.IsFullScreen)) return;
                resizing = true; ResizeBegin?.Invoke(sender, e); Size2 screen = new(Form.ClientSize.Width, Form.ClientSize.Height);
                for (int i = 0; i < Setups.Count; ++i) if (Setups[i].ResizeWithWindow == this) Setups[i].Resize(screen); FinalDC.Target = null; FinalTarget.Dispose();
                SwapChain.ResizeBuffers(dxgiScd.BufferCount, screen.Width, screen.Height, dxgiScd.Format, dxgiScd.Flags);
                using var surface2 = SwapChain.GetBackBuffer<Surface>(0);
                FinalDC.Target = FinalTarget = new(FinalDC, surface2, new() { PixelFormat = new(Format.B8G8R8A8_UNorm, AlphaMode.Ignore) }); resizing = false;
                D3DDevice.ImmediateContext.Rasterizer.SetViewport(0, 0, Form.ClientSize.Width, Form.ClientSize.Height, 0, float.PositiveInfinity);
                ResizeEnd?.Invoke(sender, e);
            }
            /// <summary>Starts the window's render loop when <see cref="Run()"/> is called.</summary>
            /// <param name="show">Shows the window (unless there is a reason not to do it, which would defy the point of making a window in the first place).
            /// The window can be shown later by calling <see cref="Control.Show()"/> on the <see cref="Form"/> property.</param>
            public void Start(bool show = true) { if (show) Form.Show(); RenderLoop = new(Form) { UseApplicationDoEvents = true }; }
            internal void OnRender()
            {
                RenderMethod?.Invoke(); if (OutputSetup != null) FinalDC.ChainBeginDraw().ChainDrawBitmap(OutputSetup.Bitmap).EndDraw();
                RenderFinal?.Invoke(this, new()); SwapChain.Present(!VSync && NoVSyncSupported ? 0 : 1, !VSync && NoVSyncSupported ? PresentFlags.AllowTearing : 0, new());
            }
            /// <summary>Spans the window over the entire screen. Leave size at 0 to upscale it to maximum or downscale it to last used size.</summary>
            /// <remarks>This is not a real "fullscreen", compared to <see cref="SwapChain.IsFullScreen"/>. The swapchain method currently doesn't work.</remarks>
            public void Fullscreen(bool enable, int width = 0, int height = 0)
            {
                if (enable)
                {
                    fakeFullPrevBorder = Form.FormBorderStyle; Form.FormBorderStyle = FormBorderStyle.None; Form.Activate();
                    fakeFullPrevState = Form.WindowState; Form.WindowState = FormWindowState.Maximized;
                    var rect = SwapChain.ContainingOutput.Description.DesktopBounds; fakeFullPrevSize = Form.ClientSize;
                    Form.ClientSize = new(width > 0 ? width : rect.Right - rect.Left, height > 0 ? height : rect.Bottom - rect.Top); Resize();
                }
                else
                {
                    Form.FormBorderStyle = fakeFullPrevBorder; Form.WindowState = fakeFullPrevState;
                    Form.ClientSize = new(width > 0 ? width : fakeFullPrevSize.Width, height > 0 ? height : fakeFullPrevSize.Height); Resize();
                }
            }
        }
        /// <summary>A setup that contains a context and a bitmap. Context drawing methods must start with <see cref="RenderTarget.BeginDraw()"/> and
        /// <see cref="RenderTarget.EndDraw()"/>.</summary>
        /// <remarks>In a way, the context is a "painter" and the bitmap is a "canvas". The context contains methods on what to draw, and the bitmap can be swapped or resized
        /// (but only when the context is not drawing).</remarks>
        public class DrawingSetup
        {
            Size2? tileSize; DXWindow rwWindow; Bitmap1 bitmap;
            /// <summary>The bitmap properties used for rendering.
            /// If you are aiming to create a new bitmap with the same properties as the one that is already in use, use these as a parameter.</summary>
            /// <remarks>Please note that the properties also contain the format of the bitmap (bytes/floats).
            /// If the setup was made with <b>useFloats</b> set to false, these properties reflect the change.
            /// <br/>To those who do not want to depend on this property while creating a new bitmap, here is the exact code used for creation:
            /// <code>new(new(useFloats ? Format.B8G8R8A8_UNorm : Format.R32G32B32A32_Float, AlphaMode.Premultiplied)) { BitmapOptions = BitmapOptions.Target }</code></remarks>
            public readonly BitmapProperties1 BitmapProperties;
            /// <summary>The DeviceContext used for rendering everything, probably the most used class of them all.</summary><remarks>Based on D2DDevice.</remarks>
            public DeviceContext DC { get; private set; }
            /// <summary>The render target of the DeviceContext. Any changes of the target will apply to the device context as well.</summary>
            /// <remarks>Depends on DeviceContext.</remarks>
            public Bitmap1 Bitmap { get => bitmap; set => DC.Target = bitmap = value; }
            /// <summary>Set this context to be resizable with a window.</summary>
            public DXWindow ResizeWithWindow
            { get => rwWindow; set { rwWindow = value; if (value != null) Resize(new(value.Form.ClientSize.Width, value.Form.ClientSize.Height)); } }
            /// <summary>The maximum allowed size of tiles the shader will be split into. Null sets the size to the entire bitmap.
            /// <b>The tiles will not use this exact size, but they will never exceed this size.</b></summary>
            /// <remarks>Setting the tile size both to a small amount or a large amount has disadvantages. Since texture coordinates in shaders are determined by tiles,
            /// the position in both pixel and compute shaders starts at the top left corner of each tile. Setting
            /// to null solves this issue, but rendering shaders will be slower, as the GPU tries to render the whole image at once.</remarks>
            public Size2? TileSize { get => tileSize; set { tileSize = value; Resize(Bitmap.PixelSize); } }
            public bool IsDisposed { get; private set; }
            /// <summary>Adds a new rendering setup.</summary>
            /// <param name="size">The size of the context's target.</param>
            /// <param name="tileSize">The <see cref="TileSize"/> property.</param>
            /// <param name="useFloats">Create the context with 32-bit float color depth (128bpp) instead of 8-bit byte color depth (32bpp).
            /// <br/><br/>Graphics-wise, floats are more useful, as they allow for colors to be "whiter than white" (or more than 1) and "blacker than black" (negative),
            /// which is useful for pixel shaders. <br/>Performance-wise, bytes are faster if you are converting to GDI bitmaps often, and require 4 times less memory.
            /// <br/>You will not need to use bytes unless you need to solve one of these two issues.</param>
            public DrawingSetup(Size2 size, Size2? tileSize = null, bool useFloats = true)
            {
                this.tileSize = tileSize;
                BitmapProperties = new(new(useFloats ? Format.B8G8R8A8_UNorm : Format.R32G32B32A32_Float, AlphaMode.Premultiplied)) { BitmapOptions = BitmapOptions.Target };
                DC = new(D2DDevice, DeviceContextOptions.EnableMultithreadedOptimizations)
                {
                    RenderingControls = new()
                    { BufferPrecision = useFloats ? BufferPrecision.PerChannel32Float : BufferPrecision.PerChannel8UNorm, TileSize = TileSize ?? size },
                    UnitMode = UnitMode.Pixels
                };
                Bitmap = new(DC, size, BitmapProperties);
            }
            /// <summary>Adds a new rendering setup.</summary>
            /// <param name="resizeWithWindow">The window to resize the bitmap with.</param>
            /// <param name="tileSize">The <see cref="TileSize"/> property.</param>
            /// <param name="useFloats">Create the context with 32-bit float color depth (128bpp) instead of 8-bit byte color depth (32bpp).
            /// <br/><br/>Graphics-wise, floats are more useful, as they allow for colors to be "whiter than white" (or more than 1) and "blacker than black" (negative),
            /// which is useful for pixel shaders. <br/>Performance-wise, bytes are faster if you are converting to GDI bitmaps often, and require 4 times less memory.
            /// <br/>You will not need to use bytes unless you need to solve one of these two issues.</param>
            public DrawingSetup(DXWindow resizeWithWindow, Size2? tileSize = null, bool useFloats = true)
            {
                this.tileSize = tileSize; rwWindow = resizeWithWindow;
                BitmapProperties = new(new(useFloats ? Format.B8G8R8A8_UNorm : Format.R32G32B32A32_Float, AlphaMode.Premultiplied)) { BitmapOptions = BitmapOptions.Target };
                Size2 screen = new(ResizeWithWindow.Form.ClientSize.Width, ResizeWithWindow.Form.ClientSize.Height);
                DC = new(D2DDevice, DeviceContextOptions.EnableMultithreadedOptimizations)
                {
                    RenderingControls = new()
                    { BufferPrecision = useFloats ? BufferPrecision.PerChannel32Float : BufferPrecision.PerChannel8UNorm, TileSize = TileSize ?? screen },
                    UnitMode = UnitMode.Pixels
                };
                Bitmap = new(DC, screen, BitmapProperties);
            }
            public void Resize(Size2 size)
            {
                Bitmap.Dispose(); Bitmap = new(DC, size, BitmapProperties);
                DC.RenderingControls = new() { BufferPrecision = DC.RenderingControls.BufferPrecision, TileSize = TileSize ?? Bitmap.PixelSize };
            }
            public void Dispose() { Bitmap.Dispose(); DC.Dispose(); IsDisposed = true; }
            /// <summary>Batch renders an array of effects applied to the entire screen.</summary>
            /// <param name="tileCorrection"></param>
            /// <param name="effects">The array of effects to render.</param>
            public DeviceContext RenderScreenShaders(bool tileCorrection, DXWindow window, params Effect[] effects)
            {
                if (tileCorrection)
                {
                    Bitmap1 result = new(DC, Bitmap.PixelSize, BitmapProperties), save, origin = Bitmap;
                    for (int i = 0; i < effects.Length; ++i)
                    { save = Bitmap; effects[i].SetInput(0, save, false); Bitmap = result; DC.DrawImage(effects[i]); result = save; }
                    if (Bitmap != origin) { result = Bitmap; Bitmap = origin; DC.DrawImage(result); }
                    result?.Dispose();
                }
                else
                {
                    Bitmap result = DC.GetScreenGPURead(window); effects[0].SetInput(0, result, false);
                    for (int i = 1; i < effects.Length; ++i) effects[i].SetInputEffect(0, effects[i - 1], false);
                    DC.DrawImage(effects[^1]); result?.Dispose();
                }
                return DC;
            }
        }
        #region methods
        /// <summary>Creates all the stuff needed for a basic SharpDX setup.</summary>
        /// <param name="crashIfLag">Crashes if a shader takes more than 2 seconds to execute. Useful when testing out shaders with loops.</param>
        public static void Initialize(bool crashIfLag = false)
        {
            //ComObject.LogMemoryLeakWarning += s => System.Windows.MessageBox.Show(s, "LEAK", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            //Configuration.EnableObjectTracking = true; //Configuration.EnableReleaseOnFinalizer = true;
            //SharpDX.Diagnostics.ObjectTracker.Tracked += (s, e) => { System.Windows.MessageBox.Show("New object: " + e.Object.ToString(), "NEW!"); };
            D3DDevice = new(SharpDX.Direct3D.DriverType.Hardware, (crashIfLag ? 0 : DeviceCreationFlags.DisableGpuTimeout) | DeviceCreationFlags.BgraSupport);
            //| DeviceCreationFlags.Debug);
            using var factory5 = (DXGIFactory = new()).QueryInterface<SharpDX.DXGI.Factory5>();
            try { NoVSyncSupported = factory5.PresentAllowTearing; } catch (Exception) { NoVSyncSupported = false; }
            using var device1 = D3DDevice.QueryInterface<SharpDX.DXGI.Device1>(); D2DDevice = new(device1);
            using var factory = D2DDevice.Factory; D2DFactory = factory.QueryInterface<SharpDX.Direct2D1.Factory1>(); D2DFactory.RegisterEffect<CloneablePixelShader>();
            DWriteFactory = new(SharpDX.DirectWrite.FactoryType.Isolated); WICFactory = new();
        }
        /// <summary>Starts displaying all windows and runs their rendering loops.</summary>
        public static void Run()
        {
            bool run = true;
            while (run && !GShared.Quitting)
            {
                run = false;
                for (int i = 0; i < Windows.Count; i++) { if (Windows[i].RenderLoop?.NextFrame() ?? false) { run = true; if (i < Windows.Count) Windows[i].OnRender(); } }
            }
            for (int i = 0; i < Windows.Count; i++) Windows[i].Form?.Close();
        }
        /// <summary>Disposes of a setup and unlinks it from all windows.</summary>
        public static void RemoveSetup(DrawingSetup setup)
        { setup.Dispose(); foreach (var window in Windows) if (window.OutputSetup == setup) window.OutputSetup = null; Setups.Remove(setup); }
        /// <summary>Disposes of a setup and unlinks it from all windows.</summary>
        public static void RemoveSetup(int index) => RemoveSetup(Setups[index]);
        static void Dispose()
        {
            foreach (var setup in Setups) setup.Dispose(); DXGIFactory.Dispose(); foreach (var effect in ExistingEffects) effect.Dispose();
            foreach (var effect in D2DFactory.RegisteredEffects) D2DFactory.GetEffectProperties(effect).Dispose();
            D2DDevice.ClearResources(int.MaxValue); D2DDevice.Dispose(); D3DDevice.Dispose(); DWriteFactory.Dispose(); WICFactory.Dispose();
        }
        public static void LoadShaderFromFile(string path, bool vertexShader = false)
        { byte[] s = System.IO.File.ReadAllBytes(path); if (vertexShader) NextVertexShader = s; else NextShader = s; }
        #endregion
        #region extensions
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
                case Format.B8G8R8A8_UNorm:
                    for (int i = 0; i < bmp2.Height; ++i)
                        Utilities.CopyMemory(bmpData.Scan0 + bmpData.Stride * i, rect.DataPointer + rect.Pitch * i, bmpData.Stride); break;
                default: break;
            }
            bmp.Unmap(); bmp2.UnlockBits(bmpData); return bmp2;
        }
        /// <summary>Copies the device context's render target for further CPU processing (such as saving to a file).</summary>
        /// <returns>The render target's bitmap. Requires a newer version of DirectX and cannot be read by the GPU.</returns>
        public static Bitmap1 GetScreenCPURead(this DeviceContext d2dc, DXWindow window, Rectangle? source = null, SharpDX.Point? destination = null)
        {
            Rectangle sourceRect = source ?? new(0, 0, window.Form.ClientSize.Width, window.Form.ClientSize.Height);
            Bitmap1 result = new(d2dc, new Size2(sourceRect.Width, sourceRect.Height), new(d2dc.PixelFormat)
            { BitmapOptions = BitmapOptions.CannotDraw | BitmapOptions.CpuRead });
            result.CopyFromRenderTarget(d2dc, destination ?? new(0, 0), sourceRect); return result;
        }
        /// <summary>Copies the device context's render target for further GPU processing (such as shaders).</summary>
        /// <returns>The render target's bitmap. The bitmap cannot be read by the CPU.</returns>
        public static Bitmap GetScreenGPURead(this DeviceContext d2dc, DXWindow window, Rectangle? source = null, SharpDX.Point? destination = null)
        {
            Rectangle sourceRect = source ?? new(0, 0, window.Form.ClientSize.Width, window.Form.ClientSize.Height);
            Bitmap result = new(d2dc, new(sourceRect.Width, sourceRect.Height), new(d2dc.PixelFormat));
            result.CopyFromRenderTarget(d2dc, destination ?? new(0, 0), sourceRect); return result;
        }
        public static DeviceContext5 DrawSvgDocument(this DeviceContext5 d2dc, SvgImage svg) { d2dc.DrawSvgDocument(svg.Document); return d2dc; }
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
        #endregion
        /// <summary>Like <see cref="SizableBuffer"/>, but more parallel for per-vertex editing and is compiled
        /// with flags <see cref="BindFlags.VertexBuffer"/> and <see cref="BindFlags.StreamOutput"/>.</summary>
        public class SizableVertexBuffer
        {
            List<uint[]> Vertices { get; } = new(); int numbersPerVertex;
            /// <summary>Amount of numbers to fit per vertex. A number is 4 bytes long, but 64-bit doubles can be spaced over two numbers.</summary>
            public int NumbersPerVertex
            {
                get => numbersPerVertex; set
                {
                    if (numbersPerVertex != value)
                    {
                        int min = Math.Min(numbersPerVertex, value);
                        for (int i = 0; i < Vertices.Count; i++) { uint[] vertex = new uint[value]; Array.Copy(Vertices[i], vertex, min); Vertices[i] = vertex; }
                    }
                    numbersPerVertex = value;
                }
            }
            public SizableVertexBuffer(int numbersPerVertex, int vertices = 0) { NumbersPerVertex = numbersPerVertex; AddVert(vertices); }
            public int Length => Vertices.Count;
            public void AddVert(int amount = 1) { for (int i = 0; i < amount; i++) Vertices.Add(new uint[NumbersPerVertex]); }
            public void InsertVert(int position) => Vertices.Insert(position, new uint[NumbersPerVertex]);
            public void RemoveVert(int position) => Vertices.RemoveAt(position);
            public unsafe T Read<T>(int vertex, int number) where T : unmanaged { fixed (void* pointer = &Vertices[vertex][number]) return *(T*)(IntPtr*)pointer; }
            public unsafe ref T Edit<T>(int vertex, int number) where T : unmanaged { fixed (void* pointer = &Vertices[vertex][number]) return ref *(T*)(IntPtr*)pointer; }
            public unsafe void Write<T>(int vertex, int number, T value) where T : unmanaged { fixed (void* pointer = &Vertices[vertex][number]) *(T*)(IntPtr*)pointer = value; }
            /// <summary>Compiles itself as a Direct3D vertex buffer. <b>Remember to dispose!</b></summary>
            /// <param name="dynamic">Allows editing the buffer after it was compiled (slower).</param>
            /// <param name="bind">Buffer usage flags. It's unlikely that a vertex buffer would serve as something else,
            /// but additionally, you may combine it with <see cref="BindFlags.StreamOutput"/> for recycled usage.</param>
            /// <param name="access">Allows reading/writing to the buffer by the CPU.</param><param name="options">Additional options.</param>
            public SharpDX.Direct3D11.Buffer Compile3D(bool dynamic = false, BindFlags bind = BindFlags.VertexBuffer,
                CpuAccessFlags access = CpuAccessFlags.None, ResourceOptionFlags options = ResourceOptionFlags.None)
            {
                uint[] result = new uint[Vertices.Count * NumbersPerVertex];
                for (int i = 0; i < Vertices.Count; i++) for (int j = 0; j < NumbersPerVertex; j++) result[i * numbersPerVertex + j] = Vertices[i][j];
                return SharpDX.Direct3D11.Buffer.Create(D3DDevice, bind, result, 0, dynamic ? ResourceUsage.Dynamic : ResourceUsage.Default, access, options);
            }
            /// <summary>Compiles itself as a Direct3D vertex buffer and prepares itself for next drawing. <b>Remember to dispose!</b></summary>
            /// <param name="slot">The slot to bind the buffer to. Maximum is 16 or 32, whatever is supported in the current Direct3D level.</param>
            /// <param name="topology">Whether to render vertices as points, lines or triangles. Strips are clusters of triangles/lines connected together in their respective
            /// order, and "adjacent" means some vertices won't render and exist only as surroundings of the previous triangle/line (clockwise or counter-clockwise).</param>
            /// <param name="dynamic">Allows editing the buffer after it was compiled (slower).</param>
            /// <param name="bind">Buffer usage flags. It's unlikely that a vertex buffer would serve as something else,
            /// but additionally, you may combine it with <see cref="BindFlags.StreamOutput"/> for recycled usage.</param>
            /// <param name="access">Allows reading/writing to the buffer by the CPU.</param><param name="options">Additional options.</param>
            public SharpDX.Direct3D11.Buffer Compile3DAndSet(int slot = 0, SharpDX.Direct3D.PrimitiveTopology topology = SharpDX.Direct3D.PrimitiveTopology.TriangleList,
                bool dynamic = false, BindFlags bind = BindFlags.VertexBuffer, CpuAccessFlags access = CpuAccessFlags.None, ResourceOptionFlags options = ResourceOptionFlags.None)
            {
                var buffer = Compile3D(dynamic, bind, access, options);
                D3DDevice.ImmediateContext.InputAssembler.PrimitiveTopology = topology;
                D3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(slot, new VertexBufferBinding(buffer, NumbersPerVertex * 4, 0));
                return buffer;
            }
        }
        public class SizableBuffer
        {
            uint[] Numbers = Array.Empty<uint>();
            /// <summary>Amount of numbers to fit per vertex. A number is 4 bytes long, but 64-bit doubles can be spaced over two numbers.</summary>
            public int Length
            {
                get => Numbers.Length;
                set { if (Numbers.Length != value) { uint[] vertex = new uint[value]; Array.Copy(Numbers, vertex, Math.Min(Numbers.Length, value)); Numbers = vertex; } }
            }
            public SizableBuffer(int length = 0) { Length = length; }
            public unsafe T Read<T>(int number) where T : unmanaged { fixed (void* pointer = &Numbers[number]) return *(T*)(IntPtr*)pointer; }
            public unsafe ref T Edit<T>(int number) where T : unmanaged { fixed (void* pointer = &Numbers[number]) return ref *(T*)(IntPtr*)pointer; }
            public unsafe void Write<T>(int number, T value) where T : unmanaged { fixed (void* pointer = &Numbers[number]) *(T*)(IntPtr*)pointer = value; }
            /// <summary>Compiles itself as a Direct3D vertex buffer. <b>Remember to dispose!</b></summary>
            /// <param name="dynamic">Allows editing the buffer after it was compiled (slower).</param>
            /// <param name="access">Allows reading/writing to the buffer by the CPU.</param><param name="options">Additional options.</param>
            public SharpDX.Direct3D11.Buffer Compile3D(bool dynamic = false, BindFlags bind = BindFlags.None,
                CpuAccessFlags access = CpuAccessFlags.None, ResourceOptionFlags options = ResourceOptionFlags.None)
                => SharpDX.Direct3D11.Buffer.Create(D3DDevice, bind, Numbers, 0, dynamic ? ResourceUsage.Dynamic : ResourceUsage.Default, access, options);
            /// <summary>Compiles itself as a Direct3D constant buffer, which can be passed to the individual 3D stages. <b>Remember to dispose!</b></summary>
            /// <param name="dynamic">Allows editing the buffer after it was compiled (slower).</param>
            /// <param name="access">Allows reading/writing to the buffer by the CPU.</param><param name="options">Additional options.</param>
            public SharpDX.Direct3D11.Buffer Compile3DAsConstantBuffer(bool dynamic = false,
                CpuAccessFlags access = CpuAccessFlags.None, ResourceOptionFlags options = ResourceOptionFlags.None)
                => Compile3D(dynamic, BindFlags.ConstantBuffer, access, options);
        }
    }
}