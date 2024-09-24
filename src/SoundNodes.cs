using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.MediaFoundation;
using CSCore;
using CSCore.DSP;
using CSC_WFormat = CSCore.WaveFormat;
using MDS_WFormat = SharpDX.Multimedia.WaveFormat;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Ensoftener.SoundNodes;

[AttributeUsage(AttributeTargets.Class)] internal class NVorbisAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Class)] internal class CSCoreAttribute : Attribute { }
#region generic
public record class SNBuffer<AType>
{
    public AType[] Samples; public int Offset, Count, Channels;
    public SNBuffer(AType[] samples, int offset, int count, int channels) { Samples = samples; Offset = offset; Count = count; Channels = channels; }
}
public interface ISoundNode<AType> : IDisposable
{
    public int ReadSamples(SNBuffer<AType> buffer); public SNBuffer<AType> SeekSamples(); public event Action<SNBuffer<AType>> OnRead, OnSeek;
    public int SampleRate { get; } public int Channels { get; }
}
public interface ISoundNodeOut<AType> : ISoundNode<AType> { public void Play(); public void Stop(); public bool IsPlaying { get; } }
public interface ISoundNodeDecoder<AType> : ISoundNode<AType>
{
    public long SampleCount { get; } public double Position { get; set; } public long SamplePosition { get; set; } public bool Loop { get; set; }
    /// <summary>Called when the stream ends, including the buffer and the position in the buffer (the buffer offset does not count).
    /// Can be called multiple times if the sample was shorter than the buffer.</summary>
    public event Action<SNBuffer<AType>, long> OnStreamEnd;
}
public class NodeGeneric<AType> : ISoundNode<AType>
{
    public event Action<SNBuffer<AType>> OnRead, OnSeek; SNBuffer<AType> lastBuffer;
    protected virtual SNBuffer<AType> LastBuffer => lastBuffer;
    public virtual void Dispose() { }
    public virtual int ReadSamples(SNBuffer<AType> buffer) { FinishReading(buffer); return buffer.Count; }
    protected void FinishReading(SNBuffer<AType> buffer) { OnRead?.Invoke(lastBuffer = buffer); }
    public virtual SNBuffer<AType> SeekSamples() { var buffer = LastBuffer; OnSeek?.Invoke(buffer); return buffer; }
    public virtual int SampleRate { get; }
    public virtual int Channels { get; }
}
public class NodeSingleInput<AType> : NodeGeneric<AType>
{
    public virtual ISoundNode<AType> Input { get; set; }
    protected override SNBuffer<AType> LastBuffer => base.LastBuffer ?? Input.SeekSamples();
    public override int SampleRate => Input.SampleRate; public override int Channels => Input.Channels;
    public override int ReadSamples(SNBuffer<AType> buffer)
    { if (Input == null) return 0; int read = Input.ReadSamples(buffer); buffer.Count = read; FinishReading(buffer); return read; }
}
public class NodeSingleSample<AType> : NodeSingleInput<AType>
{
    /// <summary>Note: the float array returns <b>samples per channel, not the entire buffer</b>.</summary>
    public Action<Memory<AType>> Operator { get; set; }
    public NodeSingleSample() => OnRead += b =>
    {
        if (Operator == null) return; float[] channels = new float[b.Channels];
        for (int i = b.Offset; i < b.Offset + b.Count; i += b.Channels) Operator(new(b.Samples, i, b.Channels));
    };
}
public class NodePan<AType> : NodeSingleSample<AType>
{
    public float Pan { get; set; }
    public NodePan() => Operator += x =>
    {
        if (x.Length == 2)
            if (x is Memory<float> f)
            {
                var s = f.Span;
                (s[0], s[1]) = (s[0] * (Pan > 0 ? 1 - Pan : 1) + s[1] * (Pan < 0 ? -Pan : 0), s[0] * (Pan > 0 ? Pan : 0) + s[1] * (Pan < 0 ? Pan + 1 : 1));
            }
            else if (x is Memory<short> h)
            {
                var s = h.Span;
                (s[0], s[1]) = ((short)(s[0] * (Pan > 0 ? 1 - Pan : 1) + s[1] * (Pan < 0 ? -Pan : 0)), (short)(s[0] * (Pan > 0 ? Pan : 0) + s[1] * (Pan < 0 ? Pan + 1 : 1)));
            }
    };
}
/// <summary>Converts from <see cref="short"/> to <see cref="float"/> or from <see cref="float"/> to <see cref="short"/>.</summary>
public class NodeFormatType<TOld, TNew> : NodeGeneric<TNew>
{
    public ISoundNode<TOld> Input { get; set; }
    SNBuffer<TOld> Buffer;
    public override int SampleRate => Input.SampleRate;
    public override int Channels => Input.Channels;
    public override unsafe int ReadSamples(SNBuffer<TNew> buffer)
    {
        if (Buffer == null || Buffer.Samples.Length < buffer.Count) Buffer = new(new TOld[buffer.Count], 0, buffer.Count, buffer.Channels);
        int smallestRead = Input.ReadSamples(Buffer);
        if (buffer is SNBuffer<float> f1 && Buffer is SNBuffer<short> s1)
        {
            int i = 0;
            fixed (float* fp = f1.Samples) fixed (short* sp = s1.Samples)
            {
                if (Avx2.IsSupported)
                {
                    var f32768 = Vector256.Create(1 / 32768f);
                    for (; i + 8 <= buffer.Count; i += 8)
                    {
                        var s0 = Sse2.LoadVector128(sp + i);
                        Avx.Store(fp + buffer.Offset + i, Avx.Multiply(Avx.ConvertToVector256Single(Avx2.UnpackLow(Avx2.Permute4x64(s0.AsUInt64().ToVector256(), 0b00100011).AsInt16(),
                            Avx2.Permute4x64(Sse2.CompareLessThan(s0, Vector128<short>.Zero).AsUInt64().ToVector256(), 0b00100011).AsInt16()).AsInt32()), f32768));
                    }
                }
                if (Sse2.IsSupported)
                {
                    var f32768 = Vector128.Create(1 / 32768f);
                    for (; i + 8 <= buffer.Count; i += 8)
                    {
                        var s0 = Sse2.LoadVector128(sp + i);
                        var s01 = Sse2.CompareLessThan(s0, Vector128<short>.Zero);
                        Sse.Store(fp + buffer.Offset + i, Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.UnpackLow(s0, s01).AsInt32()), f32768));
                        Sse.Store(fp + buffer.Offset + i + 4, Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.UnpackHigh(s0, s01).AsInt32()), f32768));
                    }
                }
                for (; i < buffer.Count; i++) f1.Samples[i] = s1.Samples[i] / 32768f;
            }
        }
        else if (buffer is SNBuffer<short> s2 && Buffer is SNBuffer<float> f2)
        {
            int i = 0;
            fixed (float* fp = f2.Samples) fixed (short* sp = s2.Samples)
            {
                if (Avx2.IsSupported)
                {
                    var f32768 = Vector256.Create(32768f);
                    var bshuffle = Vector256.Create(2, 3, 6, 7, 10, 11, 14, 15, 18, 19, 22, 23, 26, 27, 30, 31, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
                    for (; i + 8 <= buffer.Count; i += 8)
                    {
                        var f0 = Avx.LoadVector256(fp + i);
                        Sse2.Store(sp + buffer.Offset + i, Avx2.Shuffle(Avx.ConvertToVector256Int32(Avx.Multiply(f0, f32768)).AsSByte(), bshuffle).GetLower().AsInt16());
                    }
                }
                if (Sse2.IsSupported)
                {
                    var f32768 = Vector128.Create(32768f);
                    var bshuffle = Vector128.Create(2, 3, 6, 7, 10, 11, 14, 15, -1, -1, -1, -1, -1, -1, -1, -1);
                    for (; i + 8 <= buffer.Count; i += 8)
                    {
                        Sse2.Store(sp + buffer.Offset + i, Sse2.PackSignedSaturate(Sse2.ConvertToVector128Int32(Sse.Multiply(Sse.LoadVector128(fp + i), f32768)),
                                                                                   Sse2.ConvertToVector128Int32(Sse.Multiply(Sse.LoadVector128(fp + i + 4), f32768))));
                    }
                }
            }
            for (; i < buffer.Count; i++) s2.Samples[i] = (short)(f2.Samples[i] * 32768f);
        }
        FinishReading(buffer); return smallestRead;
    }
}
public class NodeFilter : NodeSingleInput<float>
{
    double[] Z1, Z2; double A0, A1, A2, B1, B2, q, _gainDB, frequency;
    public double Frequency { get => frequency; set { if (value * 2 <= SampleRate) { frequency = value; valueChanged = true; } } }
    public double Resonance { get => q; set { if (value > 0) { q = value; valueChanged = true; } } }
    public double GainDB { get => _gainDB; set { _gainDB = value; valueChanged = true; } }
    public float Process(float input, int channel)
    { double num = (double)input * A0 + Z1[channel]; Z1[channel] = (double)input * A1 + Z2[channel] - B1 * num; Z2[channel] = (double)input * A2 - B2 * num; return (float)num; }
    public enum FilterType { LowPass, HighPass, LowShelf, HighShelf, Notch, Peak };
    void Rebuild()
    {
        double k = Math.Tan(Math.PI * Frequency / SampleRate), v = Math.Pow(10, Math.Abs(GainDB) / 20.0), norm = 1 / (1 + k / q + k * k), sv2 = Math.Sqrt(2 * v);
        const double sqrt2 = 1.4142135623730951;
        switch (type)
        {
            case FilterType.LowPass: A0 = k * k * norm; A1 = 2 * A0; A2 = A0; B1 = 2 * (k * k - 1) * norm; B2 = (1 - k / q + k * k) * norm; break;
            case FilterType.HighPass: A0 = 1 * norm; A1 = -2 * A0; A2 = A0; B1 = 2 * (k * k - 1) * norm; B2 = (1 - k / q + k * k) * norm; break;
            case FilterType.LowShelf:
                if (GainDB >= 0)
                {    // boost
                    norm = 1 / (1 + sqrt2 * k + k * k); A0 = (1 + sv2 * k + v * k * k) * norm; A1 = 2 * (v * k * k - 1) * norm;
                    A2 = (1 - sv2 * k + v * k * k) * norm; B1 = 2 * (k * k - 1) * norm; B2 = (1 - sqrt2 * k + k * k) * norm;
                }
                else
                {    // cut
                    norm = 1 / (1 + sv2 * k + v * k * k); A0 = (1 + sqrt2 * k + k * k) * norm; A1 = 2 * (k * k - 1) * norm;
                    A2 = (1 - sqrt2 * k + k * k) * norm; B1 = 2 * (v * k * k - 1) * norm; B2 = (1 - sv2 * k + v * k * k) * norm;
                }
                break;
            case FilterType.HighShelf:
                if (GainDB >= 0)
                {    // boost
                    norm = 1 / (1 + sqrt2 * k + k * k); A0 = (v + sv2 * k + k * k) * norm; A1 = 2 * (k * k - v) * norm;
                    A2 = (v - sv2 * k + k * k) * norm; B1 = 2 * (k * k - 1) * norm; B2 = (1 - sqrt2 * k + k * k) * norm;
                }
                else
                {    // cut
                    norm = 1 / (v + sv2 * k + k * k); A0 = (1 + sqrt2 * k + k * k) * norm; A1 = 2 * (k * k - 1) * norm;
                    A2 = (1 - sqrt2 * k + k * k) * norm; B1 = 2 * (k * k - v) * norm; B2 = (v - sv2 * k + k * k) * norm;
                }
                break;
            case FilterType.Notch: A0 = (1 + k * k) * norm; A1 = 2 * (k * k - 1) * norm; A2 = A0; B1 = A1; B2 = (1 - k / q + k * k) * norm; break;
            case FilterType.Peak:
                if (GainDB >= 0) //boost
                { A0 = (1 + v / q * k + k * k) * norm; A1 = 2 * (k * k - 1) * norm; A2 = (1 - v / q * k + k * k) * norm; B1 = A1; B2 = (1 - 1 / q * k + k * k) * norm; }
                else //cut
                {
                    norm = 1 / (1 + v / q * k + k * k); A0 = (1 + 1 / q * k + k * k) * norm; A1 = 2 * (k * k - 1) * norm;
                    A2 = (1 - 1 / q * k + k * k) * norm; B1 = A1; B2 = (1 - v / q * k + k * k) * norm;
                }
                break;
            default:
                break;
        }
    }
    bool countChanged, valueChanged, templateChanged; int channels = 2; FilterType type;
    public FilterType Type { get => type; set { type = value; Rebuild(); } }
    public NodeFilter()
    {
        countChanged = true;
        OnRead += b =>
        {
            if (channels != b.Channels) countChanged = true;
            if (valueChanged || countChanged || templateChanged)
            { if (countChanged) { Z1 = new double[channels]; Z2 = new double[channels]; } Rebuild(); countChanged = valueChanged = templateChanged = false; }
            for (int i = b.Offset; i < b.Offset + b.Count; i += b.Channels) for (int j = 0; j < b.Channels; j++) b.Samples[i + j] = Process(b.Samples[i + j], j);
        };
    }
}
public class NodeMixtape : NodeSingleInput<float>
{
    public override int ReadSamples(SNBuffer<float> buffer)
    {
        int result = Input.ReadSamples(buffer);
        TimeSpan elapsed;
        for (int i = 0; i < buffer.Count; i++)
        {
            elapsed = TimeSpan.FromSeconds((double)i / SampleRate);
            bool[] correct = Tracks.Select(x => i % buffer.Channels == i % x.Channels && x.Position >= 0).ToArray();
            for (int t = 0; t < Tracks.Count; t++)
            {
                if (correct[t] && Tracks[t].Feedback > 0) buffer.Samples[i] += Tracks[t].Buffer[Tracks[t].Position] * Tracks[t].Feedback;
            }
            for (int t = 0; t < Tracks.Count; t++) if (correct[t]) switch (Tracks[t].State)
                    {
                        case Track.TrackState.Playing or Track.TrackState.PlayingReverse: buffer.Samples[i] += Tracks[t].Buffer[Tracks[t].Position]; break;
                        case Track.TrackState.Recording:
                            if (Tracks[t].Position < Tracks[t].Buffer.Count) Tracks[t].Buffer[Tracks[t].Position] += buffer.Samples[i];
                            else Tracks[t].Buffer.Add(buffer.Samples[i]);
                            break;
                        default: break;
                    }
            for (int t = 0; t < Tracks.Count; t++)
            {
                if (correct[t] && Tracks[t].Position < Tracks[t].Buffer.Count) buffer.Samples[i] += Tracks[t].Buffer[Tracks[t].Position] * (1 - Tracks[t].Feedback);
                switch (Tracks[t].State)
                {
                    case Track.TrackState.Playing: Tracks[t].Position++; if (Tracks[t].Position >= Tracks[t].Buffer.Count) Tracks[t].State = Track.TrackState.Stopped; break;
                    case Track.TrackState.PlayingReverse: Tracks[t].Position--; if (Tracks[t].Position < 0) Tracks[t].Position = Tracks[t].Buffer.Count; break;
                    case Track.TrackState.Recording: Tracks[t].Position++; break;
                    default: break;
                }
                if (i % Tracks[t].Channels == Tracks[t].Channels - 1) for (int j = 0; j < Tracks[t].scheduleState.Queue.Count; j++)
                    {
                        if (Tracks[t].scheduleState.Queue[j].Item1 > elapsed)
                        {
                            Tracks[t].State = Tracks[t].scheduleState.Queue[j].Item2;
                            Tracks[t].scheduleState.Queue.RemoveAt(j--);
                        }
                    }
            }
        }
        elapsed = TimeSpan.FromSeconds((double)buffer.Count / SampleRate);
        for (int t = 0; t < Tracks.Count; t++)
        {
            Tracks[t].scheduleState.Update(elapsed);
            Tracks[t].scheduleFeedback.Update(elapsed);
        }
        FinishReading(buffer); return result;
    }
    public void NewTrackRecord(TimeSpan delay)
    { Recordings.Add(new()); Tracks.Add(new() { Buffer = Recordings[^1] }); Tracks[^1].scheduleState.Schedule(delay, Track.TrackState.Recording); }
    public void NewTrackPlay(TimeSpan delay) { Tracks.Add(new()); Tracks[^1].scheduleState.Schedule(delay, Track.TrackState.Playing); }
    public void NewTrackPlayReverse(TimeSpan delay) { Tracks.Add(new()); Tracks[^1].scheduleState.Schedule(delay, Track.TrackState.PlayingReverse); }
    public List<Track> Tracks { get; } = new();
    public List<List<float>> Recordings { get; } = new();
    public class Track
    {
        public Scheduler<TrackState> scheduleState = new(); public Scheduler<double> scheduleFeedback = new();
        public float Feedback { get; set; }
        public enum TrackState { Stopped, Playing, PlayingReverse, Recording }
        public TrackState State { get; set; }
        public int Position { get; set; }
        public List<float> Buffer { get; set; }
        public int Channels { get; set; }
        public void Play(TimeSpan delay) { scheduleState.Schedule(delay, TrackState.Playing); }
        public void Record(TimeSpan delay) { scheduleState.Schedule(delay, TrackState.Recording); }
        public void Stop(TimeSpan delay) { scheduleState.Schedule(delay, TrackState.Stopped); }
        public void PlayReverse(TimeSpan delay) { scheduleState.Schedule(delay, TrackState.PlayingReverse); }
    }
    public class Scheduler<T>
    {
        internal List<(TimeSpan, T)> Queue = new();
        public void Schedule(TimeSpan timeSpan, T value) => Queue.Add((timeSpan, value));
        internal void Update(TimeSpan elapsed)
        {
            for (int i = 0; i < Queue.Count; i++)
            {
                if (Queue[i].Item1 < elapsed) Queue.RemoveAt(i); else Queue[i] = (Queue[i].Item1 - elapsed, Queue[i].Item2);
            }
        }
    }
}
/// <summary>Skips zero-level samples at the start of an audio file.</summary>
public class Node_ZLSSkip<AType> : NodeSingleInput<AType>
{
    /// <summary>The amount of milliseconds to skip at the start of a file. 0 to disable, -1 to detect the first non-zero sample.</summary>
    /// <remarks>MP3 files always have a short span of zero level samples at the start.
    /// The length depends on the codec and program being used and can sometimes be unpredictable.</remarks>
    public double SkipMsStart { get; set; }
    /// <summary>The maximum sample amplitude that will be skipped. 0.02 by default.</summary>
    /// <remarks>MP3 paddings generally don't contain perfect silence. The noise within slightly becomes louder as it gets closer to the actual audio,
    /// peaking at about 0.02-0.05 (again, these varies between codecs and programs). In extremely compressed MP3s with loud paddings, it's more beneficial to:
    /// <br/>a) mark the start of the real sound with a loud sample (kick, drawing it in Audacity etc.) and then setting the toleance to somewhere in-between,
    /// <br/>b) know the start of the audio beforehand and set <see cref="SkipMsStart"/> to the offset in milliseconds.</remarks>
    public float Tolerance { get; set; } = 0.02f;
    /// <summary>The very first, unaltered audio stream in the stream chain.</summary>
    public ISoundNodeDecoder<AType> First { get; set; }
    bool once, skip;
    public Node_ZLSSkip(ISoundNodeDecoder<AType> first)
    {
        First = first; OnRead += d =>
        {
            if (once && First.Position - d.Count * First.Channels <= 0) Skip();
            if (once && skip)
            {
                once = false; skip = false;
                if (SkipMsStart == -1)
                {
                    int index = d.Offset; long pos = First.SamplePosition;
                    if (d.Samples is float[] f)
                    {
                        while (MathF.Abs(f[index++]) < Tolerance) if (index == d.Offset + d.Count)
                            { Input.ReadSamples(d); First.Position = pos += d.Count; index = d.Offset; }
                    }
                    else if (d.Samples is short[] s)
                    {
                        while (MathF.Abs(s[index++]) < Tolerance * short.MaxValue) if (index == d.Offset + d.Count)
                            { Input.ReadSamples(d); First.Position = pos += d.Count; index = d.Offset; }
                    }
                    index -= First.Channels - index % First.Channels;
                    for (int i = 0; i < d.Count - index; i++) d.Samples[i] = d.Samples[index + i]; Input.ReadSamples(new(d.Samples, d.Offset + index, d.Count - index, d.Channels));
                }
                else if (SkipMsStart != 0 && SkipMsStart * First.SampleRate * 0.001 * First.Channels < First.SampleCount)
                {
                    First.Position = (long)(SkipMsStart * 0.001 * First.SampleRate * First.Channels); Input.ReadSamples(d);
                }
            }
            else once = true;
        };
    }
    /// <summary>Skip all zero-level samples from now on to the next non-zero level sample.</summary>
    public void Skip() => skip = true;
}
#endregion
#region CSCore
[CSCore] public abstract class CSC_Generic<AType> : NodeGeneric<AType>
{
    public IWaveSource Stream => OutStream; private protected CSC_Stream<AType> OutStream;
    public CSC_Generic() { Sound.CSCSoundGlobal.CSCNodes.Add(this); OutStream = new(null) { OnRead = b => ReadSamples(b), Owner = this }; }
    public override void Dispose() { base.Dispose(); Sound.CSCSoundGlobal.CSCNodes.Remove(this); Stream?.Dispose(); }
    public static AudioEncoding Encoding() => typeof(AType) == typeof(float) ? AudioEncoding.IeeeFloat : typeof(AType) == typeof(short) ? AudioEncoding.Pcm : AudioEncoding.Unknown;
}
[CSCore] class CSC_Stream<AType> : IWaveSource
{
    public IReadableAudioSource<AType> Source; public Func<SNBuffer<AType>, int> OnRead; public CSC_Generic<AType> Owner;
    public CSC_Stream(IReadableAudioSource<AType> source) { Source = source; OnRead = b => Source.Read(b.Samples, b.Offset, b.Count); }
    public int Read(byte[] buffer, int offset, int count)
    {
        if (offset % Owner.Channels != 0) offset -= offset % Owner.Channels;
        if (count % Owner.Channels != 0) count -= count % Owner.Channels;
        AType[] b = new AType[count];
        Buffer.BlockCopy(buffer, offset * Marshal.SizeOf<AType>(), b, 0, count * Marshal.SizeOf<AType>());
        return OnRead(new(b, offset, count, Owner.Channels));
    }
    public void Dispose() { }
    public long Position { get => Source.Position / Marshal.SizeOf<AType>(); set => Source.Position = value * Marshal.SizeOf<AType>(); }
    public long Length => Source?.Length / Marshal.SizeOf<AType>() ?? 0;
    public bool CanSeek => true;
    public CSC_WFormat WaveFormat => new(Owner.SampleRate, 32, Owner.Channels, AudioEncoding.IeeeFloat);
}
/// <summary>File decoder using CSCore.</summary>
[CSCore] public class CSC_File<AType> : CSC_Generic<AType>, ISoundNodeDecoder<AType>
{
    IWaveSource soundIn; DmoResampler resampler; CSCore.Streams.LoopStream loop; IReadableAudioSource<AType> soundOut; string filepath;
    public event Action<SNBuffer<AType>, long> OnStreamEnd;
    public CSC_File(string file) { FilePath = file; }
    public double Position { get => soundIn.GetPosition().TotalSeconds; set => soundIn.SetPosition(TimeSpan.FromSeconds(value)); }
    public long SampleCount => soundIn.Length; public long SamplePosition { get => soundIn.Position; set => soundIn.Position = value; }
    public bool Loop { get => loop.EnableLoop; set => loop.EnableLoop = value; }
    public string FilePath { get => filepath; set { filepath = value; Initialize(true); } }
    public override int SampleRate => soundIn.WaveFormat.SampleRate;
    public override int Channels => soundIn.WaveFormat.Channels;
    void Initialize(bool changePath) //soundIn -> resampler -> loop -> soundOut
    {
        if (changePath) { soundIn?.Dispose(); soundIn = CSCore.Codecs.CodecFactory.Instance.GetCodec(FilePath); }
        resampler = new(soundIn, new(soundIn.WaveFormat.SampleRate, 32, soundIn.WaveFormat.Channels, Encoding()), true);
        if (loop != null) loop.BaseSource = resampler; else { loop = new(resampler); soundOut = (IReadableAudioSource<AType>)loop; }
    }
    public override int ReadSamples(SNBuffer<AType> buffer)
    {
        int result = soundOut.Read(buffer.Samples, buffer.Offset, buffer.Count); buffer.Channels = soundIn.WaveFormat.Channels; FinishReading(buffer);
        for (long i = SampleCount - SamplePosition; i < buffer.Count; i += SampleCount)
            OnStreamEnd?.Invoke(buffer, i);
        return result;
    }
    public override void Dispose() { base.Dispose(); loop?.Dispose(); soundIn?.Dispose(); }
}
[CSCore] public class CSC_MultiInput<AType> : CSC_Generic<AType>
{
    public int CustomRate, CustomChannels;
    public override int SampleRate => CustomRate == 0 ? inputs[0].Item1.SampleRate : CustomRate;
    public override int Channels => CustomChannels == 0 ? inputs[0].Item1.Channels : CustomChannels;
    public event Action<SNBuffer<AType>[], SNBuffer<AType>> ProcessBuffers;
    public IEnumerable<ISoundNode<AType>> Inputs => inputs.Select(x => x.Item1); List<(ISoundNode<AType>, CSC_Stream<AType>, DmoResampler)> inputs = new();
    public void AddInput(ISoundNode<AType> input)
    {
        CSC_Stream<AType> stream = new(null) { OnRead = buffer => input.ReadSamples(buffer) };
        DmoResampler dmo = new(stream, new CSC_WFormat(SampleRate, 32, Channels, Encoding()));
        inputs.Add((input, stream, dmo));
    }
    public ISoundNode<AType> this[int index]
    {
        get => inputs[index].Item1; set
        { var i = inputs[index]; i.Item1 = value; inputs[index] = i; i.Item2.OnRead = buffer => value.ReadSamples(buffer); }
    }
    public void RemoveInput(int index)
    {
        inputs[index].Item2.Dispose(); inputs[index].Item3.Dispose(); inputs.RemoveAt(index);
    }
    public override int ReadSamples(SNBuffer<AType> buffer)
    {
        int smallestRead = int.MaxValue;
        ProcessBuffers?.Invoke(inputs.Select(x =>
        {
            var buffer2 = buffer with { Samples = new AType[buffer.Samples.Length] };
            smallestRead = Math.Min(smallestRead, ((IReadableAudioSource<AType>)x.Item3).Read(buffer2.Samples, buffer.Offset, buffer.Count)); return buffer2;
        }).ToArray(), buffer); FinishReading(buffer); return smallestRead;
    }
    public override void Dispose() { base.Dispose(); for (int i = 0; i < inputs.Count; i++) { inputs[i].Item2.Dispose(); inputs[i].Item2.Dispose(); } }
}
[CSCore] public class CSC_Speed<AType> : CSC_Generic<AType>
{
    public ISoundNode<AType> Input { get; set; } DmoResampler resampler; double speed; System.Threading.ManualResetEvent isRead;
    public double Speed
    {
        get => speed; set
        {
            speed = value; resampler?.Dispose();
            resampler = new(Stream, new CSC_WFormat((int)(SampleRate / value), 32, Channels, Encoding())) { DisposeBaseSource = false };
            isRead.WaitOne(); 
        }
    }
    public override int SampleRate => Input.SampleRate;
    public override int Channels => Input.Channels;
    public CSC_Speed(ISoundNode<AType> input, double speed)
    {
        Input = input; isRead = new(true); Speed = speed;
        OutStream.OnRead = buffer => Input.ReadSamples(buffer);
    }
    public override int ReadSamples(SNBuffer<AType> buffer)
    {
        isRead.Reset(); int result = ((IReadableAudioSource<AType>)resampler).Read(buffer.Samples, buffer.Offset, buffer.Count); isRead.Set(); FinishReading(buffer); return result;
    }
    public override void Dispose()
    {
        base.Dispose(); isRead.WaitOne(1000, true); resampler?.Dispose(); isRead.Close(); isRead.Dispose();
    }
}
[CSCore] public class CSC_FFT : CSC_Generic<float>
{
    public ISoundNode<float> Input { get; set; }
    public override int SampleRate => Input.SampleRate;
    public override int Channels => Input.Channels;
    public List<FftProvider> FFT { get; } = new(); int frequencyCount = 128, channels = 2;
    public int FrequencyCount { get => frequencyCount; set { frequencyCount = value; Rebuild(true); } }
    public CSC_FFT()
    {
        OnRead += b =>
        {
            if (channels != b.Channels) { Rebuild(false); channels = b.Channels; }
            for (int i = b.Offset; i < b.Offset + b.Count; i += b.Channels)
                for (int j = 0; j < b.Channels; j++) FFT[j].Add(b.Samples[i + j], b.Samples[i + j]);
        };
    }
    public override SNBuffer<float> SeekSamples()
    {
        var b = base.SeekSamples(); if (b == null) return b;
        if (channels != b.Channels) { Rebuild(false); channels = b.Channels; }
        for (int i = b.Offset; i < b.Offset + b.Count; i += b.Channels)
            for (int j = 0; j < b.Channels; j++) FFT[j].Add(b.Samples[i + j], b.Samples[i + j]);
        return b;
    }
    void Rebuild(bool clear)
    {
        if (clear) FFT.Clear(); else if (channels < FFT.Count) FFT.RemoveRange(channels, FFT.Count - channels);
        for (int i = FFT.Count; i < channels; i++) FFT.Add(new(1, (FftSize)Math.Pow(2, Math.Floor(Math.Log2(FrequencyCount)))));
    }
    public IEnumerable<float[]> ChannelsFrequencies() => FFT.Select(x => FFTFrequencies(x));
    public float[] FFTFrequencies(FftProvider fft) { float[] f = new float[FrequencyCount]; fft.GetFftData(f); return f; }
    public float[] ChannelFrequencies(int channel) => FFTFrequencies(FFT[channel]);
    public override int ReadSamples(SNBuffer<float> buffer) { int result = Input.ReadSamples(buffer); FinishReading(buffer); return result; }
}
/// <summary>WASAPI or DirectSound output using CSCore.</summary>
[CSCore] public class CSC_Out<AType> : CSC_Generic<AType>, ISoundNodeOut<AType>
{
    ISoundNode<AType> source; public CSCore.SoundOut.ISoundOut SoundOut;
    public ISoundNode<AType> Input { get => source; set { source = value; } }
    public override int SampleRate => Input.SampleRate;
    public override int Channels => Input.Channels;
    public CSC_Out(ISoundNode<AType> source, bool useDirectSound = false, int latency = 100)
    {
        SoundOut = useDirectSound ? new CSCore.SoundOut.DirectSoundOut { Latency = latency } :
            new CSCore.SoundOut.WasapiOut { Device = Sound.CSCSoundGlobal.AudioOut, UseChannelMixingMatrices = true, Latency = latency };
        Input = source; SoundOut.Initialize(Stream);
    }
    public override int ReadSamples(SNBuffer<AType> buffer) { int result = Input.ReadSamples(buffer); FinishReading(buffer); return result; }
    protected override SNBuffer<AType> LastBuffer => base.LastBuffer ?? Input.SeekSamples();
    public override void Dispose() { base.Dispose(); SoundOut?.Dispose(); }
    public void Play()
    {
        if (SoundOut.PlaybackState == CSCore.SoundOut.PlaybackState.Paused) SoundOut.Resume();
        else if (SoundOut.PlaybackState != CSCore.SoundOut.PlaybackState.Playing) SoundOut.Play();
    }
    public void Pause() => SoundOut.Pause(); public bool IsPlaying => SoundOut.PlaybackState == CSCore.SoundOut.PlaybackState.Playing;
    public void Stop() => SoundOut.Stop();
}
/// <summary>Xaudio 2 output using CSCore.</summary>
[CSCore] public class CSC_XA2Out<AType> : CSC_Generic<AType>, ISoundNodeOut<AType> where AType : struct
{
    public ISoundNode<AType> Input; CSCore.XAudio2.XAudio2SourceVoice InputVoice; CSCore.XAudio2.XAudio2MasteringVoice SoundOut; CSCore.XAudio2.VoiceCallback callbacks;
    CSCore.XAudio2.XAudio2Buffer[] audioBuffers; int currentBuffer; System.Threading.AutoResetEvent onBufferEnd;
    public event Action<SNBuffer<AType>> OnBufferPlayed; System.Threading.Thread thread;
    public CSC_XA2Out(CSCore.XAudio2.XAudio2 xaudio, ISoundNode<AType> input, int sampleRate = 44100, int channels = 2, int latency = 10, int bufferCount = 2)
    {
        Input = input; SoundOut = xaudio.CreateMasteringVoice(channels, sampleRate); callbacks = new();
        InputVoice = xaudio.CreateSourceVoice(new(sampleRate, 32, channels, Encoding()));
        callbacks.BufferEnd += (s, e) => { if (audioBuffers.Any(x => x.ContextPtr == e.BufferContext)) onBufferEnd.Set(); };
        audioBuffers = new CSCore.XAudio2.XAudio2Buffer[bufferCount]; onBufferEnd = new(false);
        for (int i = 0; i < audioBuffers.Length; i++) audioBuffers[i] = new((int)(latency * 0.001 * SampleRate) * Marshal.SizeOf<AType>() * channels);
        System.Threading.Thread t = new(() =>
        {
            while (IsPlaying && !SoundOut.IsDisposed && !GShared.Quitting)
            {
                AType[] b = new AType[audioBuffers[currentBuffer].AudioBytes / Marshal.SizeOf<AType>()];
                SNBuffer<AType> buffer = new(b, 0, b.Length, Channels);
                if (ReadSamples(buffer) > 0)
                {
                    Utilities.Write(audioBuffers[currentBuffer].AudioDataPtr, b, 0, b.Length);
                    while (IsPlaying && InputVoice.State.BuffersQueued == bufferCount && !InputVoice.IsDisposed && !GShared.Quitting) onBufferEnd.WaitOne(latency);
                    if (InputVoice.IsDisposed || GShared.Quitting || !IsPlaying) return;
                    InputVoice.SubmitSourceBuffer(audioBuffers[currentBuffer]);
                    currentBuffer = ++currentBuffer % audioBuffers.Length;
                    OnBufferPlayed?.Invoke(buffer);
                }
            }
        }) { Name = "CSCore XAudio Thread" };
        t.Start();
    }
    public override int SampleRate { get { SoundOut.GetVoiceDetailsNative(out var details); return details.InputSampleRate; } }
    public override int Channels { get { SoundOut.GetVoiceDetailsNative(out var details); return details.InputChannels; } }
    public void Play() { IsPlaying = true; thread.Start(); InputVoice.Start(); }
    public void Stop() { IsPlaying = false; InputVoice.Stop(); }
    public bool IsPlaying { get; private set; }
    public override int ReadSamples(SNBuffer<AType> buffer) { if (Input == null) return 0; int result = Input.ReadSamples(buffer); FinishReading(buffer); return result; }
    public override void Dispose()
    {
        base.Dispose(); Stop(); SoundOut.Dispose(); InputVoice.Dispose(); for (int i = 0; i < audioBuffers.Length; i++) audioBuffers[i].Dispose();
        callbacks.Dispose(); onBufferEnd.Set(); onBufferEnd.Close(); onBufferEnd.Dispose();
    }
}
#endregion
#region SharpDX
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("59eff8b9-938c-4a26-82f2-95cb84cdc837")] interface IMediaBuffer
{
    [PreserveSig] public int SetLength(int cbLength);
    [PreserveSig] public int GetMaxLength(out int cbMaxLengthRef);
    [PreserveSig] public int GetBufferAndLength(IntPtr bufferOut, IntPtr cbLengthRef);
}
class MediaBuffer : IMediaBuffer, IDisposable
{
    readonly int _maxlength; IntPtr _buffer; int _length; bool _disposed;
    public MediaBuffer(int maxlength)
    {
        if (maxlength < 1) throw new ArgumentOutOfRangeException(nameof(maxlength)); _maxlength = maxlength;
        _buffer = Marshal.AllocCoTaskMem(maxlength); if (_buffer == IntPtr.Zero) throw new OutOfMemoryException("Could not allocate memory");
    }
    public int MaxLength => _maxlength;
    public int Length { get => _length; set => SetLength(value); }
    public int SetLength(int length)
    {
        if (length > MaxLength || length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length can not be less than zero or greater than maxlength.");
        _length = length; return 0;
    }
    public int GetMaxLength(out int length)
    {
        length = _maxlength;
        return 0;
    }
    public int GetBufferAndLength(IntPtr ppBuffer, IntPtr validDataByteLength)
    {
        if (ppBuffer != IntPtr.Zero) Marshal.WriteIntPtr(ppBuffer, _buffer);
        if (validDataByteLength != IntPtr.Zero) Marshal.WriteInt32(validDataByteLength, _length); return 0;
    }
    public void Write(byte[] buffer, int offset, int count) { Length = count; Marshal.Copy(buffer, offset, _buffer, count); }
    public void Read(byte[] buffer, int offset) => Read(buffer, offset, Length);
    public void Read(byte[] buffer, int offset, int count)
    { if (count > Length) throw new ArgumentOutOfRangeException(nameof(count), "count is greater than MaxLength"); Marshal.Copy(_buffer, buffer, offset, count); }
    public unsafe void Read(byte[] buffer, int offset, int count, int sourceOffset)
    {
        if (count > Length) throw new ArgumentOutOfRangeException(nameof(count), "count is greater than MaxLength");
        var p = (byte*)_buffer.ToPointer(); p += sourceOffset;
        Marshal.Copy(new IntPtr(p), buffer, offset, count);
    }
    public void Write(float[] buffer, int offset, int count) { Length = count * 4; Marshal.Copy(buffer, offset, _buffer, count); }
    public void Read(float[] buffer, int offset) => Read(buffer, offset, Length / 4);
    public void Read(float[] buffer, int offset, int count)
    { if (count * 4 > Length) throw new ArgumentOutOfRangeException(nameof(count), "count is greater than MaxLength"); Marshal.Copy(_buffer, buffer, offset, count); }
    public unsafe void Read(float[] buffer, int offset, int count, int sourceOffset)
    {
        if (count * 4 > Length) throw new ArgumentOutOfRangeException(nameof(count), "count is greater than MaxLength");
        var p = (byte*)_buffer.ToPointer(); p += sourceOffset;
        Marshal.Copy(new IntPtr(p), buffer, offset, count);
    }
    public void Dispose()
    { if (!_disposed) { if (_buffer != IntPtr.Zero) { Marshal.FreeCoTaskMem(_buffer); _buffer = IntPtr.Zero; } GC.SuppressFinalize(this); _disposed = true; } }
}
[ComImport, Guid("f447b69e-1884-4a7e-8055-346f74d6edb3")] class MFResampler { }
[ComImport, Guid("d8ad0f58-5494-4102-97c5-ec798e59bcf4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMO : IUnknown { }
[ComImport, Guid("E7E9984F-F09F-4da4-903F-6E2E0EFE56B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IResamplerProps
{ int SetHalfFilterLength(int quality); int SetUserChannelMtx([In] float[] channelConversionMatrix); }
[DirectX.SharpDX] class SDX_Resampler_Internal<AType>
{
    IMediaObject resampler; MFResampler mfresampler; IResamplerProps resamplerProps; MediaBuffer InputBuffer, OutputBuffer;
    DmoOutputDataBuffer DMOutputBuffer;
    System.Threading.ManualResetEventSlim locker = new(true); MDS_WFormat inputFormat, outputFormat; int quality, MaxLatency;
    public event Func<SNBuffer<AType>, int> BufferToResample; byte[] overhang = Array.Empty<byte>(); int overhangOffset;
    public int Quality { get => quality; set { quality = value; resamplerProps.SetHalfFilterLength(value); } }
    public MDS_WFormat InputFormat
    {
        get => inputFormat; set
        {
            if (inputFormat == null || !inputFormat.Equals(value))
            {
                locker.Wait(); locker.Reset();
                using var transform = resampler.QueryInterface<Transform>();
                transform.SetInputType(0, MediaType.FromWaveFormat(value), 0);
                //if (inputFormat != value) resampler.Discontinuity(0);
                /*resampler.GetInputStatus(0, out int status);
                if (status == 0)
                {
                    resampler?.Dispose(); Marshal.ReleaseComObject(mfresampler); Marshal.ReleaseComObject(resamplerProps);
                    mfresampler = new(); resampler = new(Marshal.GetComInterfaceForObject((IMO)mfresampler, typeof(IMO)));
                    resamplerProps = (IResamplerProps)mfresampler; Quality = Quality;
                    using var transform2 = resampler.QueryInterface<Transform>();
                    transform2.SetInputType(0, MediaType.FromWaveFormat(value), 0);
                }*/
                inputFormat = value;
                InputBuffer?.Dispose(); InputBuffer = new(inputFormat.AverageBytesPerSecond * MaxLatency / 1000);
                locker.Set();
            }
        }
    }
    public MDS_WFormat OutputFormat
    {
        get => outputFormat; set
        {
            if (outputFormat == null || !outputFormat.Equals(value))
            {
                locker.Wait(); locker.Reset();
                using var transform = resampler.QueryInterface<Transform>();
                transform.SetOutputType(0, MediaType.FromWaveFormat(value), 0);
                //if (outputFormat != null) resampler.Discontinuity(0);
                outputFormat = value;
                OutputBuffer?.Dispose(); OutputBuffer = new(outputFormat.AverageBytesPerSecond * MaxLatency / 1000);
                DMOutputBuffer = new() { PBuffer = ToIMB(OutputBuffer) };
                locker.Set();
            }
        }
    }
    static unsafe IntPtr ToIMB(MediaBuffer buffer) => Marshal.GetComInterfaceForObject(buffer, typeof(IMediaBuffer));
    public SDX_Resampler_Internal(MDS_WFormat input, MDS_WFormat output, int maxLatency = 500)
    {
        locker.Wait(); locker.Reset();
        mfresampler = new(); resampler = new(Marshal.GetComInterfaceForObject((IMO)mfresampler, typeof(IMO)));
        resamplerProps = (IResamplerProps)mfresampler; Quality = 10; MaxLatency = maxLatency;
        locker.Set();
        InputFormat = input; OutputFormat = output;
    }
    public int Read(AType[] buffer, int offset, int count)
    {
        locker.Wait(); locker.Reset();
        int read = 0, size = Marshal.SizeOf<AType>();
        count *= size;
        if (overhangOffset < overhang.Length)
        {
            read = Math.Min(overhang.Length - overhangOffset, count);
            Buffer.BlockCopy(overhang, overhangOffset, buffer, offset, read);
            overhangOffset += read;
        }
        while (read < count)
        {
            resampler.GetInputStatus(0, out int status);
            //System.Diagnostics.Debug.Assert(status == 1);
            //if (status == 1)
            {
                var bytesToRead = (int)Math.Ceiling(((long)count - read) * inputFormat.AverageBytesPerSecond / (double)outputFormat.AverageBytesPerSecond / size) * size;
                //bytesToRead = Math.Min(InputBuffer.MaxLength, bytesToRead);
                bytesToRead -= bytesToRead % inputFormat.BlockAlign;
                if (bytesToRead == 0) break;
                AType[] sourceBuffer = new AType[bytesToRead / size];
                int bytesRead = BufferToResample?.Invoke(new(sourceBuffer, 0, bytesToRead / size, inputFormat.Channels)) * size ?? 0;
                if (bytesRead <= 0 || resampler.IsDisposed) break;
                byte[] bufferByteForm = new byte[bytesToRead]; Buffer.BlockCopy(sourceBuffer, 0, bufferByteForm, 0, bytesToRead);

                if (InputBuffer.MaxLength < bytesRead) { InputBuffer.Dispose(); InputBuffer = new(bytesRead); }
                InputBuffer.Write(bufferByteForm, 0, bytesRead);
                resampler.ProcessInput(0, ToIMB(InputBuffer), 0, DMOutputBuffer.RtTimestamp, DMOutputBuffer.RtTimelength);
                DMOutputBuffer.DwStatus = 0;
                do
                {
                    if (OutputBuffer.MaxLength < count) { OutputBuffer.Dispose(); DMOutputBuffer.PBuffer = ToIMB(OutputBuffer = new(count)); }
                    OutputBuffer.SetLength(0);
                    resampler.ProcessOutput(0, 1, new[] { DMOutputBuffer }, out DMOutputBuffer.DwStatus);
                    if (OutputBuffer.Length <= 0) break;
                    bufferByteForm = new byte[OutputBuffer.Length];
                    OutputBuffer.Read(bufferByteForm, 0);
                    if (bufferByteForm.Length > count - read)
                    {
                        Buffer.BlockCopy(bufferByteForm, 0, buffer, offset * size + read, count - read);
                        overhang = bufferByteForm; overhangOffset = count - read;
                        read = count;
                    }
                    else
                    {
                        Buffer.BlockCopy(bufferByteForm, 0, buffer, offset * size + read, bufferByteForm.Length);
                        read += OutputBuffer.Length;
                    }
                } while ((DMOutputBuffer.DwStatus & 0x01000000) > 0); //todo: Implement DataAvailable
            }
        }
        locker.Set();
        return read / size;
    }
    public void Dispose()
    {
        locker.Wait(); locker.Reset(); Marshal.ReleaseComObject(mfresampler); Marshal.ReleaseComObject(resamplerProps);
        resampler?.Dispose(); InputBuffer?.Dispose(); OutputBuffer?.Dispose(); locker.Set();
    }
}
[DirectX.SharpDX] public class SDX_Resampler<AType> : NodeSingleInput<AType>
{
    public static MDS_WFormat DetectFormat(int sampleRate, int channels) =>
        typeof(AType) == typeof(float) ? MDS_WFormat.CreateIeeeFloatWaveFormat(sampleRate, channels) : new(sampleRate, Marshal.SizeOf<AType>() * 8, channels);
    public static Guid DetectGuid() => typeof(AType) == typeof(float) ? AudioFormatGuids.Float : typeof(AType) == typeof(short) ? AudioFormatGuids.Pcm : Guid.Empty;
    SDX_Resampler_Internal<AType> resampler; int sampleRate, channels; ISoundNode<AType> input;
    public int TargetSampleRate { get => sampleRate; set { sampleRate = value; if (resampler != null) resampler.OutputFormat = DetectFormat(value, TargetChannels); } }
    public int TargetChannels { get => channels; set { channels = value; if (resampler != null) resampler.OutputFormat = DetectFormat(TargetSampleRate, value); } }
    public override ISoundNode<AType> Input
    {
        get => input; set
        {
            input = value; if (input != null)
            {
                int R(SNBuffer<AType> b) => Input.ReadSamples(b);
                //if (resampler == null)
                if (resampler != null)
                {
                    resampler.BufferToResample -= R;
                    resampler.BufferToResample += buffer =>
                    {
                        resampler = new(DetectFormat(SampleRate, Channels), DetectFormat(TargetSampleRate, TargetChannels));
                        resampler.BufferToResample += R;
                        return resampler.Read(buffer.Samples, buffer.Offset, buffer.Count);
                    };
                }
                else
                {
                    resampler = new(DetectFormat(SampleRate, Channels), DetectFormat(TargetSampleRate, TargetChannels));
                    resampler.BufferToResample += R;
                }
                //resampler.InputFormat = DetectFormat(SampleRate, Channels);
            }
        }
    }
    public SDX_Resampler(ISoundNode<AType> input, int targetSampleRate = 44100, int targetChannels = 2)
    { TargetSampleRate = targetSampleRate; TargetChannels = targetChannels; Input = input; }
    public override int ReadSamples(SNBuffer<AType> buffer)
    { if (buffer == null) return 0; int read = resampler.Read(buffer.Samples, buffer.Offset, buffer.Count); buffer.Count = read; FinishReading(buffer); return read; }
    public override void Dispose() { base.Dispose(); resampler?.Dispose(); }
}
/// <summary>Stream decoder using SharpDX.</summary>
/// <remarks>Before using this node, call <see cref="MediaManager.Startup"/> or <see cref="MediaFactory.Startup"/> somewhere in your project.</remarks>
[DirectX.SharpDX] public class SDX_Decoder<AType> : NodeGeneric<AType>, ISoundNodeDecoder<AType> where AType : struct
{
    SourceReader Reader; Stream Stream; long position; SharpDX.MediaFoundation.MediaBuffer currentSample;
    public event Action<SNBuffer<AType>, long> OnStreamEnd; int sampleRate, channels, samplePos;
    System.Threading.ManualResetEventSlim locker = new(true);
    public long SampleCount { get; private set; }
    public bool Loop { get; set; }
    void SkipToPos()
    {
        currentSample?.Dispose(); currentSample = null; samplePos = 0;
        Reader.SetCurrentPosition(position); if (position == 0) return;
        while (true)
        {
            using var sample = Reader.ReadSample(SourceReaderIndex.AllStreams, SourceReaderControlFlags.None, out _, out var flags, out long pos);
            if (flags.HasFlag(SourceReaderFlags.Endofstream) || pos + sample.SampleDuration >= position)
            { currentSample = sample.ConvertToContiguousBuffer(); break; }
        }
    }
    public double Position
    {
        get => TimeSpan.FromTicks(position).TotalSeconds;
        set { position = TimeSpan.FromSeconds(value).Ticks; locker.Wait(); locker.Reset(); SkipToPos(); locker.Set(); }
    }
    public long SamplePosition
    {
        get => (long)(TimeSpan.FromTicks(position).TotalSeconds * SampleRate * Channels);
        set { position = TimeSpan.FromSeconds((double)value / SampleRate / Channels).Ticks; locker.Wait(); locker.Reset(); SkipToPos(); locker.Set(); }
    }
    public override int SampleRate => sampleRate;
    public override int Channels => channels;
    public SDX_Decoder(string file) => Initialize(new FileStream(file, FileMode.Open, FileAccess.Read));
    public SDX_Decoder(Stream stream) => Initialize(stream);
    void Initialize(Stream stream)
    {
        Reader = new(Stream = stream);
        Reader.SetStreamSelection(SourceReaderIndex.AllStreams, false);
        Reader.SetStreamSelection(SourceReaderIndex.FirstAudioStream, true);
        using MediaType mediaType = Reader.GetCurrentMediaType(SourceReaderIndex.FirstAudioStream);
        sampleRate = mediaType.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond);
        channels = mediaType.Get(MediaTypeAttributeKeys.AudioNumChannels);

        using MediaType decodeType = new();
        decodeType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        decodeType.Set(MediaTypeAttributeKeys.Subtype, SDX_Resampler<AType>.DetectGuid());
        decodeType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, Marshal.SizeOf<AType>() * 8);
        Reader.SetCurrentMediaType(SourceReaderIndex.FirstAudioStream, decodeType);
        SampleCount = (long)(new TimeSpan(Reader.GetPresentationAttribute(SourceReaderIndex.InvalidStreamIndex,
            PresentationDescriptionAttributeKeys.Duration)).TotalSeconds * SampleRate);
    }
    public override int ReadSamples(SNBuffer<AType> buffer)
    {
        if (buffer == null || buffer.Count <= 0) return 0;
        while (currentSample == null || currentSample.CurrentLength <= 0) //zdroj praskání?
        {
            locker.Wait(); locker.Reset();
            using var sample = Reader.ReadSample(SourceReaderIndex.FirstAudioStream, 0, out _, out var flags, out position);
            if (sample == null || flags.HasFlag(SourceReaderFlags.Endofstream))
            { OnStreamEnd?.Invoke(buffer, 0); if (Loop) { position = 0; SkipToPos(); } else { locker.Set(); return 0; } }
            else { currentSample?.Dispose(); currentSample = sample.ConvertToContiguousBuffer(); samplePos = 0; }
            locker.Set();
        }
        int rest = Math.Min(currentSample.CurrentLength / Marshal.SizeOf<AType>(), buffer.Count);
        if (rest < buffer.Count) //rekurze zbytku
        {
            Utilities.Read(currentSample.Lock(out _, out _) + samplePos, buffer.Samples, buffer.Offset, rest); currentSample.Unlock();
            currentSample?.Dispose(); currentSample = null;
            return rest + ReadSamples(new(buffer.Samples, buffer.Offset + rest, buffer.Count - rest, buffer.Channels));
        }
        else
        {
            Utilities.Read(currentSample.Lock(out _, out _) + samplePos, buffer.Samples, buffer.Offset, buffer.Count); currentSample.Unlock(); FinishReading(buffer);
            currentSample.CurrentLength -= buffer.Count * Marshal.SizeOf<AType>();
            samplePos += buffer.Count * Marshal.SizeOf<AType>(); FinishReading(buffer); return buffer.Count;
        }
    }
    public override void Dispose()
    { base.Dispose(); locker.Wait(); locker.Reset(); Stream?.Close(); Stream?.Dispose(); Reader?.Dispose(); currentSample?.Dispose(); locker.Set(); locker.Dispose(); }
}
[DirectX.SharpDX] public class SDX_MultiInput<AType> : NodeGeneric<AType> where AType : struct
{
    public int CustomRate, CustomChannels;
    public override int SampleRate => CustomRate == 0 ? inputs[0].Item1.SampleRate : CustomRate;
    public override int Channels => CustomChannels == 0 ? inputs[0].Item1.Channels : CustomChannels;
    public event Action<SNBuffer<AType>[], SNBuffer<AType>> ProcessBuffers;
    public IEnumerable<ISoundNode<AType>> Inputs => inputs.Select(x => x.Item1); List<(ISoundNode<AType>, SDX_Resampler_Internal<AType>)> inputs = new();
    public void AddInput(ISoundNode<AType> input)
    {
        SDX_Resampler_Internal<AType> dmo = new(SDX_Resampler<AType>.DetectFormat(input.SampleRate, input.Channels), SDX_Resampler<AType>.DetectFormat(SampleRate, Channels));
        inputs.Add((input, dmo));
    }
    public ISoundNode<AType> this[int index]
    {
        get => inputs[index].Item1; set
        { var i = inputs[index]; i.Item1 = value; inputs[index] = i; i.Item2.BufferToResample += buffer => value.ReadSamples(buffer); }
    }
    public void RemoveInput(int index)
    {
        inputs[index].Item2.Dispose(); inputs.RemoveAt(index);
    }
    public override int ReadSamples(SNBuffer<AType> buffer)
    {
        if (buffer == null) return 0;
        int smallestRead = int.MaxValue;
        ProcessBuffers?.Invoke(inputs.Select(x =>
        {
            var buffer2 = buffer with { Samples = new AType[buffer.Count] };
            smallestRead = Math.Min(smallestRead, x.Item2.Read(buffer2.Samples, 0, buffer.Count)); return buffer2;
        }).ToArray(), buffer); FinishReading(buffer); return smallestRead;
    }
    public override void Dispose() { base.Dispose(); for (int i = 0; i < inputs.Count; i++) inputs[i].Item2.Dispose(); }
}
[DirectX.SharpDX] public class SDX_Speed<AType> : NodeSingleInput<AType>
{
    SDX_Resampler_Internal<AType> resampler; double speed; public double Speed
    { get => speed; set { speed = value; if (resampler != null) resampler.OutputFormat = SDX_Resampler<AType>.DetectFormat((int)(SampleRate / value), Channels); } }
    public SDX_Speed(ISoundNode<AType> input, double speed)
    {
        Input = input; Speed = speed;
        resampler = new(SDX_Resampler<AType>.DetectFormat(SampleRate, Channels), SDX_Resampler<AType>.DetectFormat((int)(SampleRate / Speed), Channels));
        resampler.BufferToResample += buffer => Input.ReadSamples(buffer);
    }
    public override int ReadSamples(SNBuffer<AType> buffer)
    { if (buffer == null) return 0; int read = resampler.Read(buffer.Samples, buffer.Offset, buffer.Count); buffer.Count = read; FinishReading(buffer); return read; }
    public override void Dispose() { base.Dispose(); resampler?.Dispose(); }
}
/// <summary>DirectSound output using SharpDX. Note: DirectSound does not play when the window is out of focus.</summary>
[DirectX.SharpDX] public class SDX_DSOut<AType> : NodeSingleInput<AType>, ISoundNodeOut<AType> where AType : struct
{
    SharpDX.DirectSound.DirectSound SoundOut; public SharpDX.DirectSound.SecondarySoundBuffer SoundBuffer;
    System.Threading.AutoResetEvent onBufferEnd; MDS_WFormat format;
    public event Action<SNBuffer<AType>> OnBufferPlayed; System.Threading.Thread thread;
    int RoundNearestSample(double fraction) => (int)(fraction * SoundBuffer.Capabilities.BufferBytes) / (Channels * Marshal.SizeOf<AType>()) * (Channels * Marshal.SizeOf<AType>());
    /// <remarks>For best results, the latency must be bufferCount * 10 and at least 30. DirectSound also won't play when the window is out of focus.</remarks>
    public SDX_DSOut(System.Windows.Forms.Form window, ISoundNode<AType> input, int sampleRate = 44100, int channels = 2,
        int latency = 30, int bufferCount = 3, Guid soundDriver = default)
    {
        format = SDX_Resampler<AType>.DetectFormat(sampleRate, channels);
        Input = input; Initialize(window, latency, bufferCount, soundDriver);
        thread = new(() =>
        {
            AType[] b = new AType[SoundBuffer.Capabilities.BufferBytes];
            while (IsPlaying && !SoundOut.IsDisposed && !GShared.Quitting)
                {
                    for (int i = 0; i < bufferCount; i++)
                    {
                        int end = RoundNearestSample((i + 1.0) / bufferCount) / Marshal.SizeOf<AType>(), start = RoundNearestSample((double)i / bufferCount) / Marshal.SizeOf<AType>();
                        SNBuffer<AType> buffer = new(b, start, end - start, channels);
                        if (ReadSamples(buffer) > 0)
                        {
                            if (SoundOut.IsDisposed || !IsPlaying || GShared.Quitting) break;
                            onBufferEnd.WaitOne(latency);
                            SoundBuffer.Write(b, start, end - start, 0, SharpDX.DirectSound.LockFlags.FromWriteCursor);
                            OnBufferPlayed?.Invoke(buffer);
                        }
                    }
                }
        }) { Name = "SharpDX DirectSound Thread" };
    }
    void Initialize(System.Windows.Forms.Form window, int latency, int bufferCount, Guid soundDriver)
    {
        SoundOut = new(soundDriver);
        SoundOut.SetCooperativeLevel(window.Handle, SharpDX.DirectSound.CooperativeLevel.Normal); onBufferEnd = new(false);
        SoundBuffer = new(SoundOut, new()
        { Flags = SharpDX.DirectSound.BufferFlags.ControlPositionNotify, Format = format, BufferBytes = format.ConvertLatencyToByteSize(latency) });
        var positions = new SharpDX.DirectSound.NotificationPosition[bufferCount];
        for (int i = 0; i < bufferCount; i++)
            positions[i] = new() { Offset = RoundNearestSample((double)i / bufferCount) + 1, WaitHandle = onBufferEnd };
        SoundBuffer.SetNotificationPositions(positions);
    }
    public override int SampleRate => format.SampleRate; public override int Channels => format.Channels;
    public void Play() { IsPlaying = true; thread.Start(); SoundBuffer.Play(0, SharpDX.DirectSound.PlayFlags.None | SharpDX.DirectSound.PlayFlags.Looping); }
    public void Stop() { IsPlaying = false; SoundBuffer.Stop(); } public bool IsPlaying { get; private set; }
    public override void Dispose() { base.Dispose(); Stop(); SoundOut.Dispose(); SoundBuffer.Dispose(); onBufferEnd.Close(); onBufferEnd.Dispose(); }
}
/// <summary>Xaudio 2 output using SharpDX.</summary>
/// <remarks>Before using this node, create a <see cref="SharpDX.XAudio2.MasteringVoice"/> somewhere in your project.
/// There can be only one mastering voice in existence, that's why Ensoftener does not create one.</remarks>
[DirectX.SharpDX] public class SDX_XA2Out<AType> : NodeSingleInput<AType>, ISoundNodeOut<AType> where AType : struct
{
    public SharpDX.XAudio2.SourceVoice InputVoice { get; } bool scheduleDispose = false;
    SharpDX.XAudio2.AudioBuffer[] audioBuffers; DataStream[] dataBuffers; int currentBuffer; System.Threading.AutoResetEvent onBufferEnd;
    public event Action<SNBuffer<AType>> OnBufferPlayed; System.Threading.Thread thread;
    public unsafe SDX_XA2Out(SharpDX.XAudio2.XAudio2 xaudio, ISoundNode<AType> input, int sampleRate = 44100, int channels = 2, int latency = 10, int bufferCount = 1)
    {
        Input = input; InputVoice = new(xaudio, SDX_Resampler<AType>.DetectFormat(sampleRate, channels));
        onBufferEnd = new(false); InputVoice.BufferEnd += c => { /*if (audioBuffers.Any(x => x.Context == c)) */onBufferEnd.Set(); };
        audioBuffers = new SharpDX.XAudio2.AudioBuffer[bufferCount]; dataBuffers = new DataStream[bufferCount];
        for (int i = 0; i < dataBuffers.Length; i++) audioBuffers[i] = new(dataBuffers[i] = new((int)(latency * 0.001 * sampleRate) * Marshal.SizeOf<AType>() * channels, false, true));
        thread = new(() =>
        {
            while (IsPlaying && !InputVoice.IsDisposed && !GShared.Quitting)
            {
                AType[] b = new AType[dataBuffers[currentBuffer].Length / Marshal.SizeOf<AType>()];
                SNBuffer<AType> buffer = new(b, 0, b.Length, Channels);
                if (scheduleDispose) { Dispose(); break; }
                if (ReadSamples(buffer) > 0)
                {
                    Utilities.Write(audioBuffers[currentBuffer].AudioDataPointer, b, 0, b.Length);
                    while (IsPlaying && InputVoice.State.BuffersQueued == bufferCount && !InputVoice.IsDisposed && !GShared.Quitting) onBufferEnd.WaitOne(latency);
                    if (InputVoice.IsDisposed || GShared.Quitting) return;
                    InputVoice.SubmitSourceBuffer(audioBuffers[currentBuffer], null);
                    currentBuffer = ++currentBuffer % dataBuffers.Length;
                    OnBufferPlayed?.Invoke(buffer);
                }
            }
        }) { Name = "SharpDX XAudio Thread" };
    }
    public override int SampleRate { get { InputVoice.GetVoiceDetails(out var details); return details.InputSampleRate; } }
    public override int Channels { get { InputVoice.GetVoiceDetails(out var details); return details.InputChannelCount; } }
    public void Play() { IsPlaying = true; thread.Start(); InputVoice.Start(); }
    public void Stop() { IsPlaying = false; InputVoice.Stop(); }
    public bool IsPlaying { get; private set; }
    public float Volume { get => InputVoice.Volume; set => InputVoice.Volume = value; }
    public override void Dispose()
    {
        base.Dispose(); Stop(); InputVoice.Dispose(); for (int i = 0; i < dataBuffers.Length; i++) dataBuffers[i].Dispose();
        onBufferEnd.Set(); onBufferEnd.Close(); onBufferEnd.Dispose();
    }
    public void ScheduleDispose() => scheduleDispose = true;
}
#endregion
/// <summary>Stream decoder using NVorbis.</summary>
[NVorbis] public class OGG_Decoder : NodeGeneric<float>, ISoundNodeDecoder<float>
{
    NVorbis.VorbisReader decoder; Stream Stream; bool loop;
    public event Action<SNBuffer<float>, long> OnStreamEnd; 
    public OGG_Decoder(string file) { decoder = new(Stream = new FileStream(file, FileMode.Open, FileAccess.Read)); }
    public OGG_Decoder(Stream stream) { decoder = new(Stream = stream); }
    public override int SampleRate => decoder.SampleRate;
    public override int Channels => decoder.Channels;
    public long SampleCount => decoder.TotalSamples;
    public double Position { get => decoder.TimePosition.TotalSeconds; set => decoder.TimePosition = TimeSpan.FromSeconds(value); }
    public long SamplePosition { get => decoder.SamplePosition; set => decoder.SamplePosition = value; }
    public bool Loop { get => loop; set => loop = true; }
    public override int ReadSamples(SNBuffer<float> buffer)
    {
        int read = decoder.ReadSamples(buffer.Samples, buffer.Offset, buffer.Count);
        if (decoder.IsEndOfStream && loop) { OnStreamEnd?.Invoke(buffer, read); SamplePosition = 0; }
        if (read < buffer.Count) read += ReadSamples(new(buffer.Samples, buffer.Offset + read, buffer.Count - read, buffer.Channels));
        FinishReading(buffer); return read;
    }
    public override void Dispose() { base.Dispose(); decoder?.Dispose(); Stream.Close(); Stream.Dispose(); }
}