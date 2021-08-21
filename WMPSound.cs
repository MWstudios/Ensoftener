using System.IO;
using System.Collections.Generic;
using WMPLib;

namespace Ensoftener.Sound
{
    /// <summary>A simplified version of <see cref="WindowsMediaPlayer"/> that's easier to understand.
    /// You also don't need to include the WMPLib namespace which would normally require specifying
    /// <b>&lt;UseWindowsForms&gt;true&lt;/UseWindowsForms&gt;</b> in your .csproj file.</summary>
    public class WMPSound
    {
        public WindowsMediaPlayer Sound = new();
        /// <summary>The location of the sound. It can be a local file or a website URL.</summary>
        /// <remarks>On Chrome, website sound file will appear as a black page with a small player in the middle. If that's the case, then you've got the sound's URL.</remarks>
        public string FilePath { get => Sound.URL; set => Sound.URL = value; }
        /// <summary>Volume of the sound between 0 and 100.</summary>
        public int Volume { get => Sound.settings.volume; set => Sound.settings.volume = value; }
        /// <summary>Speed of the sound, without changed pitch. 1 is normal.</summary>
        public double Speed { get => Sound.settings.rate; set => Sound.settings.rate = value; }
        /// <summary>Position of the sound player, in seconds.</summary>
        public double Position { get => Sound.controls.currentPosition; set => Sound.controls.currentPosition = value; }
        /// <summary>Balance of the sound.</summary>
        public int Balance { get => Sound.settings.balance; set => Sound.settings.balance = value; }
        /// <summary>Looping of the sound.</summary>
        public bool Loop { get => Sound.settings.getMode("loop"); set => Sound.settings.setMode("loop", value); }
        /// <summary></summary>
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
        /// <summary>Length of the sound, in seconds.</summary>
        public double Length => Sound.currentMedia.duration;
        public WMPSound(string path) { if (!File.Exists(path)) throw new FileNotFoundException(); FilePath = path; Sound.settings.autoStart = false; Stop(); }
        public void Play() => Sound.controls.play();
        public void Pause() => Sound.controls.pause();
        public void Stop() => Sound.controls.stop();
    }
}
