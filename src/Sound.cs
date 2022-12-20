using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Collections.Generic;
//using Microsoft.DirectX.DirectSound;
using WMPLib;
using CSCore;
using CSCore.SoundOut;
using CSCore.CoreAudioAPI;
using CSCore.DSP;
using NVorbis;

namespace Ensoftener.Sound
{
    internal static class SoundGlobal
    {
        public static MMDevice AudioIn { get; private set; }
        public static MMDevice AudioOut { get; private set; }
        public static List<CSCSound> CSWSounds { get; } = new();
        public static void Initialize()
        {
            using MMDeviceEnumerator mmde = new();
            AudioIn = mmde.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            AudioOut = mmde.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            GShared.OnQuit += (s, e) =>
            {
                while (CSWSounds.Count != 0) { CSWSounds[0].Stop(); CSWSounds[0].Dispose(); }
                AudioOut?.Dispose(); AudioIn?.Dispose();
            };
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
    public class WMPSound : ISoundGeneric
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
    public class WPFSound : ISoundGeneric
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
    /*/// <summary>A variant of Microsoft DirectSound's <see cref="SecondaryBuffer"/>.</summary>
    /// <remarks><b>Non-functional on .NET 5, use <see cref="WMPSound"/> or <see cref="WPFSound"/> instead.</b></remarks>
    public class MSDSound : ISoundGeneric
    {
        public SecondaryBuffer Sound; int pausedPos; bool loop; double realFreq;
        public string FilePath { get; set; }
        public double Volume { get => Sound.Volume * 0.01; set => Sound.Volume = (int)(value * 100); }
        public double Speed { get => Sound.Frequency / realFreq; set => Sound.Frequency = (int)(realFreq * value); }
        /// <summary>Position of the sound player, in seconds.</summary>
        /// <remarks>The position is internally converted from seconds to buffer bytes, so it might not be entirely accurate.</remarks>
        public double Position { get => Sound.PlayPosition / realFreq; set { Sound.SetCurrentPosition((int)(value * realFreq)); pausedPos = 0; } }
        public double Balance { get => Sound.Pan * 0.01; set => Sound.Pan = (int)(value * 100); }
        public bool Loop { get => loop; set => loop = value; }
        public MSDSound(string path)
        {
            FilePath = path;
            Sound = new(path, new() { ControlPan = true, ControlVolume = true, CanGetCurrentPosition = true }, SoundGlobal.Device);
            realFreq = Sound.Frequency;
        }
        /// <remarks><b>This value does not work in this class.</b></remarks>
        public double Length => throw new NotImplementedException();
        public void Pause() { pausedPos = Sound.PlayPosition; Sound.Stop(); }
        public void Play()
        {
            if (pausedPos != 0) { Sound.SetCurrentPosition(pausedPos); pausedPos = 0; }
            Sound.Play(0, loop ? BufferPlayFlags.Looping : BufferPlayFlags.Default);
        }
        public void Stop() => Sound.Stop();
        public void Dispose() => Sound.Dispose();
    }*/
    public class CSCSound : ISoundGeneric
    {
        IWaveSource soundIn; public ISoundOut Sound; DmoResampler stretcher, backStretcher;
        PresetMod_ChangeRate mod1; PresetMod_Loop mod2; PresetMod_Panning mod3;
        string filepath; bool useDS, ForceStereo; int Latency; double speed = 1; /// <inheritdoc/>
        public double Volume { get => Sound?.Volume ?? 1; set => Sound.Volume = (float)value; }
        /// <summary>Speed of the sound, with changed pitch. 1 is normal. Changing speed reloads all components (including the file) and should be used sparingly</summary>
        public double Speed { get => speed; set { speed = value; Initialize(true); } } /// <inheritdoc/>
        public double Position { get => soundIn.GetPosition().TotalSeconds; set => soundIn.SetPosition(TimeSpan.FromSeconds(value)); } /// <inheritdoc/>
        public double Balance { get => mod3.Pan; set => mod3.Pan = (float)value; } /// <inheritdoc/>
        public bool Loop { get => mod2.Loop; set => mod2.Loop = value; }
        public string FilePath { get => filepath; set { filepath = value; Initialize(true); } } /// <inheritdoc/>
        public double Length => soundIn.GetLength().TotalSeconds; public bool Stereo => soundIn.WaveFormat.Channels > 1; public int SampleRate => soundIn.WaveFormat.SampleRate;
        /// <exception cref="FileNotFoundException"/><exception cref="NotSupportedException"/>
        public CSCSound(string file, bool useDirectSound = false, int latency = 100, bool forceStereo = true)
        { SoundGlobal.CSWSounds.Add(this); useDS = useDirectSound; Latency = latency; ForceStereo = forceStereo; FilePath = file; }
        void Initialize(bool changePath) //soundIn (W) -> stretcher (W) -> (W>S) -> modifiers (S) -> (S>W) -> backStretcher (W) -> Sound (W)
        {
            PlaybackState playing = Sound?.PlaybackState ?? PlaybackState.Stopped;
            long pos = soundIn?.Position ?? 0; double volume = Volume;
            Sound?.Dispose(); backStretcher?.Dispose(); stretcher?.Dispose();
            if (changePath)
            {
                soundIn?.Dispose();
                FileStream stream = File.OpenRead(FilePath); byte[] header = new byte[4]; stream.Read(header, 0, 4); stream.Position = 0;
                if (header[0] == 'O' && header[1] == 'g' && header[2] == 'g' && header[3] == 'S') soundIn = new OggSource(stream).ToWaveSource();
                else { soundIn = CSCore.Codecs.CodecFactory.Instance.GetCodec(FilePath); stream.Dispose(); }
            }
            if (playing == PlaybackState.Playing || playing == PlaybackState.Paused) soundIn.Position = pos;
            stretcher = new(soundIn, new WaveFormat((int)(soundIn.WaveFormat.SampleRate * Speed) / (Stereo ? 1 : 2), soundIn.WaveFormat.BitsPerSample, 2));
            if (mod1 != null) { mod1.BaseSource.Dispose(); mod1.BaseSource = stretcher.ToSampleSource(); mod1.Source = soundIn.ToSampleSource(); }
            else { mod1 = new(soundIn.ToSampleSource(), stretcher.ToSampleSource()); mod2 = new(mod1); mod3 = new(mod2) { DisposeBaseSource = false }; }
            backStretcher = new(mod3.ToWaveSource(), soundIn.WaveFormat.SampleRate);
            Sound = useDS ? new DirectSoundOut() { Latency = Latency } : new WasapiOut() { Device = SoundGlobal.AudioOut, UseChannelMixingMatrices = true, Latency = Latency };
            Sound.Initialize(backStretcher); Volume = volume; if (playing == PlaybackState.Playing) Sound.Play();
        }
        public void Pause() => Sound.Pause();
        public void Play() { mod1.hardStop = false; if (Sound.PlaybackState == PlaybackState.Paused) Sound.Resume(); else Sound.Play(); }
        public void Stop() { mod1.hardStop = true; Sound?.Stop(); }
        public bool Playing => Sound.PlaybackState == PlaybackState.Playing;
        public void Dispose() { mod2?.Dispose(); Sound?.Dispose(); SoundGlobal.CSWSounds.Remove(this); }
        /// <summary>Plug this into the beginning of your sound modifier chain.</summary>
        public ISampleSource GetModifierInput() => mod3;
        /// <summary>Inserts a modifier before the final sound output. Can be the end of a larger modifier chain.</summary>
        public void SetOutputModifier(ModifierBase modifier)
        {
            //backStretcher.BaseSource.Dispose();
            backStretcher.BaseSource = modifier.ToWaveSource();
        }
        /// <summary>Not designed for instancing. Use <see cref="Modifier"/> or <see cref="ModifierAdvanced"/> instead.</summary>
        public abstract class ModifierBase : SampleAggregatorBase
        {
            internal ISampleSource Source; internal bool hardStop, hardStopResponse;
            /// <summary>To plug in the sound source or another modifier, put <see cref="GetModifierInput"/> in the input or the modifier you want to plug in.</summary>
            public ModifierBase(ISampleSource source) : base(source) { Source = source ?? throw new ArgumentNullException(nameof(source)); }
            public override int Read(float[] buffer, int offset, int count) //count <= buffer.Length
            {
                if (hardStop) { hardStopResponse = true; return 0; } else hardStopResponse = false;
                if (Source == null) return 0; int samples = Source.Read(buffer, offset, count);
                return samples;
            }
            public override long Position { get => Source.Position; set => Source.Position = value; }
            public override long Length => Source.Length;
        }
        /// <summary>Custom operator that modifies sound waves. <b>Must be disposed.</b></summary>
        public class Modifier : ModifierBase
        { /// <inheritdoc/>
            public Modifier(ISampleSource source) : base(source) { }
            public override int Read(float[] buffer, int offset, int count) //count <= buffer.Length
            {
                int samples = base.Read(buffer, offset, count); if (samples == 0) return 0;
                for (int i = offset; i < offset + count; i += 2) if (Operator != null) (buffer[i], buffer[i + 1]) = Operator.Invoke((buffer[i], buffer[i + 1]));
                return samples;
            }
            /// <summary>Modifies every sound wave that's being played, using both left and right channel as inputs and outputs.
            /// If the sound has a single channel, both inputs will be the same.</summary>
            public Func<(float, float), (float, float)> Operator { get; set; }
        }
        /// <summary>Similarly to <see cref="Modifier"/>, modifies sound waves that are being played, except that this also recieves all of the streaming data,
        /// including the direct access to the array of samples. <b>Also must be disposed.</b></summary>
        /// <remarks>To implement a working modifier, create a for loop that starts at the offset and ends at offset + count.
        /// That way, you modify every relevant sample that actually plays in the speaker.</remarks>
        public class ModifierAdvanced : ModifierBase
        {
            /// <summary>Contains information about the audio stream.</summary>
            public struct CSWSoundSampleData
            {
                /// <summary>The array of samples to be modified for this frame. Because the modifier is called every latency interval,
                /// the array's length is usually the sample rate multiplied by latency (44100 * 0.1s = 4410).</summary>
                public float[] Buffer; public int Offset, Count, SamplesRead;
                public CSWSoundSampleData(float[] buffer, int offset, int count, int samplesRead) { Buffer = buffer; Offset = offset; Count = count; SamplesRead = samplesRead; }
            } /// <inheritdoc/>
            public ModifierAdvanced(ISampleSource source) : base(source) { }
            public override int Read(float[] buffer, int offset, int count) //count <= buffer.Length
            { int samples = base.Read(buffer, offset, count); if (samples == 0) return 0; Operator?.Invoke(Source, new(buffer, offset, count, samples)); return samples; }
            public Action<ISampleSource, CSWSoundSampleData> Operator { get; set; }
        }
        public class PresetMod_ChangeRate : Modifier
        { public PresetMod_ChangeRate(ISampleSource source, ISampleSource speed) : base(speed) => Source = source ?? throw new ArgumentNullException(nameof(source)); }
        public class PresetMod_Loop : Modifier
        {
            public bool Loop { get; set; } public PresetMod_Loop(ISampleSource source) : base(source) { }
            public override int Read(float[] buffer, int offset, int count)
            {
                int samples = base.Read(buffer, offset, count);
                if (Loop && Source.Position + samples >= Source.Length && Source.Read(buffer, offset + samples, count - samples) == 0) Source.Position = 0;
                return samples;
            }
        }
        public class PresetMod_Panning : Modifier
        {
            public float Pan { get; set; }
            public PresetMod_Panning(ISampleSource source) : base(source) => Operator += x =>
            (x.Item1 * (Pan > 0 ? 1 - Pan : 1) + x.Item2 * (Pan < 0 ? -Pan : 0), x.Item1 * (Pan > 0 ? Pan : 0) + x.Item2 * (Pan < 0 ? Pan + 1 : 1));
        }
        /// <summary>Improved version of CSCore's filter that supports more channels.</summary>
        public class PresetMod_Filter : ModifierAdvanced
        {
            public Func<BiQuad> Template { get; set; } int channels = 2; BiQuad[] filters = Array.Empty<BiQuad>();
            void Rebuild() { filters = new BiQuad[channels]; for (int i = 0; i < channels; i++) filters[i] = Template(); }
            public double Frequency { get => filters.Length != 0 ? filters[0].Frequency : 0; set { foreach (var f in filters) f.Frequency = value; } }
            public int MaxFrequency { get => filters.Length != 0 ? filters[0].SampleRate : 0; }
            public int Channels { get => channels; set { channels = value; if (filters.Length != value) Rebuild(); } }
            public double Resonance { get => filters.Length != 0 ? filters[0].Q : 0; set { foreach (var f in filters) f.Q = value; } }
            /// <summary>Creates a new modifier using the specified filter.</summary><param name="source">The sound input.</param>
            /// <param name="filter">A method that returns the filter. Example: <code>() => new CSCore.DSP.LowpassFilter(22050, 1000)</code></param>
            public PresetMod_Filter(ISampleSource source, Func<BiQuad> filter) : base(source)
            {
                Template = filter; Rebuild();
                Operator += (s, d) =>
                {
                    for (int i = d.Offset; i < d.Offset + d.Count; i += Channels) for (int j = 0; j < Channels; j++)
                            d.Buffer[i + j] = filters[j].Process(d.Buffer[i + j]);
                };
            }
        }
        public class ModifierMultiInput : ModifierBase
        {
            public List<ISampleSource> Sources { get; } = new();
            public ModifierMultiInput(ISampleSource rate) : base(rate) { }
            public override int Read(float[] buffer, int offset, int count)
            {
                int samples = 0;
                Operator?.Invoke(Sources.Zip(Sources.Select(x => new float[buffer.Length]))
                    .Select(x => { samples = x.First.Read(x.Second, offset, count); return x.Second; }), offset, count);
                return samples;
            }
            /// <summary>The operator that will process the sound input. The first parameter is a list a buffer from all inputs, in the order they were added.
            /// The second parameter is the start offset within the buffer and the third parameter is the sample count.</summary>
            public Action<IEnumerable<float[]>, int, int> Operator { get; set; }
        }
        public class PresetMod_FFT : ModifierAdvanced
        {
            public List<FftProvider> FFT { get; } = new(); int frequencyCount = 128, channels = 1;
            public int FrequencyCount { get => frequencyCount; set { frequencyCount = value; Rebuild(true); } }
            public int Channels { get => channels; set { channels = value; Rebuild(false); } }
            public PresetMod_FFT(ISampleSource source) : base(source)
            {
                Operator += (s, d) =>
                { for (int i = d.Offset; i < d.Offset + d.Count; i += Channels) for (int j = 0; j < Channels; j++) FFT[j].Add(d.Buffer[i + j], d.Buffer[i + j]); };
            }
            void Rebuild(bool clear)
            {
                if (clear) FFT.Clear(); else if (Channels < FFT.Count) FFT.RemoveRange(Channels, FFT.Count - Channels);
                for (int i = FFT.Count; i < Channels; i++) FFT.Add(new(1, (FftSize)Math.Pow(2, Math.Floor(Math.Log2(FrequencyCount)))));
            }
            public IEnumerable<float[]> ChannelsFrequencies() => FFT.Select(x => FFTFrequencies(x));
            public float[] FFTFrequencies(FftProvider fft)
            { CSCore.Utils.Complex[] f = new CSCore.Utils.Complex[FrequencyCount]; fft.GetFftData(f); return f.Select(x => (float)x.Value).ToArray(); }
            public float[] ChannelFrequencies(int channel) => FFTFrequencies(FFT[channel]);
        }
    }
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