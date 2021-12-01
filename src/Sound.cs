using System;
using System.IO;
using System.Windows.Media;
using System.Collections.Generic;
//using Microsoft.DirectX.DirectSound;
using WMPLib;
using CSCore;
using CSCore.SoundOut;
using CSCore.CoreAudioAPI;

namespace Ensoftener.Sound
{
    internal static class SoundGlobal
    {
        public static MMDevice AudioIn { get; private set; }
        public static MMDevice AudioOut { get; private set; }
        public static List<CSWSound> CSWSounds { get; } = new();
        public static void Initialize()
        {
            using MMDeviceEnumerator mmde = new();
            AudioIn = mmde.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            AudioOut = mmde.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            Global.Form.FormClosed += (s, e) =>
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
        public string FilePath { get => Sound.URL; set => Sound.URL = value; }
        public double Volume { get => Sound.settings.volume * 0.01; set => Sound.settings.volume = (int)(value * 100); }
        public double Speed { get => Sound.settings.rate; set => Sound.settings.rate = value; }
        public double Position { get => Sound.controls.currentPosition; set => Sound.controls.currentPosition = value; }
        /// <remarks><b>This value does not work in this class.</b></remarks>
        public double Balance { get => Sound.settings.balance * 0.01; set => Sound.settings.balance = (int)(value * 100); }
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
    }
    /// <summary>A variant of <see cref="MediaPlayer"/> that's ported from WPF to Windows Forms.
    /// You also don't need to specify <b>&lt;UseWPF&gt;true&lt;/UseWPF&gt;</b> in your .csproj file.</summary>
    public class WPFSound : ISoundGeneric
    {
        public MediaPlayer Sound = new();
        /// <summary>The location of the sound. It can be a local file or a website URL.</summary>
        /// <remarks>On Chrome, website sound file will appear as a black page with a small player in the middle. If that's the case, then you've got the sound's URL.</remarks>
        public Uri FilePath { get => Sound.Source; set => Sound.Open(value); }
        public double Volume { get => Sound.Volume; set => Sound.Volume = value; }
        public double Speed { get => Sound.SpeedRatio; set => Sound.SpeedRatio = value; }
        public double Position { get => Sound.Position.TotalSeconds; set => Sound.Position = TimeSpan.FromSeconds(value); }
        public double Balance { get => Sound.Balance; set => Sound.Balance = value; }
        public bool Loop { get; set; }
        public double Length => Sound.NaturalDuration.TimeSpan.TotalSeconds;
        /// <exception cref="FileNotFoundException"/>
        public WPFSound(Uri path)
        {
            if (!File.Exists(path.AbsolutePath)) throw new FileNotFoundException();
            FilePath = path;
            Sound.MediaEnded += (s, e) => { if (Loop) { Position = 0; Play(); } };
        }
        public WPFSound(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();
            FilePath = new(path);
            Sound.MediaEnded += (s, e) => { if (Loop) { Position = 0; Play(); } };
        }
        public void Play() => Sound.Play();
        public void Pause() => Sound.Pause();
        public void Stop() => Sound.Stop();
        public void Dispose() => Sound.Close();
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
    public class CSWSound : ISoundGeneric
    {
        IWaveSource soundIn; public ISoundOut Sound; CSCore.DSP.DmoResampler stretcher, backStretcher;
        string filepath; bool useDS; int Latency; double speed = 1, balance;
        public CSWSoundModifier Modifier { get; private set; }
        public double Volume { get => Sound?.Volume ?? 1; set => Sound.Volume = (float)value; }
        /// <summary>Speed of the sound, with changed pitch. 1 is normal. Changing speed reloads all components (including the source file) and should be used sparingly</summary>
        public double Speed { get => speed; set { speed = value; Initialize(true); } }
        public double Position { get => soundIn.GetPosition().TotalSeconds; set => soundIn.SetPosition(TimeSpan.FromSeconds(value)); }
        public double Balance
        {
            get => balance;
            set
            {
                balance = value;
                Modifier.vLtoL = value > 0 ? 1 - (float)value : 1; Modifier.vLtoR = value > 0 ? (float)value : 0;
                Modifier.vRtoR = value < 0 ? (float)value + 1 : 1; Modifier.vRtoL = value < 0 ? -(float)value : 0;
            }
        }
        public bool Loop { get => Modifier?.loop ?? false; set => Modifier.loop = value; }
        public string FilePath { get => filepath; set { filepath = value; Initialize(true); } }
        public double Length => soundIn.GetLength().TotalSeconds;
        public CSWSound(string file, bool useDirectSound = false, int latency = 100)
        { SoundGlobal.CSWSounds.Add(this); useDS = useDirectSound; Latency = latency; FilePath = file; }
        void Initialize(bool changePath)
        {
            if (Modifier != null) { Modifier.hardStop = true;// while (!Modifier.hardStopResponse) ;
            }
            PlaybackState playing = Sound?.PlaybackState ?? PlaybackState.Stopped;
            long pos = soundIn?.Position ?? 0; bool loop = Loop; double volume = Volume;
            Func<float, float> modifier = Modifier?.Modifier;
            Action<ISampleSource, CSWSoundModifier.CSWSoundSampleData> modifierAdvanced = Modifier?.ModifierAdvanced;
            Sound?.Dispose(); backStretcher?.Dispose(); Modifier?.Dispose(); stretcher?.Dispose();
            if (changePath) { soundIn?.Dispose(); soundIn = CSCore.Codecs.CodecFactory.Instance.GetCodec(FilePath); }
            if (playing == PlaybackState.Playing || playing == PlaybackState.Paused) soundIn.Position = pos;
            stretcher = new(soundIn, (int)(soundIn.WaveFormat.SampleRate * Speed));
            Modifier = new(soundIn.ToSampleSource(), stretcher.ToSampleSource()) { Modifier = modifier, ModifierAdvanced = modifierAdvanced, loop = loop };
            backStretcher = new(Modifier.ToWaveSource(), soundIn.WaveFormat.SampleRate);
            Sound = useDS ? new DirectSoundOut() { Latency = Latency } : new WasapiOut() { Device = SoundGlobal.AudioOut, UseChannelMixingMatrices = true, Latency = Latency };
            Sound.Initialize(backStretcher); Balance = balance; Volume = volume; if (playing == PlaybackState.Playing) Sound.Play();
        }
        public void Pause() => Sound.Pause();
        public void Play() { Modifier.hardStop = false; if (Sound.PlaybackState == PlaybackState.Paused) Sound.Resume(); else Sound.Play(); }
        public void Stop() { Modifier.hardStop = true; Sound?.Stop(); }
        public void Dispose()
        {
            soundIn?.Dispose(); Modifier?.Dispose(); Sound?.Dispose(); stretcher?.Dispose(); backStretcher?.Dispose();
            SoundGlobal.CSWSounds.Remove(this);
        }
        public class CSWSoundModifier : SampleAggregatorBase
        {
            internal float vLtoL = 1, vRtoR = 1, vLtoR = 0, vRtoL = 0;
            internal bool loop, hardStop, hardStopResponse;
            float[] previousBuffer;
            /// <summary>Contains information about the audio stream.</summary>
            public struct CSWSoundSampleData
            {
                /// <summary>The array of samples to be modified for this frame. Because the modifier is called every latency interval,
                /// the array's length is usually the sample rate multiplied by latency (44100 * 0.1s = 4410).</summary>
                public float[] Buffer;
                public float[] PreviousBuffer;
                public int Offset;
                public int Count;
                public int SamplesRead;
                public CSWSoundSampleData(float[] buffer, float[] pBuffer, int offset, int count, int samplesRead)
                { Buffer = buffer; PreviousBuffer = pBuffer; Offset = offset; Count = count; SamplesRead = samplesRead; }
            }
            readonly ISampleSource Source;
            public CSWSoundModifier(ISampleSource source, ISampleSource source2) : base(source2) { Source = source ?? throw new ArgumentNullException(); }
            public override int Read(float[] buffer, int offset, int count)
            {
                if (hardStop) { hardStopResponse = true; return 0; }
                else hardStopResponse = false;
                int samples = Source?.Read(buffer, offset, count) ?? 0;
                ModifierAdvanced?.Invoke(Source, new(buffer, previousBuffer, offset, count, samples));
                float vL, vR;
                for (int i = offset; i < offset + count; i += 2)
                {
                    vL = buffer[i] * vLtoL + buffer[i + 1] * vRtoL; vR = buffer[i] * vLtoR + buffer[i + 1] * vRtoR;
                    buffer[i] = Modifier?.Invoke(vL) ?? vL; buffer[i + 1] = Modifier?.Invoke(vR) ?? vR; 
                }
                previousBuffer = buffer;
                if (loop && this.GetPosition() > this.GetLength() - TimeSpan.FromSeconds(previousBuffer.Length / (double)Source.WaveFormat.SampleRate)) Source.Position = 0;
                return samples;
            }
            /// <summary>Modifies every sound wave that's being played.</summary>
            public Func<float, float> Modifier { get; set; }
            /// <summary>Like <b><see cref="Modifier"/></b>, modifies sound waves that are being played, except that this also recieves all of the streaming data,
            /// including the direct access to the array of samples.</summary>
            /// <remarks>To implement a working modifier, create a for loop that starts at the offset and ends at offset + count.
            /// That way, you modify every relevant sample that actually plays in the speaker.</remarks>
            public Action<ISampleSource, CSWSoundSampleData> ModifierAdvanced { get; set; }
            public override long Position { get => Source.Position; set => Source.Position = value; }
            public override long Length => Source.Length;
        }
    }
}