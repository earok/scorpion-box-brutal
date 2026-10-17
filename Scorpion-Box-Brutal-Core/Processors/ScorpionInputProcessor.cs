/*
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using SK.Libretro;
using static SK.Libretro.Wrapper;

namespace ScorpionBox.Core.Processors;
internal class ScorpionInputProcessor : IInputProcessor
{
    private ScorpionBoxGame _box;
    private bool _useXInput;
    private float _deadZone;
    private float _axisDeadZone;
    private HashSet<Tuple<int, int>> _stableJoystickAxes = [];

    private Dictionary<retro_key, Keys> _keyMap = [];
    private Dictionary<retro_key, Keys> _keyMapShifted = [];
    private Dictionary<Keys, retro_key> _inversekeyMap = [];
    private Dictionary<Keys, retro_key> _inversekeyMapShifted = [];

    private Keys[] _lastPressedKeys = [];
    private int _lastMouseX;
    private int _lastMouseY;
    private float _mouseDeltaX;
    private float _mouseDeltaY;

    public ScorpionInputProcessor(ScorpionBoxGame scorpionBoxGame, bool useXInput, float deadZone)
    {

        var mouseState = Mouse.GetState();

        _box = scorpionBoxGame;
        _useXInput = useXInput;
        _deadZone = deadZone;
        _axisDeadZone = 32767 * deadZone;

        _keyMap[retro_key.RETROK_BACKSPACE] = Keys.Back;
        _keyMap[retro_key.RETROK_TAB] = Keys.Tab;
        _keyMap[retro_key.RETROK_CLEAR] = Keys.OemClear;
        _keyMap[retro_key.RETROK_RETURN] = Keys.Enter;
        _keyMap[retro_key.RETROK_PAUSE] = Keys.Pause;
        _keyMap[retro_key.RETROK_ESCAPE] = Keys.Escape;
        _keyMap[retro_key.RETROK_SPACE] = Keys.Space;
        _keyMap[retro_key.RETROK_QUOTE] = Keys.OemQuotes;
        _keyMap[retro_key.RETROK_PERIOD] = Keys.OemPeriod;
        _keyMap[retro_key.RETROK_SEMICOLON] = Keys.OemSemicolon;
        _keyMap[retro_key.RETROK_COMMA] = Keys.OemComma;
        _keyMap[retro_key.RETROK_EQUALS] = Keys.OemPlus;
        _keyMap[retro_key.RETROK_QUESTION] = Keys.OemQuestion;
        _keyMap[retro_key.RETROK_MINUS] = Keys.OemMinus;
        _keyMap[retro_key.RETROK_LEFTBRACKET] = Keys.OemOpenBrackets;
        _keyMap[retro_key.RETROK_RIGHTBRACKET] = Keys.OemCloseBrackets;
        _keyMap[retro_key.RETROK_BACKSLASH] = Keys.OemBackslash;
        _keyMap[retro_key.RETROK_DELETE] = Keys.Delete;

        //Direct loops
        for (var i = 0; i <= 9; i++)
        {
            _keyMap[retro_key.RETROK_0 + i] = Keys.D0 + i;
        }
        for (var i = 0; i <= 9; i++)
        {
            _keyMap[retro_key.RETROK_KP0 + i] = Keys.NumPad0 + i;
        }
        for (var i = 1; i <= 15; i++)
        {
            _keyMap[retro_key.RETROK_F1 + i - 1] = Keys.F1 + i - 1;
        }
        for (var i = 0; i <= 25; i++)
        {
            _keyMap[retro_key.RETROK_a + i] = Keys.A + i;
        }


        _keyMap[retro_key.RETROK_KP_PERIOD] = Keys.Decimal;
        _keyMap[retro_key.RETROK_KP_DIVIDE] = Keys.Divide;
        _keyMap[retro_key.RETROK_KP_MULTIPLY] = Keys.Multiply;
        _keyMap[retro_key.RETROK_KP_MINUS] = Keys.Subtract;
        _keyMap[retro_key.RETROK_KP_PLUS] = Keys.Add;
        _keyMap[retro_key.RETROK_KP_ENTER] = Keys.Separator;
        _keyMap[retro_key.RETROK_KP_EQUALS] = Keys.Separator; //Not really sure here

        _keyMap[retro_key.RETROK_UP] = Keys.Up;
        _keyMap[retro_key.RETROK_DOWN] = Keys.Down;
        _keyMap[retro_key.RETROK_LEFT] = Keys.Left;
        _keyMap[retro_key.RETROK_RIGHT] = Keys.Right;

        _keyMap[retro_key.RETROK_INSERT] = Keys.Insert;
        _keyMap[retro_key.RETROK_HOME] = Keys.Home;
        _keyMap[retro_key.RETROK_END] = Keys.End;
        _keyMap[retro_key.RETROK_PAGEUP] = Keys.PageUp;
        _keyMap[retro_key.RETROK_PAGEDOWN] = Keys.PageDown;

        _keyMap[retro_key.RETROK_NUMLOCK] = Keys.NumLock;
        _keyMap[retro_key.RETROK_CAPSLOCK] = Keys.CapsLock;
        _keyMap[retro_key.RETROK_SCROLLOCK] = Keys.Scroll;

        _keyMap[retro_key.RETROK_LSHIFT] = Keys.LeftShift;
        _keyMap[retro_key.RETROK_RSHIFT] = Keys.RightShift;

        _keyMap[retro_key.RETROK_LCTRL] = Keys.LeftControl;
        _keyMap[retro_key.RETROK_RCTRL] = Keys.RightControl;

        _keyMap[retro_key.RETROK_LALT] = Keys.LeftAlt;
        _keyMap[retro_key.RETROK_RALT] = Keys.RightAlt;

        _keyMap[retro_key.RETROK_LMETA] = Keys.LeftWindows;
        _keyMap[retro_key.RETROK_RMETA] = Keys.RightWindows;

        _keyMap[retro_key.RETROK_COMPOSE] = Keys.LaunchMail;
        _keyMap[retro_key.RETROK_HELP] = Keys.Help;
        _keyMap[retro_key.RETROK_PRINT] = Keys.PrintScreen; //Should this be print?

        //Shifted keys
        _keyMapShifted[retro_key.RETROK_EXCLAIM] = Keys.D1;
        _keyMapShifted[retro_key.RETROK_AT] = Keys.D2;
        _keyMapShifted[retro_key.RETROK_HASH] = Keys.D3;
        _keyMapShifted[retro_key.RETROK_DOLLAR] = Keys.D4;
        _keyMapShifted[retro_key.RETROK_DOLLAR + 1] = Keys.D5; //Missing definition for % ?
        _keyMapShifted[retro_key.RETROK_CARET] = Keys.D6;
        _keyMapShifted[retro_key.RETROK_AMPERSAND] = Keys.D7;
        _keyMapShifted[retro_key.RETROK_ASTERISK] = Keys.D8;
        _keyMapShifted[retro_key.RETROK_LEFTPAREN] = Keys.D9;
        _keyMapShifted[retro_key.RETROK_RIGHTPAREN] = Keys.D0;

        _keyMapShifted[retro_key.RETROK_QUOTEDBL] = Keys.OemQuotes;
        _keyMapShifted[retro_key.RETROK_PLUS] = Keys.OemPlus;
        _keyMapShifted[retro_key.RETROK_SLASH] = Keys.OemQuestion;
        _keyMapShifted[retro_key.RETROK_COLON] = Keys.OemSemicolon;
        _keyMapShifted[retro_key.RETROK_LESS] = Keys.OemComma;
        _keyMapShifted[retro_key.RETROK_GREATER] = Keys.OemPeriod;
        _keyMapShifted[retro_key.RETROK_UNDERSCORE] = Keys.OemMinus;
        _keyMapShifted[retro_key.RETROK_BACKQUOTE] = Keys.OemTilde;

        _keyMapShifted[retro_key.RETROK_LEFTBRACKET] = Keys.OemOpenBrackets;
        _keyMapShifted[retro_key.RETROK_RIGHTBRACKET] = Keys.OemCloseBrackets;
        _keyMapShifted[retro_key.RETROK_BAR] = Keys.OemBackslash;
        _keyMapShifted[retro_key.RETROK_TILDE] = Keys.OemTilde;

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

        /* unknown?
_keyMap[retro_key.RETROK_LSUPER] = Keys.LeftWindows;
_keyMap[retro_key.RETROK_RSUPER] = Keys.RightWindows;
_keyMap[retro_key.RETROK_MODE] = //Function lock key?
_keyMap[retro_key.RETROK_SYSREQ] = 
_keyMap[retro_key.RETROK_BREAK] =
_keyMap[retro_key.RETROK_MENU] =
_keyMap[retro_key.RETROK_POWER]
_keyMap[retro_key.RETROK_EURO]  //Map to dollar?
_keyMap[retro_key.RETROK_UNDO]  //Map to dollar?
_keyMap[retro_key.RETROK_OEM_102]
 

    }

    public bool JoypadButton(int port, int button)
    {
        var state = Keyboard.GetState();
        if (_box.KeyDictionary.TryGetValue(port, out var portData))
        {
            if (portData.TryGetValue((retro_device_id_joypad)button, out var key))
            {
                if (state.IsKeyDown(key))
                {
                    return true;
                }
            }
        }

        if (_useXInput)
        {
            var gpState = GamePad.GetState(port);
            if (gpState.IsConnected)
            {
                switch ((retro_device_id_joypad)button)
                {
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_LEFT:
                        return gpState.DPad.Left == ButtonState.Pressed || gpState.ThumbSticks.Left.X < -_deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_RIGHT:
                        return gpState.DPad.Right == ButtonState.Pressed || gpState.ThumbSticks.Left.X > _deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_DOWN:
                        return gpState.DPad.Down == ButtonState.Pressed || gpState.ThumbSticks.Left.Y < -_deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_UP:
                        return gpState.DPad.Up == ButtonState.Pressed || gpState.ThumbSticks.Left.Y > _deadZone;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_A:
                        return gpState.IsButtonDown(Buttons.A);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_B:
                        return gpState.IsButtonDown(Buttons.B);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_X:
                        return gpState.IsButtonDown(Buttons.X);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_Y:
                        return gpState.IsButtonDown(Buttons.Y);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_START:
                        return gpState.IsButtonDown(Buttons.Start);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_SELECT:
                        return gpState.IsButtonDown(Buttons.Back);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_L:
                        return gpState.IsButtonDown(Buttons.LeftShoulder);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_R:
                        return gpState.IsButtonDown(Buttons.RightShoulder);
                }
            }
        }
        else
        {
            var joystick = Joystick.GetState(port);
            if (joystick.IsConnected) 
            {
                switch ((retro_device_id_joypad)button)
                {
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_LEFT:
                        return TestJoystickAxis(joystick, port, 0) < 0;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_RIGHT:
                        return TestJoystickAxis(joystick, port, 0) > 0;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_DOWN:
                        return TestJoystickAxis(joystick, port, 1) > 0;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_UP:
                        return TestJoystickAxis(joystick, port, 1) < 0;
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_A:
                        return TestJoystickButton(joystick, 0);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_B:
                        return TestJoystickButton(joystick, 1);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_X:
                        return TestJoystickButton(joystick, 2);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_Y:
                        return TestJoystickButton(joystick, 3);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_L:
                        return TestJoystickButton(joystick, 4);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_R:
                        return TestJoystickButton(joystick, 5);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_START:
                        return TestJoystickButton(joystick, 6);
                    case retro_device_id_joypad.RETRO_DEVICE_ID_JOYPAD_SELECT:
                        return TestJoystickButton(joystick, 7);
                }
            }
        }

        return false;
    }

    private int TestJoystickAxis(JoystickState joystick, int port, int axis)
    {
        //Check the "hats" (dpads)
        foreach (var hat in joystick.Hats)
        {
            if (axis == 0)
            {
                if (hat.Left == ButtonState.Pressed)
                {
                    return -1;
                }
                if (hat.Right == ButtonState.Pressed)
                {
                    return 1;
                }
            }
            else
            {
                if (hat.Up == ButtonState.Pressed)
                {
                    return -1;
                }
                if (hat.Down == ButtonState.Pressed)
                {
                    return 1;
                }
            }
        }

        while (axis < joystick.Axes.Length)
        {
            var result = joystick.Axes[axis];
            var index = new Tuple<int, int>(port, axis);

            //We'll consider an axis stable if it's at zero
            if (_stableJoystickAxes.Contains(index) == false)
            {
                //Not stable yet
                if (result > _axisDeadZone || result < -_axisDeadZone)
                {
                    return 0;
                }
                _stableJoystickAxes.Add(index);
            }

            if (result > _axisDeadZone)
            {
                return 1;
            }
            if (result < -_axisDeadZone)
            {
                return -1;
            }
            axis += 2;
        }

        return 0;
    }

    private bool TestJoystickButton(JoystickState joystick, int index)
    {
        if (index < joystick.Buttons.Count()
            && joystick.Buttons[index] == ButtonState.Pressed)
        {
            return true;
        }
        return false;
    }

    public bool MouseButton(int port, int button)
    {
        //Port is ignored, we don't support multi mice
        var state = Mouse.GetState();

        var result = button switch
        {
            0 => state.LeftButton == ButtonState.Pressed,
            1 => state.RightButton == ButtonState.Pressed,
            2 => state.MiddleButton == ButtonState.Pressed,
            _ => false
        };

        return result;
    }

    public float MouseDelta(int port, int axis)
    {
        return axis switch
        {
            0 => _box.ScaleX(_mouseDeltaX),
            1 => _box.ScaleY(_mouseDeltaY),
            _ => 0,
        };
    }

    public float MouseWheelDelta(int port, int axis)
    {
        return 0;
    }

    public bool Key(int id)
    {
        var state = Keyboard.GetState();
        var dict = _keyMap;
        if (state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))
        {
            dict = _keyMapShifted;
        }

        if (dict.ContainsKey((retro_key)id) == false)
        {
            return false;
        }
        var mappedKey = dict[(retro_key)id];
        return state.IsKeyDown(mappedKey);

    }


    public void Poll(retro_keyboard_event_t keyboardEventCallback)
    {
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
}
*/