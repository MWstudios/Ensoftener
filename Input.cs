using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Ensoftener.Input
{
    public static class Input
    {
        public class MouseButton
        {
            bool held2; public bool PressedDown { get; private set; } public bool Held { get; private set; } public bool Released { get; private set; }
            public void Update(Keys button) { Held = IsKeyPressed(button); if (Held) PressedDown = !held2; else Released = held2; held2 = Held; }
        }
        readonly static byte[] keyInputs = new byte[256];
        public static MouseButton LeftButton { get; private set; } = new();
        public static MouseButton RightButton { get; private set; } = new();
        public static MouseButton MiddleButton { get; private set; } = new();
        public static MouseButton SideButton1 { get; private set; } = new();
        public static MouseButton SideButton2 { get; private set; } = new();
        internal static void Update()
        {
            XboxInput.Update();
            GetKeyboardState(keyInputs);
            LeftButton.Update(Keys.LButton); RightButton.Update(Keys.RButton);
            MiddleButton.Update(Keys.MButton); SideButton1.Update(Keys.XButton1); SideButton2.Update(Keys.XButton2);
        }
        public static float MouseX { get; private set; } = 0;
        public static float MouseY { get; private set; } = 0;
        public static sbyte Scrolls { get; private set; } = 0;
        internal static void Initialize() { Global.Form.MouseMove += Form_MouseMove; Global.Form.MouseWheel += Form_MouseWheel; }
        static void Form_MouseMove(object sender, MouseEventArgs e) => SetFlags(e);
        static void Form_MouseWheel(object sender, MouseEventArgs e) => SetFlags(e);
        public static bool IsKeyPressed(Keys key) => (keyInputs[(int)key] & 128) == 128;
        public static bool IsKeyEnabled(Keys key) => (keyInputs[(int)key] & 1) == 1;
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl)] static extern bool GetKeyboardState(byte[] lpKeyState);
        static void SetFlags(MouseEventArgs e) { MouseX = e.X; MouseY = e.Y; Scrolls = (sbyte)(e.Delta / SystemInformation.MouseWheelScrollDelta); }
    }
    /// <summary></summary>
    public static class XboxInput
    {
        public class XboxController
        {
            public byte LT => input.LT; public byte RT => input.RT; XIStruct input = new(); int number;
            public short LeftJostickX => input.joyLeftX; public short LeftJoystickY => input.joyLeftY;
            public short RightJoystickX => input.joyRightX;  public short RightJoystickY => input.joyRightY;
            public XboxController(int num) => number = num;
            public bool IsButtonPressed(XboxButtons button) => ((input.buttons >> (int)button) & 1) == 1;
            internal void Update() { XInputGetState(number, input); }
            public void Vibrate(ushort left, ushort right) { XInputSetState(number, left, right); }
        }
        public static XboxController Controller1 { get; } = new(0);
        public static XboxController Controller2 { get; } = new(1);
        public static XboxController Controller3 { get; } = new(2);
        public static XboxController Controller4 { get; } = new(3);
        [DllImport("xinput9_1_0.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl)] static extern bool XInputGetState(int controller, XIStruct xInput);
        [DllImport("xinput9_1_0.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        static extern bool XInputSetState(int controller, ushort left, ushort right);
        internal static void Update() { Controller1.Update(); Controller2.Update(); Controller3.Update(); Controller4.Update(); }
        [System.Flags] public enum XboxButtons
        { DPadUp, DPadDown, DPadLeft, DPadRight, MenuButton, WindowButton, LeftJoy, RightJoy, LB, RB, A = 12, B = 13, X = 14, Y = 15 }
        [StructLayout(LayoutKind.Sequential)] internal struct XIStruct
        { public uint packet; public short buttons; public byte LT, RT; public short joyLeftX, joyLeftY, joyRightX, joyRightY; }
    }
}
