using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Ensoftener
{
    public static class Input
    {
        public struct MouseButton
        {
            public bool PressedDown { get; private set; } public bool Held { get; private set; } public bool Released { get; private set; }
            public void Update(bool held) { if (held) PressedDown = !Held; else Released = held; Held = held; }
        }
        static bool mouseInputsLocked = false; static int WHEEL_DELTA = int.MaxValue; static byte[] keyInputs = new byte[256];
        public static MouseButton LeftButton { get; private set; } = new();
        public static MouseButton RightButton { get; private set; } = new();
        public static MouseButton MiddleButton { get; private set; } = new();
        public static MouseButton SideButton1 { get; private set; } = new();
        public static MouseButton SideButton2 { get; private set; } = new();
        public static void Update() { mouseInputsLocked = false; GetKeyboardState(keyInputs); }
        public static float MouseX { get; private set; } = 0;
        public static float MouseY { get; private set; } = 0;
        public static sbyte Scrolls { get; private set; } = 0;
        public static void Initialize()
        { Global.Form.MouseMove += Form_MouseMove; Global.Form.MouseDown += Form_MouseDown; Global.Form.MouseWheel += Form_MouseWheel; }
        static void Form_MouseMove(object sender, MouseEventArgs e) { if (!mouseInputsLocked) { SetFlags(e); mouseInputsLocked = true; } }
        static void Form_MouseDown(object sender, MouseEventArgs e) { if (!mouseInputsLocked) { SetFlags(e); mouseInputsLocked = true; } }
        static void Form_MouseWheel(object sender, MouseEventArgs e) { if (!mouseInputsLocked) { SetFlags(e); mouseInputsLocked = true; } }
        public static bool IsKeyPressed(Keys key) => (keyInputs[(int)key] & 128) == 128;
        public static bool IsKeyEnabled(Keys key) => (keyInputs[(int)key] & 1) == 1;
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl)] static extern bool GetKeyboardState(byte[] lpKeyState);
        static void SetFlags(MouseEventArgs e)
        {
            MouseX = e.X; MouseY = e.Y;
            LeftButton.Update(e.Button.HasFlag(MouseButtons.Left));
            RightButton.Update(e.Button.HasFlag(MouseButtons.Right));
            MiddleButton.Update(e.Button.HasFlag(MouseButtons.Middle));
            SideButton1.Update(e.Button.HasFlag(MouseButtons.XButton1));
            SideButton2.Update(e.Button.HasFlag(MouseButtons.XButton2));
            if (e.Delta < WHEEL_DELTA && e.Delta > 0) WHEEL_DELTA = e.Delta;
            Scrolls = (sbyte)(e.Delta / WHEEL_DELTA);
        }
    }
}
