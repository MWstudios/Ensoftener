using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ensoftener
{
    /// <summary>Extensions, Win32 calls and common methods shared across DirectX and Vulkan.</summary>
    public static class GShared
    {
        static uint renderThreadId = 0; internal static bool Quitting { get; private set; }
        public static List<SideLoop> SideLoops { get; } = new();
        static GShared() { renderThreadId = GetCurrentThreadId(); }
        public static event EventHandler OnQuit;
        /// <summary>A loop separated from the main rendering method. The loop will run ONLY after <b><see cref="GDX.Run(Action, bool)"/></b> has started.</summary>
        public class SideLoop
        {
            bool nameUpdated = false; string name;
            /// <summary>The name of the thread that will appear on the debugger.</summary>
            public string Name { get => name; set { name = value; nameUpdated = true; } }
            public System.Threading.Tasks.Task Task { get; }
            /// <summary>The loop's refresh rate, in miliseconds. Accepts up to 4 decimal numbers. Default is 16.6667 ms.</summary>
            public double RefreshRate { get; set; } = 16.6667;
            /// <summary>Updates mouse+keyboard input in this thread. This is very important to look after, as updating input while another thread
            /// is running can have consequences if the other thread relies on this input.</summary>
            public bool UpdateInput { get; set; } = true;
            /// <summary>The method to call each update.</summary>
            public Action UpdateMethod { get; set; }
            public SideLoop(Action updateMethod)
            {
                UpdateMethod = updateMethod;
                Task = System.Threading.Tasks.Task.Run(() =>
                {
                    System.Diagnostics.Stopwatch t = new(); t.Start();
                    while (renderThreadId == 0) ; AttachThreadInput(GetCurrentThreadId(), renderThreadId, true);
                    while (!Quitting)
                    {
                        while (t.ElapsedTicks < RefreshRate * 10000) ; if (nameUpdated && Name != null) Thread.CurrentThread.Name = Name;
                        UpdateMethod(); if (UpdateInput) Input.Input.Update(); t.Restart();
                    }
                });
            }
        }
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern bool AttachThreadInput(uint source, uint dest, bool attach);
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)] internal static extern uint GetCurrentThreadId();
        /// <summary>Schedules a shutdown of the window. After the current rendering method ends, all sounds are stopped and components deleted.</summary>
        /// <remarks>Windows doesn't allow for different threads to close a single window, which means the closure needs to be scheduled for later
        /// and then executed by the window thread.</remarks>
        public static void Quit() { Quitting = true; OnQuit(null, null); }
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