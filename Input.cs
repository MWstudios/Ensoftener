using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Ensoftener
{
    public static class Input
    {
        public class MouseButton
        {
            bool held2; public bool PressedDown { get; private set; } public bool Held { get; private set; } public bool Released { get; private set; }
            public void Update(Keys button) { Held = IsKeyPressed(button); if (Held) PressedDown = !held2; else Released = held2; held2 = Held; }
        }
        static int WHEEL_DELTA = int.MaxValue; static byte[] keyInputs = new byte[256];
        public static MouseButton LeftButton { get; private set; } = new();
        public static MouseButton RightButton { get; private set; } = new();
        public static MouseButton MiddleButton { get; private set; } = new();
        public static MouseButton SideButton1 { get; private set; } = new();
        public static MouseButton SideButton2 { get; private set; } = new();
        public static void Update()
        {
            GetKeyboardState(keyInputs);
            LeftButton.Update(Keys.LButton); RightButton.Update(Keys.RButton);
            MiddleButton.Update(Keys.MButton); SideButton1.Update(Keys.XButton1); SideButton2.Update(Keys.XButton2);
        }
        public static float MouseX { get; private set; } = 0;
        public static float MouseY { get; private set; } = 0;
        public static sbyte Scrolls { get; private set; } = 0;
        public static void Initialize() { Global.Form.MouseMove += Form_MouseMove; Global.Form.MouseWheel += Form_MouseWheel; }
        static void Form_MouseMove(object sender, MouseEventArgs e) => SetFlags(e);
        static void Form_MouseWheel(object sender, MouseEventArgs e) => SetFlags(e);
        public static bool IsKeyPressed(Keys key) => (keyInputs[(int)key] & 128) == 128;
        public static bool IsKeyEnabled(Keys key) => (keyInputs[(int)key] & 1) == 1;
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl)] static extern bool GetKeyboardState(byte[] lpKeyState);
        static void SetFlags(MouseEventArgs e)
        {
            MouseX = e.X; MouseY = e.Y;
            if (e.Delta < WHEEL_DELTA && e.Delta > 0) WHEEL_DELTA = e.Delta;
            Scrolls = (sbyte)(e.Delta / WHEEL_DELTA);
        }
    }
}
