using System;
using System.IO;
using System.Windows.Media;
using System.Collections.Generic;
using WMPLib;
using CSCore;
using CSCore.CoreAudioAPI;
using NVorbis;

namespace Ensoftener.Sound;

[AttributeUsage(AttributeTargets.Class)] internal class WMPLibAttribute : Attribute { }
[SoundNodes.CSCore] public static class CSCSoundGlobal
{
    public static MMDevice AudioIn { get; private set; }
    public static MMDevice AudioOut { get; private set; }
    internal static HashSet<IDisposable> CSCNodes { get; } = new();
    public static void Initialize()
    {
        using MMDeviceEnumerator mmde = new();
        AudioIn = mmde.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        AudioOut = mmde.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        CSCore.Codecs.CodecFactory.Instance.Register("ogg", new(x => new OggSource(x).ToWaveSource(), "ogg"));
        GShared.OnQuit += (s, e) =>
        {
            CSCNodes.RemoveWhere(x => { x.Dispose(); return true; });
            AudioOut?.Dispose(); AudioIn?.Dispose();
        };
    }
    [SoundNodes.NVorbis]
    public sealed class OggSource : ISampleSource
    {
        readonly Stream Stream; readonly VorbisReader VorbisReader; bool Disposed;
        public OggSource(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream is not readable.", nameof(stream));
            Stream = stream; VorbisReader = new(stream, false); WaveFormat = new(VorbisReader.SampleRate, 32, VorbisReader.Channels, AudioEncoding.IeeeFloat);
        }
        public bool CanSeek => Stream.CanSeek; public WaveFormat WaveFormat { get; }
        public long Length => CanSeek ? (long)(VorbisReader.TotalTime.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels) : 0;
        public long Position
        {
            get => CanSeek ? (long)(VorbisReader.TimePosition.TotalSeconds * VorbisReader.SampleRate * VorbisReader.Channels) : 0;
            set
            {
                if (!CanSeek) throw new InvalidOperationException("NVorbisSource is not seekable.");
                if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value));
                VorbisReader.TimePosition = TimeSpan.FromSeconds((double)value / VorbisReader.SampleRate / VorbisReader.Channels);
            }
        }
        public int Read(float[] buffer, int offset, int count) => VorbisReader.ReadSamples(buffer, offset, count);
        public void Dispose() { if (!Disposed) VorbisReader.Dispose(); else throw new ObjectDisposedException("NVorbisSource"); Disposed = true; }
    }
}
internal interface ISoundGeneric
{
    /// <summary>Volume of the sound between 0 and 1.</summary>
    public double Volume { get; set; } /// <summary>Speed of the sound, without changed pitch. 1 is normal.</summary>
    public double Speed { get; set; } /// <summary>Position of the sound player, in seconds.</summary>
    public double Position { get; set; } /// <summary>Balance of the sound. -1 is on the left speaker, 1 is on the right speaker.</summary>
    public double Balance { get; set; } /// <summary>Looping of the sound.</summary>
    public bool Loop { get; set; } /// <summary>Length of the sound, in seconds.</summary>
    public double Length { get; } public abstract void Play(); public abstract void Pause(); public abstract void Stop(); public void Dispose() { }
}
/// <summary>A simplified version of <see cref="WindowsMediaPlayer"/> that's easier to understand.
/// You also don't need to include the WMPLib namespace which would normally require specifying
/// <b>&lt;UseWindowsForms&gt;true&lt;/UseWindowsForms&gt;</b> in your .csproj file.</summary>
[WMPLib] public sealed class WMPSound : ISoundGeneric
{
    public WindowsMediaPlayer Sound = new();
    /// <summary>The location of the sound. It can be a local file or a website URL.</summary>
    /// <remarks>On Chrome, website sound file will appear as a black page with a small player in the middle. If that's the case, then you've got the sound's URL.</remarks>
    public string FilePath { get => Sound.URL; set => Sound.URL = value; } /// <inheritdoc/>
    public double Volume { get => Sound.settings.volume * 0.01; set => Sound.settings.volume = (int)(value * 100); } /// <inheritdoc/>
    public double Speed { get => Sound.settings.rate; set => Sound.settings.rate = value; } /// <inheritdoc/>
    public double Position { get => Sound.controls.currentPosition; set => Sound.controls.currentPosition = value; }
    /// <remarks><b>This value does not work in this class.</b></remarks>
    public double Balance { get => Sound.settings.balance * 0.01; set => Sound.settings.balance = (int)(value * 100); } /// <inheritdoc/>
    public bool Loop { get => Sound.settings.getMode("loop"); set => Sound.settings.setMode("loop", value); }
    /// <summary>Gets the song's metadata. This method requests all possible keys, so at the end you might get a dictionary of 100 empty keyvalues.</summary>
    public Dictionary<string, string> Metadata
    {
        get
        {
            Dictionary<string, string> info = new();
            for (int i = 0; i < Sound.currentMedia.attributeCount; ++i)
            { string name = Sound.currentMedia.getAttributeName(i); info[name] = Sound.currentMedia.getItemInfo(name); }
            return info;
        }
    }
    /// <summary>Length of the sound, in seconds.</summary><remarks>This value only works when the sound is playing.</remarks>
    public double Length => Sound.currentMedia.duration;
    /// <exception cref="FileNotFoundException"/>
    public WMPSound(string path) { if (!File.Exists(path)) throw new FileNotFoundException(); FilePath = path; Sound.settings.autoStart = false; Stop(); }
    public void Play() => Sound.controls.play();
    public void Pause() => Sound.controls.pause();
    public void Stop() => Sound.controls.stop();
    public bool Playing => Sound.playState == WMPPlayState.wmppsPlaying;
}
/// <summary>A variant of <see cref="MediaPlayer"/> that's ported from WPF to Windows Forms.
/// You also don't need to specify <b>&lt;UseWPF&gt;true&lt;/UseWPF&gt;</b> in your .csproj file.</summary>
public sealed class WPFSound : ISoundGeneric
{
    public MediaPlayer Sound = new();
    /// <summary>The location of the sound. It can be a local file or a website URL.</summary>
    /// <remarks>On Chrome, website sound file will appear as a black page with a small player in the middle. If that's the case, then you've got the sound's URL.</remarks>
    public Uri FilePath { get => Sound.Source; set => Sound.Open(value); } /// <inheritdoc/>
    public double Volume { get => Sound.Volume; set => Sound.Volume = value; } /// <inheritdoc/>
    public double Speed { get => Sound.SpeedRatio; set => Sound.SpeedRatio = value; } /// <inheritdoc/>
    public double Position
    {
        get => Sound.Position.TotalSeconds;
        set => Sound.Clock.Controller.Seek(TimeSpan.FromSeconds(value), System.Windows.Media.Animation.TimeSeekOrigin.BeginTime);
    }/// <inheritdoc/>
    public double Balance { get => Sound.Balance; set => Sound.Balance = value; } /// <inheritdoc/>
    public bool Loop { get; set; } /// <inheritdoc/>
    public double Length => Sound.NaturalDuration.TimeSpan.TotalSeconds;
    /// <exception cref="FileNotFoundException"/>
    public WPFSound(Uri path)
    {
        if (!File.Exists(path.AbsolutePath)) throw new FileNotFoundException();
        FilePath = path; Sound.Clock = new MediaTimeline(path).CreateClock();
        Sound.MediaEnded += (s, e) => { if (Loop) { Position = 0; Play(); } };
    }
    /// <exception cref="FileNotFoundException"/>
    public WPFSound(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException();
        FilePath = new(path); Sound.Clock = new MediaTimeline(new Uri(path)).CreateClock();
        Sound.MediaEnded += (s, e) => { if (Loop) { Position = 0; Play(); } };
    }
    public void Play() => Sound.Clock.Controller.Begin();
    public void Pause() => Sound.Clock.Controller.Pause();
    public void Stop() => Sound.Clock.Controller.Stop();
    public void Dispose() { Sound.Clock.Controller.Remove(); Sound.Close(); }
}