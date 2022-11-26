using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Ensoftener.Input
{
    public static class Input
    {
        public class MouseButton
        {
            bool held2, fakeHold, fakeClick; public bool PressedDown { get; private set; } public bool Held { get; private set; } public bool Released { get; private set; }
            public void Update(Keys button)
            {
                Held = IsKeyPressed(button) || (fakeHold && UseFakeInputs);
                if (fakeClick) { fakeHold = false; fakeClick = false; }
                if (Held) PressedDown = !held2; else Released = held2; held2 = Held;
            }
            public void HoldDown() { if (UseFakeInputs) fakeHold = true; }
            public void StopHoldingDown() { if (UseFakeInputs) fakeHold = false; }
            public void Click() { if (UseFakeInputs) { fakeHold = true; fakeClick = true; } }
        }
        readonly static byte[] keyInputs = new byte[256], fakeInputs = new byte[256]; static float mX, mY;
        public static MouseButton LeftButton { get; private set; } = new();
        public static MouseButton RightButton { get; private set; } = new();
        public static MouseButton MiddleButton { get; private set; } = new();
        public static MouseButton SideButton1 { get; private set; } = new();
        public static MouseButton SideButton2 { get; private set; } = new();
        /// <summary>The character inputs of the keyboard. Null if there are none.</summary>
        public static string KeyboardChars { get; private set; }
        public static void Update()
        {
            XboxInput.Update(); GetKeyboardState(keyInputs); KeyboardChars = null; Scrolls = 0;
            LeftButton.Update(Keys.LButton); RightButton.Update(Keys.RButton);
            MiddleButton.Update(Keys.MButton); SideButton1.Update(Keys.XButton1); SideButton2.Update(Keys.XButton2);
        }
        public static float MouseX { get => mX; set => Cursor.Position = new((int)(Cursor.Position.X - mX + value), Cursor.Position.Y); }
        public static float MouseY { get => mY; set => Cursor.Position = new(Cursor.Position.X, (int)(Cursor.Position.Y - mY + value)); }
        public static sbyte Scrolls { get; private set; } = 0;
        /// <summary>Accept faking keyboard and mouse input with <b><see cref="PressKey(Keys)"/></b>, <b><see cref="UnpressKey(Keys)"/></b>,
        /// <b><see cref="MouseButton.HoldDown()"/></b>, <b><see cref="MouseButton.StopHoldingDown()"/></b> and <b><see cref="MouseButton.Click()"/></b>.</summary>
        public static bool UseFakeInputs { get; set; } = false;
        internal static void Initialize() { GDX.Form.MouseMove += Form_MouseMove; GDX.Form.MouseWheel += Form_MouseWheel; GDX.Form.KeyPress += Form_KeyPress; }
        private static void Form_KeyPress(object sender, KeyPressEventArgs e) => KeyboardChars = (KeyboardChars ?? string.Empty) + e.KeyChar.ToString();
        static void Form_MouseMove(object sender, MouseEventArgs e) => SetFlags(e, false);
        static void Form_MouseWheel(object sender, MouseEventArgs e) => SetFlags(e, true);
        public static bool IsKeyPressed(Keys key) => (keyInputs[(int)key] & 128) == 128 || (UseFakeInputs && (fakeInputs[(int)key] & 128) == 128);
        public static bool IsKeyEnabled(Keys key) => (keyInputs[(int)key] & 1) == 1 || (UseFakeInputs && (fakeInputs[(int)key] & 1) == 1);
        public static void PressKey(Keys key) { fakeInputs[(int)key] |= 1; fakeInputs[(int)key] ^= 128; }
        public static void UnpressKey(Keys key) { fakeInputs[(int)key] &= 0b11111110; }
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)] static extern bool GetKeyboardState(byte[] lpKeyState);
        static void SetFlags(MouseEventArgs e, bool scroll) { if (scroll) Scrolls = (sbyte)(e.Delta / SystemInformation.MouseWheelScrollDelta); mX = e.X; mY = e.Y; }
    }
    /// <summary>The class that provides everything necessary for working with Xbox controllers.</summary>
    public static class XboxInput
    {
        public class XboxController
        {
            public bool Connected { get; private set; }
            /// <summary>Gets the tilt of the left trigger (from 0 to 255).</summary>
            public byte LT => input.LT;
            /// <summary>Gets the tilt of the right trigger (from 0 to 255).</summary>
            public byte RT => input.RT; XIStruct input = new(); int number;
            /// <summary>Gets the X position of the left joystick (from -32768 to 32767).</summary>
            public short LeftJoystickX => input.joyLeftX;
            /// <summary>Gets the Y position of the left joystick (from -32768 to 32767).</summary>
            public short LeftJoystickY => input.joyLeftY;
            /// <summary>Gets the X position of the right joystick (from -32768 to 32767).</summary>
            public short RightJoystickX => input.joyRightX;
            /// <summary>Gets the Y position of the right joystick (from -32768 to 32767).</summary>
            public short RightJoystickY => input.joyRightY;
            public XboxController(int num) => number = num;
            public bool IsButtonPressed(XboxButtons button) => ((input.buttons >> (int)button) & 1) == 1;
            internal void Update() => Connected = XInputGetState(number, ref input) == 0;
            /// <summary>Vibrates the controller infinitely (until set to 0).</summary>
            /// <param name="left">The strength of the motor on the left side, from 0 to 65535.</param>
            /// <param name="right">The strength of the motor on the right side, from 0 to 65535. The right motor vibrates more "smoothly" than the left motor.</param>
            /// <remarks>Not all values actually influence the strength, the motors have only several steps of vibration intensity.</remarks>
            public void Vibrate(ushort left, ushort right) { XVStruct vibration = new(left, right); if (Connected) XInputSetState(number, ref vibration); }
        }
        /// <summary>The first controller that was plugged in.</summary>
        public static XboxController Controller1 { get; } = new(0);
        /// <summary>The second controller that was plugged in.</summary>
        public static XboxController Controller2 { get; } = new(1);
        /// <summary>The third controller that was plugged in.</summary>
        public static XboxController Controller3 { get; } = new(2);
        /// <summary>The fourth controller that was plugged in.</summary>
        public static XboxController Controller4 { get; } = new(3);
        [DllImport("xinput9_1_0.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern int XInputGetState(int controller, ref XIStruct xInput);
        [DllImport("xinput9_1_0.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern int XInputSetState(int controller, ref XVStruct xVibrate);
        internal static void Update() { Controller1.Update(); Controller2.Update(); Controller3.Update(); Controller4.Update(); }
        public enum XboxButtons { DPadUp, DPadDown, DPadLeft, DPadRight, MenuButton, WindowButton, LeftJoystick, RightJoystick, LB, RB, A = 12, B = 13, X = 14, Y = 15 }
        [StructLayout(LayoutKind.Sequential)] internal struct XIStruct
        { public uint packet; public short buttons; public byte LT, RT; public short joyLeftX, joyLeftY, joyRightX, joyRightY; }
        [StructLayout(LayoutKind.Sequential)] internal struct XVStruct { public ushort left, right; public XVStruct(ushort l, ushort r) { left = l; right = r; } }
    }
}
