using System;
using SharpDX.MediaFoundation;

namespace Ensoftener;
using DirectX;
using System.Runtime.InteropServices;

/// <summary>Video player using <see cref="MediaEngine"/>. Audio buffer cannot be rerouted or accessed.</summary>
[SharpDX] public class SDX_MEVideo
{
    public MediaEngineEx Engine { get; }
    public string FilePath { get; set; }
    /// <summary>Video stream. Has priority over <see cref="FilePath"/>.</summary>
    public ByteStream SourceStream { get; set; }
    public double Speed { get => Engine.PlaybackRate; set => Engine.DefaultPlaybackRate = Engine.PlaybackRate = value; }
    public SharpDX.Direct2D1.Bitmap1 CurrentFrame { get; private set; }
    public double Volume { get => Engine.Volume; set => Engine.Volume = value; }
    public double Pan { get => Engine.Balance; set => Engine.Balance = value; }
    public bool Playing { get; private set; }
    public bool Loaded { get; private set; }
    /// <summary>Position, in seconds.</summary>
    public double Position { get => Engine.CurrentTime; set => Engine.CurrentTime = value; }
    public bool HasVideo => Engine.HasVideo();
    public bool HasAudio => Engine.HasAudio();
    public bool Loop { get => Engine.Loop; set => Engine.Loop = value; }
    public int AudioSampleRate { get; private set; }
    public int AudioChannels { get; private set; }
    public SharpDX.DXGI.Rational FPS { get; private set; }
    public int VideoWidth => (CurrentFrame?.PixelSize.Width) ?? 0;
    public int VideoHeight => (CurrentFrame?.PixelSize.Height) ?? 0;
    /// <summary>Length, in seconds.</summary>
    public double Length => Engine.Duration;
    public SDX_MEVideo(string path = null)
    {
        FilePath = path;
        using MediaEngineClassFactory mecf = new();
        using DXGIDeviceManager manager = new();
        manager.ResetDevice(GDX.D3DDevice);
        using var multi = GDX.D3DDevice.QueryInterface<SharpDX.Direct3D.DeviceMultithread>(); multi.SetMultithreadProtected(true);
        using MediaEngine engine = new(mecf, new() { VideoOutputFormat = (int)SharpDX.DXGI.Format.R8G8B8A8_UNorm, DxgiManager = manager });
        Engine = engine.QueryInterface<MediaEngineEx>();
        engine.PlaybackEvent += (mEvent, l, i) =>
        {
            if (mEvent == MediaEngineEvent.Play) Playing = true;
            if (mEvent == MediaEngineEvent.CanPlay || mEvent == MediaEngineEvent.Error) Loaded = true;
        };
    }
    public void Load(SharpDX.Direct2D1.DeviceContext dc)
    {
        Loaded = false;
        if (SourceStream != null) Engine.SetSourceFromByteStream(SourceStream, FilePath);
        else if (FilePath != null) Engine.Source = FilePath;
        else return;
        Engine.Load();
        while (!Loaded) ;
        for (int i = 0; i < Engine.NumberOfStreams; i++)
        {
            try
            {
                Engine.GetStreamAttribute(i, MediaTypeAttributeKeys.AudioNumChannels.Guid, out var variant);
                if (variant.ElementType == SharpDX.Win32.VariantElementType.UInt4) AudioChannels = (int)(uint)variant.Value;
            }
            catch (SharpDX.SharpDXException) { }
            try
            {
                Engine.GetStreamAttribute(i, MediaTypeAttributeKeys.AudioSamplesPerSecond.Guid, out var variant);
                if (variant.ElementType == SharpDX.Win32.VariantElementType.UInt4) AudioSampleRate = (int)(uint)variant.Value;
            }
            catch (SharpDX.SharpDXException) { }
            try
            {
                Engine.GetStreamAttribute(i, MediaTypeAttributeKeys.FrameRate.Guid, out var fps);
                if (fps.ElementType == SharpDX.Win32.VariantElementType.ULong) FPS = new((int)((ulong)fps.Value >> 32), (int)((ulong)fps.Value & 0xFFFFFFFF));
            }
            catch (SharpDX.SharpDXException) { }
        }
        Engine.GetNativeVideoSize(out int w, out int h);
        CurrentFrame?.Dispose();
        CurrentFrame = new(dc, new SharpDX.Size2(w, h), new(new(SharpDX.DXGI.Format.R8G8B8A8_UNorm, SharpDX.Direct2D1.AlphaMode.Premultiplied),
            GDX.D2DFactory.DesktopDpi.Width, GDX.D2DFactory.DesktopDpi.Height, SharpDX.Direct2D1.BitmapOptions.Target));
    }
    public void Play() { Engine.Play(); }
    public void Pause() { Engine.Pause(); }
    public void Stop() { Pause(); Position = 0; }
    public void FrameForward() => Engine.FrameStep(true);
    public void FrameBackward() => Engine.FrameStep(false);
    /// <summary>Renders a frame onto <see cref="CurrentFrame"/>.</summary>
    public void RenderFrame()
    {
        if (CurrentFrame != null && Loaded && Engine.OnVideoStreamTick(out long ptsRef))
            Engine.TransferVideoFrame(CurrentFrame.Surface, null, new(0, 0, CurrentFrame.PixelSize.Width, CurrentFrame.PixelSize.Height), null);
    }
    public void Dispose()
    {
        Engine.Dispose();
    }
}
/// <summary>Video player using <see cref="SourceReader"/>. Has a sound node as the audio output.</summary>
/// <remarks>There is no Play() method, the audio is played from the sound node (which needs an output) and the video is played via <see cref="RenderFrame"/>.</remarks>
[SharpDX] public class SDX_SRVideo<AudioType> where AudioType : struct
{
    public class SDX_SRAudio : SoundNodes.NodeGeneric<AudioType>
    {
        internal AudioType[] overhang = Array.Empty<AudioType>();
        SDX_SRVideo<AudioType> Video; long position;
        public double Position => TimeSpan.FromTicks(position).TotalSeconds;
        public SDX_SRAudio(SDX_SRVideo<AudioType> video) => Video = video;
        public override int SampleRate => Video.AudioSampleRate;
        public override int Channels => Video.AudioChannels;
        public unsafe override int ReadSamples(SoundNodes.SNBuffer<AudioType> buffer)
        {
            if (!Video.HasAudio) { FinishReading(buffer); return 0; }
            int copied = Math.Min(buffer.Count, overhang.Length), typeSize = Marshal.SizeOf<AudioType>();
            Buffer.BlockCopy(overhang, 0, buffer.Samples, buffer.Offset, copied * typeSize);
            if (overhang.Length >= buffer.Count)
            {
                AudioType[] newOverhang = new AudioType[overhang.Length - buffer.Count];
                Buffer.BlockCopy(overhang, copied * typeSize, newOverhang, 0, newOverhang.Length * typeSize);
                overhang = newOverhang;
                FinishReading(buffer); return buffer.Count;
            }
            for (int i = overhang.Length; i < buffer.Count;)
            {
                using var sample = Video.Reader.ReadSample(SourceReaderIndex.FirstAudioStream, SourceReaderControlFlags.None, out _, out var flags, out position);
                if (sample == null || flags.HasFlag(SourceReaderFlags.Endofstream)) { FinishReading(buffer); return i; } //konec
                if (Video.SyncVideoToAudio) Video.videoGoalPos = position - TimeSpan.FromSeconds((double)buffer.Count / SampleRate / Channels).Ticks;
                using var mBuffer = sample.ConvertToContiguousBuffer();
                IntPtr src = mBuffer.Lock(out int maxLength, out int currentLength);
                fixed (AudioType* s = buffer.Samples)
                    Buffer.MemoryCopy((void*)src, s + i + buffer.Offset, Math.Min(currentLength, (buffer.Count - i) * typeSize),
                        Math.Min(currentLength, (buffer.Count - i) * typeSize));
                if (currentLength > (buffer.Count - i) * typeSize)
                {
                    overhang = new AudioType[currentLength / typeSize - (buffer.Count - i)];
                    fixed (AudioType* o = overhang)
                        Buffer.MemoryCopy((AudioType*)src + (buffer.Count - i), o, overhang.LongLength * typeSize, overhang.LongLength * typeSize);
                }
                i += currentLength / typeSize;
                mBuffer.Unlock();
            }
            FinishReading(buffer); return buffer.Count;
        }
        internal void ResetOverhang() => overhang = Array.Empty<AudioType>();
    }
    public SourceReader Reader { get; private set; } long position; TimeSpan noSyncTime, frameDuration;
    public SDX_SRAudio AudioNode { get; private set; }
    public string FilePath { get; set; }
    /// <summary>Video stream. Has priority over <see cref="FilePath"/>.</summary>
    public System.IO.Stream SourceStream { get; set; }
    public int AudioSampleRate { get; private set; }
    public int AudioChannels { get; private set; }
    public bool HasVideo { get; private set; }
    public bool HasAudio { get; private set; }
    /// <summary>Position, in seconds.</summary>
    public double Position
    {
        get => TimeSpan.FromTicks(position).TotalSeconds; set
        {
            Reader.Flush(SourceReaderIndex.AllStreams);
            Reader.SetCurrentPosition(TimeSpan.FromSeconds(value).Ticks); AudioNode.ResetOverhang();
            while (true)
            {
                using var sample = Reader.ReadSample(SourceReaderIndex.AllStreams, SourceReaderControlFlags.None, out _, out var flags, out position);
                if (flags.HasFlag(SourceReaderFlags.Endofstream) || position + sample.SampleDuration * 2 >= TimeSpan.FromSeconds(value).Ticks) break;
            }
        }
    }
    public SharpDX.DXGI.Rational FPS { get; private set; }
    public int VideoWidth { get; private set; }
    public int VideoHeight { get; private set; }
    /// <summary>Length, in seconds.</summary>
    public double Length { get; private set; }
    public SharpDX.Direct2D1.Bitmap CurrentFrame { get; private set; }
    public SDX_SRVideo(string path = null) { FilePath = path; }
    public bool Loaded { get; private set; }
    public unsafe void Load(SharpDX.Direct2D1.DeviceContext dc)
    {
        Loaded = false;
        if (SourceStream == null && FilePath == null) return;
        using DXGIDeviceManager manager = new();
        manager.ResetDevice(GDX.D3DDevice);
        using var multi = GDX.D3DDevice.QueryInterface<SharpDX.Direct3D.DeviceMultithread>(); multi.SetMultithreadProtected(true);
        using MediaAttributes attributes = new();
        attributes.Set(SourceReaderAttributeKeys.D3DManager, manager);
        attributes.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
        Reader = SourceStream != null ? new(SourceStream, attributes) : new(FilePath, attributes);
        Length = TimeSpan.FromTicks(Reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration)).TotalSeconds;
        position = AudioSampleRate = AudioChannels = 0; HasAudio = HasVideo = false; noSyncTime = TimeSpan.Zero;
        for (int i = 0; ; i++) try
            {
                using MediaType nativeType = Reader.GetNativeMediaType(i, 0);
                using MediaType mType = new();
                var majorType = nativeType.Get(MediaTypeAttributeKeys.MajorType);
                mType.Set(MediaTypeAttributeKeys.MajorType, majorType);
                if (majorType == MediaTypeGuids.Video)
                {
                    HasVideo = true; mType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Argb32);
                    long size = nativeType.Get(MediaTypeAttributeKeys.FrameSize); VideoWidth = (int)(size >> 32); VideoHeight = (int)(size & 0xFFFFFFFF);
                }
                else if (majorType == MediaTypeGuids.Audio)
                {
                    HasAudio = true; mType.Set(MediaTypeAttributeKeys.Subtype, SoundNodes.SDX_Resampler<AudioType>.DetectGuid());
                    AudioSampleRate = nativeType.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond);
                    AudioChannels = nativeType.Get(MediaTypeAttributeKeys.AudioNumChannels);
                }
                Reader.SetCurrentMediaType(i, mType);
            }
            catch (SharpDX.SharpDXException) { break; } //no more streams
        CurrentFrame?.Dispose();
        CurrentFrame = new(dc, new SharpDX.Size2(VideoWidth, VideoHeight), new(new(SharpDX.DXGI.Format.B8G8R8A8_UNorm, SharpDX.Direct2D1.AlphaMode.Premultiplied)));
        using MediaType mType2 = Reader.GetCurrentMediaType(SourceReaderIndex.FirstVideoStream);
        long fpsRatio = mType2.Get(MediaTypeAttributeKeys.FrameRate);
        FPS = new((int)(fpsRatio >> 32), (int)(fpsRatio & 0xFFFFFFFF));

        AudioNode = new(this);
        Loaded = true;
    }
    /// <summary>Video is advanced not by delta time, but by the audio node stream being read.</summary>
    public bool SyncVideoToAudio { get; set; } = true; long videoGoalPos;
    /// <summary>Renders a frame onto <see cref="CurrentFrame"/>.</summary>
    /// <param name="deltaTime">The time to progress the video forward by. If <see cref="SyncVideoToAudio"/> and <see cref="HasAudio"/> are enabled, this parameter does nothing.</param>
    public unsafe void RenderFrame(TimeSpan deltaTime)
    {
        if (!Loaded) return; long duration = 0;
        while (position + duration < videoGoalPos)
        {
            using var sample = Reader.ReadSample(SourceReaderIndex.FirstVideoStream, SourceReaderControlFlags.None, out int stream, out var flags, out position);
            if (sample == null || flags.HasFlag(SourceReaderFlags.Endofstream)) return;
            duration = sample.SampleDuration;
            using var mBuffer = sample.ConvertToContiguousBuffer();
            using var buffer2d = mBuffer.QueryInterface<Buffer2D>();
            byte[] image = new byte[IntPtr.Size];
            buffer2d.Lock2D(image, out int widthBytes);
            fixed (byte* imagePtrPtr = image)
            {
                CurrentFrame.CopyFromMemory(*(IntPtr*)imagePtrPtr, widthBytes);
                buffer2d.Unlock2D();
            }
        }
        //if (!(HasAudio && SyncVideoToAudio && AudioNode.Position <= Position))
            videoGoalPos += deltaTime.Ticks;
    }
    public void Dispose() { Reader.Dispose(); }
}
