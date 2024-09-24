using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Ensoftener.Input
{
    public static class Input
    {
        public class MouseButton
        {
            bool held2; public bool PressedDown { get; private set; } public bool Held { get; private set; } public bool Released { get; private set; }
            public void Update(Keys button)
            {
                Held = IsKeyPressed(button);
                if (Held) PressedDown = !held2; else Released = held2; held2 = Held;
            }
        }
        readonly static byte[] keyInputs = new byte[256], lastInputs = new byte[256]; static float mX, mY;
        public static MouseButton LeftButton { get; private set; } = new();
        public static MouseButton RightButton { get; private set; } = new();
        public static MouseButton MiddleButton { get; private set; } = new();
        public static MouseButton SideButton1 { get; private set; } = new();
        public static MouseButton SideButton2 { get; private set; } = new();
        /// <summary>The character inputs of the keyboard. Null if there are none.</summary>
        public static string KeyboardChars { get; private set; }
        public static void Update()
        {
            XboxInput.Update();
            System.Buffer.BlockCopy(keyInputs, 0, lastInputs, 0, 256); GetKeyboardState(keyInputs); KeyboardChars = null; Scrolls = 0;
            LeftButton.Update(Keys.LButton); RightButton.Update(Keys.RButton);
            MiddleButton.Update(Keys.MButton); SideButton1.Update(Keys.XButton1); SideButton2.Update(Keys.XButton2);
        }
        public static float MouseX { get; internal set; }
        public static float MouseY { get; internal set; }
        public static sbyte Scrolls { get; private set; } = 0;
        internal static void Form_KeyPress(object sender, KeyPressEventArgs e) => KeyboardChars = (KeyboardChars ?? string.Empty) + e.KeyChar.ToString();
        internal static void Form_MouseWheel(object sender, MouseEventArgs e) { Scrolls += (sbyte)(e.Delta / SystemInformation.MouseWheelScrollDelta); }
        public static bool IsKeyPressed(Keys key) => (keyInputs[(int)key] & 128) == 128;
        public static bool IsKeyEnabled(Keys key) => (keyInputs[(int)key] & 1) == 1;
        public static bool IsKeyPressedDown(Keys key) => (keyInputs[(int)key] & 128) == 128 && (lastInputs[(int)key] & 128) != (keyInputs[(int)key] & 128);
        public static bool IsKeyReleased(Keys key) => (keyInputs[(int)key] & 128) == 0 && (lastInputs[(int)key] & 128) != (keyInputs[(int)key] & 128);
        [DllImport("user32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)] static extern bool GetKeyboardState(byte[] lpKeyState);
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
        /// <summary>The 4 controllers in the order they were plugged in.
        /// If there are less, the remaining ones will have <see cref="XboxController.Connected"/> set to false.</summary>
        public static XboxController[] Controllers { get; } = { new(0), new(1), new(2), new(3) };
        [DllImport("xinput9_1_0.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern int XInputGetState(int controller, ref XIStruct xInput);
        [DllImport("xinput9_1_0.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern int XInputSetState(int controller, ref XVStruct xVibrate);
        internal static void Update() { for (int i = 0; i < Controllers.Length; i++) Controllers[i].Update(); }
        public enum XboxButtons { DPadUp, DPadDown, DPadLeft, DPadRight, MenuButton, WindowButton, LeftJoystick, RightJoystick, LB, RB, A = 12, B = 13, X = 14, Y = 15 }
        [StructLayout(LayoutKind.Sequential)] internal struct XIStruct
        { public uint packet; public short buttons; public byte LT, RT; public short joyLeftX, joyLeftY, joyRightX, joyRightY; }
        [StructLayout(LayoutKind.Sequential)] internal struct XVStruct { public ushort left, right; public XVStruct(ushort l, ushort r) { left = l; right = r; } }
    }
}
