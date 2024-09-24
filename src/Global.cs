using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ensoftener
{
    /// <summary>Extensions, Win32 calls and graphics API-independent methods.</summary>
    public static class GShared
    {
        internal static uint MainThreadId = 0; internal static bool Quitting { get; private set; }
        /*public static uint SystemVolume
        {
            get { _ = waveOutGetVolume(IntPtr.Zero, out uint CurrVol); return (CurrVol & 0x0000ffff) * 100 / ushort.MaxValue; }
            set { uint NewVolume = ushort.MaxValue * Math.Min(value, 100) / 100; _ = waveOutSetVolume(IntPtr.Zero, (NewVolume & 0x0000ffff) | (NewVolume << 16)); }
        }*/
        static GShared() { MainThreadId = GetCurrentThreadId(); }
        public static event EventHandler OnQuit;
        /// <summary>A loop separated from the main rendering method.</summary>
        public class SideLoop
        {
            public System.Threading.Thread Thread { get; }
            /// <summary>The loop's refresh rate, in miliseconds. Accepts up to 4 decimal numbers. Default is 16.6667 ms.</summary>
            public TimeSpan RefreshRate { get; set; } = new(166667);
            /// <summary>Updates mouse+keyboard input in this thread. This is very important to look after, as updating input while another thread
            /// is running can have consequences if the other thread relies on this input. True by default.</summary>
            public bool UpdateInput { get; set; } = true;
            /// <summary>The method to call each update.</summary>
            public event Action UpdateMethod;
            public SideLoop(Action updateMethod)
            {
                UpdateMethod = updateMethod;
                Thread = new(() =>
                {
                    System.Diagnostics.Stopwatch t = new(); t.Start();
                    while (MainThreadId == 0) ; AttachThreadInput(GetCurrentThreadId(), MainThreadId, true);
                    while (!Quitting)
                    {
                        while (t.Elapsed < RefreshRate) ; if (Quitting) break;
                        UpdateMethod(); if (UpdateInput) Input.Input.Update(); t.Restart();
                    }
                });
                Thread.Start();
            }
        }
        [DllImport("winmm.dll")] static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);
        [DllImport("winmm.dll")] static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern long SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern bool AttachThreadInput(uint source, uint dest, bool attach);
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)] internal static extern uint GetCurrentThreadId();
        /// <summary>Schedules a shutdown of the window. After the current rendering method ends, all sounds are stopped and components deleted.</summary>
        /// <remarks>Windows doesn't allow for different threads to close a single window, which means the closure needs to be scheduled for later
        /// and then executed by the window thread.</remarks>
        public static void Quit() { Quitting = true; OnQuit?.Invoke(null, null); }
        /// <summary>Loads an embedded resource file from a program or library.</summary>
        /// <param name="resourcePath">The path and file name to the resource relative to the .csproj file as it was built.</param>
        /// <param name="_namespace">The namespace where the resource is (you can get it via <see cref="Type.Namespace"/>).</param>
        /// <param name="assembly">The program or library to load the resource from. Null uses the caller program (<b>not</b> Ensoftener).</param>
        /// <returns>The stream to the resource.</returns>
        /// <remarks>To get a list of embedded resources, call <see cref="System.Reflection.Assembly.GetManifestResourceNames"/>.</remarks>
        public static System.IO.Stream LoadEmbeddedResource(string resourcePath, string _namespace, System.Reflection.Assembly assembly = null)
        {
            assembly ??= System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceStream($"{_namespace}.{resourcePath.Replace('/', '.').Replace('\\', '.')}");
        }
        #region extensions
        /// <summary>Adds an object to the end of the <seealso cref="List{T}"/> only if the object isn't already present.</summary>
        public static void AddIfMissing<T>(this List<T> list, T item) { if (!list.Contains(item)) list.Add(item); }
        /// <summary>Copies all contents of a <seealso cref="List{T}"/> into a new <seealso cref="List{T}"/>.</summary>
        public static List<T> Clone<T>(this List<T> list) { List<T> newList = new(); foreach (var item in list) newList.Add(item); return newList; }
        /// <summary>Clears a <seealso cref="List{T}"/> and adds one item.</summary>
        public static void OneItem<T>(this List<T> list, T item) { list.Clear(); list.Add(item); }
        #endregion
    }
}