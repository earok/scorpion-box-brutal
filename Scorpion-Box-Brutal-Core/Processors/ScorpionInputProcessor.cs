using Brutal.GlfwApi;
using SK.Libretro;
using static SK.Libretro.Wrapper;

namespace ScorpionBox.Processors
{

    internal class ScorpionInputProcessor : IInputProcessor
    {
        private ScorpionBoxGame _box;
        private Wrapper _retro;
        private GlfwWindow _window;

        private float _deadZone;
        private float _axisDeadZone;
        private HashSet<Tuple<int, int>> _stableJoystickAxes = [];
        //    private Keys[] _lastPressedKeys = [];
        private int _lastMouseX;
        private int _lastMouseY;
        private float _mouseDeltaX;
        private float _mouseDeltaY;

        private Dictionary<retro_key, GlfwKey> _keyMap = [];
        private Dictionary<retro_key, GlfwKey> _keyMapShifted = [];
        private Dictionary<GlfwKey, retro_key> _inversekeyMap = [];
        private Dictionary<GlfwKey, retro_key> _inversekeyMapShifted = [];

        private readonly GlfwJoystickHat[] _hatLeft = { GlfwJoystickHat.Left, GlfwJoystickHat.LeftUp, GlfwJoystickHat.LeftDown };
        private readonly GlfwJoystickHat[] _hatRight = { GlfwJoystickHat.Right, GlfwJoystickHat.RightUp, GlfwJoystickHat.RightDown };
        private readonly GlfwJoystickHat[] _hatUp = { GlfwJoystickHat.Up, GlfwJoystickHat.LeftUp, GlfwJoystickHat.RightUp };
        private readonly GlfwJoystickHat[] _hatDown = { GlfwJoystickHat.Down, GlfwJoystickHat.LeftDown, GlfwJoystickHat.RightDown };

        public ScorpionInputProcessor(ScorpionBoxGame box, Wrapper retro, GlfwWindow window)
        {
            _box = box;
            _retro = retro;
            _window = window;

            _keyMap[retro_key.RETROK_BACKSPACE] = GlfwKey.Backspace;
            _keyMap[retro_key.RETROK_TAB] = GlfwKey.Tab;
            _keyMap[retro_key.RETROK_RETURN] = GlfwKey.Enter;
            _keyMap[retro_key.RETROK_PAUSE] = GlfwKey.Pause;
            _keyMap[retro_key.RETROK_ESCAPE] = GlfwKey.Escape;
            _keyMap[retro_key.RETROK_SPACE] = GlfwKey.Space;
            _keyMap[retro_key.RETROK_DELETE] = GlfwKey.Delete;

            _keyMap[retro_key.RETROK_QUOTE] = GlfwKey.Apostrophe;
            _keyMap[retro_key.RETROK_PERIOD] = GlfwKey.Period;
            _keyMap[retro_key.RETROK_SEMICOLON] = GlfwKey.Semicolon;
            _keyMap[retro_key.RETROK_COMMA] = GlfwKey.Comma;
            _keyMap[retro_key.RETROK_EQUALS] = GlfwKey.Equal;
            _keyMap[retro_key.RETROK_SLASH] = GlfwKey.Slash;
            _keyMap[retro_key.RETROK_MINUS] = GlfwKey.Minus;
            _keyMap[retro_key.RETROK_LEFTBRACKET] = GlfwKey.LeftBracket;
            _keyMap[retro_key.RETROK_RIGHTBRACKET] = GlfwKey.RightBracket;
            _keyMap[retro_key.RETROK_BACKSLASH] = GlfwKey.Backslash;
            _keyMap[retro_key.RETROK_BACKQUOTE] = GlfwKey.GraveAccent;

            //Direct loops
            for (var i = 0; i <= 9; i++)
            {
                _keyMap[retro_key.RETROK_0 + i] = GlfwKey.Number0 + i;
            }
            for (var i = 0; i <= 9; i++)
            {
                _keyMap[retro_key.RETROK_KP0 + i] = GlfwKey.Kp0 + i;
            }
            for (var i = 1; i <= 15; i++)
            {
                _keyMap[retro_key.RETROK_F1 + i - 1] = GlfwKey.F1 + i - 1;
            }
            for (var i = 0; i <= 25; i++)
            {
                _keyMap[retro_key.RETROK_a + i] = GlfwKey.A + i;
            }

            _keyMap[retro_key.RETROK_UP] = GlfwKey.Up;
            _keyMap[retro_key.RETROK_DOWN] = GlfwKey.Down;
            _keyMap[retro_key.RETROK_LEFT] = GlfwKey.Left;
            _keyMap[retro_key.RETROK_RIGHT] = GlfwKey.Right;
            _keyMap[retro_key.RETROK_INSERT] = GlfwKey.Insert;
            _keyMap[retro_key.RETROK_HOME] = GlfwKey.Home;
            _keyMap[retro_key.RETROK_END] = GlfwKey.End;
            _keyMap[retro_key.RETROK_PAGEUP] = GlfwKey.PageUp;
            _keyMap[retro_key.RETROK_PAGEDOWN] = GlfwKey.PageDown;
            _keyMap[retro_key.RETROK_NUMLOCK] = GlfwKey.NumLock;
            _keyMap[retro_key.RETROK_CAPSLOCK] = GlfwKey.CapsLock;
            _keyMap[retro_key.RETROK_LSHIFT] = GlfwKey.LeftShift;
            _keyMap[retro_key.RETROK_RSHIFT] = GlfwKey.RightShift;
            _keyMap[retro_key.RETROK_LCTRL] = GlfwKey.LeftControl;
            _keyMap[retro_key.RETROK_RCTRL] = GlfwKey.RightControl;
            _keyMap[retro_key.RETROK_LALT] = GlfwKey.LeftAlt;
            _keyMap[retro_key.RETROK_RALT] = GlfwKey.RightAlt;
            _keyMap[retro_key.RETROK_PRINT] = GlfwKey.PrintScreen;

            _keyMap[retro_key.RETROK_KP_PERIOD] = GlfwKey.KpDecimal;
            _keyMap[retro_key.RETROK_KP_DIVIDE] = GlfwKey.KpDivide;
            _keyMap[retro_key.RETROK_KP_MULTIPLY] = GlfwKey.KpMultiply;
            _keyMap[retro_key.RETROK_KP_MINUS] = GlfwKey.KpSubtract;
            _keyMap[retro_key.RETROK_KP_PLUS] = GlfwKey.KpAdd;
            _keyMap[retro_key.RETROK_KP_ENTER] = GlfwKey.KpEnter;
            _keyMap[retro_key.RETROK_KP_EQUALS] = GlfwKey.KpEqual;
            _keyMap[retro_key.RETROK_SCROLLOCK] = GlfwKey.ScrollLock;
            _keyMap[retro_key.RETROK_LSUPER] = GlfwKey.LeftSuper;
            _keyMap[retro_key.RETROK_RSUPER] = GlfwKey.RightSuper;
            _keyMap[retro_key.RETROK_MENU] = GlfwKey.Menu;
            _keyMap[retro_key.RETROK_OEM_102] = GlfwKey.World2;

            //Shifted keys
            _keyMapShifted[retro_key.RETROK_EXCLAIM] = GlfwKey.Number1;
            _keyMapShifted[retro_key.RETROK_AT] = GlfwKey.Number2;
            _keyMapShifted[retro_key.RETROK_HASH] = GlfwKey.Number3;
            _keyMapShifted[retro_key.RETROK_DOLLAR] = GlfwKey.Number4;
            _keyMapShifted[(retro_key)'%'] = GlfwKey.Number5; //libretro.h has no RETROK_PERCENT
            _keyMapShifted[retro_key.RETROK_CARET] = GlfwKey.Number6;
            _keyMapShifted[retro_key.RETROK_AMPERSAND] = GlfwKey.Number7;
            _keyMapShifted[retro_key.RETROK_ASTERISK] = GlfwKey.Number8;
            _keyMapShifted[retro_key.RETROK_LEFTPAREN] = GlfwKey.Number9;
            _keyMapShifted[retro_key.RETROK_RIGHTPAREN] = GlfwKey.Number0;

            _keyMapShifted[retro_key.RETROK_QUOTEDBL] = GlfwKey.Apostrophe;
            _keyMapShifted[retro_key.RETROK_PLUS] = GlfwKey.Equal;
            _keyMapShifted[retro_key.RETROK_QUESTION] = GlfwKey.Slash;
            _keyMapShifted[retro_key.RETROK_COLON] = GlfwKey.Semicolon;
            _keyMapShifted[retro_key.RETROK_LESS] = GlfwKey.Comma;
            _keyMapShifted[retro_key.RETROK_GREATER] = GlfwKey.Period;
            _keyMapShifted[retro_key.RETROK_UNDERSCORE] = GlfwKey.Minus;

            _keyMapShifted[retro_key.RETROK_LEFTBRACE] = GlfwKey.LeftBracket;
            _keyMapShifted[retro_key.RETROK_RIGHTBRACE] = GlfwKey.RightBracket;
            _keyMapShifted[retro_key.RETROK_BAR] = GlfwKey.Backslash;
            _keyMapShifted[retro_key.RETROK_TILDE] = GlfwKey.GraveAccent;

            //fix the shifted keys to ensure they contain every keypair
            foreach (var keyPair in _keyMap)
            {
                if (_keyMapShifted.ContainsKey(keyPair.Key) == false)
                {
                    _keyMapShifted[keyPair.Key] = keyPair.Value;
                }
            }

            //Inverse the keys
            foreach (var keypair in _keyMap)
            {
                _inversekeyMap[keypair.Value] = keypair.Key;
            }
            foreach (var keypair in _keyMapShifted)
            {
                _inversekeyMapShifted[keypair.Value] = keypair.Key;
            }

            _retro.ActivateInput(this);
        }

        public ScorpionInputProcessor SetDeadZone(float deadZone)
        {
            _deadZone = deadZone;
            _axisDeadZone = 32767 * deadZone;
            return this;
        }

        public unsafe bool JoypadButton(int port, int button)
        {
            //Check the keyboard bindings first
            if (_box.KeyDictionary.TryGetValue(port, out var portData))
            {
                if (portData.TryGetValue((retro_device_id_joypad)button, out var key))
                {
                    if (KeyDown(key))
                    {
                        return true;
                    }
                }
            }

            //We don't have a joystick plugged in that port
            if (Glfw.JoystickPresent(port) == false)
            {
                return false;
            }

            //Use gamepad interface (temporary fallback test to regular joystick)
            if (Glfw.JoystickIsGamepad(port) && false)
            {
                Glfw.GetGamepadState(port, out var gpState);
                switch ((retro_device_id_joypad)button)
                {
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_LEFT:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.DpadLeft) || gpState.Axes[(int)GlfwGamepadAxis.LeftX] < -_deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_RIGHT:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.DpadRight) || gpState.Axes[(int)GlfwGamepadAxis.LeftX] > _deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_DOWN:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.DpadDown) || gpState.Axes[(int)GlfwGamepadAxis.LeftY] > _deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_UP:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.DpadUp) || gpState.Axes[(int)GlfwGamepadAxis.LeftY] < -_deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_A:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.A);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_B:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.B);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_X:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.X);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_Y:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.Y);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_START:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.Start);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_SELECT:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.Back);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_L:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.LeftBumper);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_R:
                        return IsJoypadButtonDown(gpState, GlfwGamepadButton.RightBumper);
                }
            }

            //Not a gamepad, fallback
            var hats = Glfw.GetJoystickHats(port);
            var axes = Glfw.GetJoystickAxes(port);
            var buttons = Glfw.GetJoystickButtons(port);

            switch ((retro_device_id_joypad)button)
            {
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_LEFT:
                    return IsJoystickHat(hats, _hatLeft) || JoystickAxis(axes, 0) < -_deadZone;
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_RIGHT:
                    return IsJoystickHat(hats, _hatRight) || JoystickAxis(axes, 0) > _deadZone;
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_UP:
                    return IsJoystickHat(hats, _hatUp) || JoystickAxis(axes, 1) < -_deadZone;
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_DOWN:
                    return IsJoystickHat(hats, _hatDown) || JoystickAxis(axes, 1) > _deadZone;
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_A:
                    return IsJoystickButtonDown(buttons, 0);
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_B:
                    return IsJoystickButtonDown(buttons, 1);
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_X:
                    return IsJoystickButtonDown(buttons, 2);
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_Y:
                    return IsJoystickButtonDown(buttons, 3);
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_L:
                    return IsJoystickButtonDown(buttons, 4);
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_R:
                    return IsJoystickButtonDown(buttons, 5);
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_START:
                    return IsJoystickButtonDown(buttons, 6);
                case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_SELECT:
                    return IsJoystickButtonDown(buttons, 7);
            }


            return false;
        }

        private bool IsJoystickButtonDown(ReadOnlySpan<byte> buttons, int button)
        {
            return buttons.Length > button ? buttons[button] == Glfw.Constants.GLFW_PRESS : false;
        }

        private float JoystickAxis(ReadOnlySpan<float> axes, int axis)
        {
            return axes.Length > axis ? axes[axis] : 0;
        }

        private static bool IsJoystickHat(ReadOnlySpan<GlfwJoystickHat> hats, GlfwJoystickHat[] options)
        {
            foreach (var hat in hats)
            {
                foreach (var option in options)
                {
                    if (hat == option)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static unsafe bool IsJoypadButtonDown(GlfwGamepadState state, GlfwGamepadButton button)
        {
            return state.Buttons[(int)button] == Glfw.Constants.GLFW_PRESS;
        }


        public bool Key(int id)
        {
            var currentKeyMap = _keyMap;
            if (KeyDown(GlfwKey.LeftShift) || KeyDown(GlfwKey.RightShift))
            {
                currentKeyMap = _keyMapShifted;
            }

            if (currentKeyMap.TryGetValue((retro_key)id, out var value))
            {
                return KeyDown(value);
            }
            return false;
        }

        private bool KeyDown(GlfwKey value)
        {
            return _window.GetKey(value) != GlfwKeyAction.Release;
        }

        //Only one mouse supported
        public bool MouseButton(int port, int button) => port == 0 && _window.GetMouseButton((GlfwMouseButton)button) == GlfwButtonAction.Press;

        public float MouseDelta(int port, int axis)
        {
            float result;
            if (axis == 0)
            {
                var cursorX = (int)_window.CursorX;
                result = ((_lastMouseX - cursorX) * _retro.Width) / _window.Width;
                _lastMouseX = cursorX;
            }
            else
            {
                var cursorY = (int)_window.CursorY;
                result = ((_lastMouseY - cursorY) * _retro.Height) / _window.Height;
                _lastMouseY = cursorY;
            }
            return -result;
        }

        public float MouseWheelDelta(int port, int axis)
        {
            //No support for mouse wheel controls yet
            return 0;
        }

        public void Poll(Wrapper.retro_keyboard_event_t keyboardEventCallback)
        {
            /* Code from old implementation - still required?

        var mouseState = Mouse.GetState();
        var anyMouse =
            _box.IsMouseOnScreen(mouseState.X, mouseState.Y)
            && (mouseState.LeftButton == ButtonState.Pressed
            || mouseState.RightButton == ButtonState.Pressed
            || mouseState.MiddleButton == ButtonState.Pressed);

        _mouseDeltaX = mouseState.X - _lastMouseX;
        _mouseDeltaY = mouseState.Y - _lastMouseY;

        mouseState = Mouse.GetState();
        _lastMouseX = mouseState.X;
        _lastMouseY = mouseState.Y;

        //Special, force quick scroll
        if(_box.IsMouseUp(mouseState.Y))
        {
            _mouseDeltaY = -100;
        }
        if (_box.IsMouseDown(mouseState.Y))
        {
            _mouseDeltaY = 100;
        }
        if (_box.IsMouseLeft(mouseState.X))
        {
            _mouseDeltaX = -100;
        }
        if (_box.IsMouseRight(mouseState.X))
        {
            _mouseDeltaX = 100;
        }

        if (keyboardEventCallback != null)
        {
            var state = Keyboard.GetState();
            var pressedKeys = state.GetPressedKeys();

            var keymap = _inversekeyMap;
            if (pressedKeys.Contains(Keys.LeftWindows) || pressedKeys.Contains(Keys.RightWindows))
            {
                keymap = _inversekeyMapShifted;
            }

            //Keys that have just been pressed
            foreach (var key in pressedKeys)
            {
                if (_lastPressedKeys.Contains(key) == false && keymap.TryGetValue(key, out var value))
                {
                    keyboardEventCallback(true, (uint)value, 0, 0);
                }
            }

            //Keys that have just been released
            foreach (var key in _lastPressedKeys)
            {
                if (pressedKeys.Contains(key) == false && keymap.TryGetValue(key, out var value))
                {
                    keyboardEventCallback(false, (uint)value, 0, 0);
                }
            }

            _lastPressedKeys = pressedKeys;
        }
    }
*/

        }
    }



}