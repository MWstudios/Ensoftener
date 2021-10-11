using System;
using System.IO;
using System.Windows.Media;
using System.Collections.Generic;
//using Microsoft.DirectX.DirectSound;
using WMPLib;

namespace Ensoftener.Sound
{
    /*internal static class SoundGlobal
    {
        public static Device Device { get; } = new();
    }*/
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
}