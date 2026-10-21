using Brutal.GlfwApi;
using Brutal.Numerics;
using ScorpionBox;
using ScorpionBox.Processors;
using ScorpionBox.Windows;
using SK.Libretro;
using SK.Libretro.Utilities;
using static SK.Libretro.Wrapper;

var scorpionBox = new ScorpionBoxGame();
try
{
    scorpionBox.Run();
}
catch (Exception ex)
{
    Console.WriteLine("Fatal error: " + ex.Message);
}
finally
{
    if (scorpionBox != null)
    {
        scorpionBox.Dispose();
    }
}


namespace ScorpionBox
{
    public class ScorpionBoxGame : IDisposable
    {
        public Dictionary<int, Dictionary<retro_device_id_joypad, GlfwKey>> KeyDictionary = [];
        public WindowWrapper Window;
        public Wrapper Retro;

        private string _core;
        private string _game;
        private int _volume = 100;
        private string _title = "BRUTAL";
        private GlfwKey _keyFullscreen;
        private GlfwKey _keyQuit;
        private GlfwKey _keyVolUp;
        private GlfwKey _keyVolDown;
        private bool _isFullScreen;

        public void Run()
        {
            if (!Glfw.Init())
            {
                throw new Exception("Failed to initialize GLFW");
            }

            //Remember the default video mode sizes
            var monitor = Glfw.PrimaryMonitor;
            var middleOfScreen = new int2(monitor.VideoMode.Width, monitor.VideoMode.Height) / 2;

            var dll = new DllModuleWindows();
            Retro = new Wrapper(".", dll);
            var portDevices = new Dictionary<uint, uint>();
            var coreOptionsList = new CoreOptionsList();
            ReadConfig(coreOptionsList, portDevices);

            if (Retro.StartGame("Cores", _core, "Games", _game, coreOptionsList, portDevices) == false)
            {
                throw new Exception("Could not start game");
            }

            var bestMultiplierWidth = monitor.VideoMode.Width / (int)Retro.Width;
            var bestMultiplierHeight = monitor.VideoMode.Height / (int)Retro.Height;
            var bestMultiplier = Math.Min(bestMultiplierWidth,bestMultiplierHeight);

            var size = new int2((int)Retro.Width, (int)Retro.Height) * bestMultiplier;
            Window = new WindowWrapper(size, _title);
            _ = new ScorpionGraphicsProcessor(this);
            _ = new ScorpionInputProcessor(this).SetDeadZone(0.5f);
            _ = new ScorpionAudioProcessor(this);

            var ticks = 0;
            var fullscreenKeyState = false;

            while (Window.IsRunning && IsKeyDown(_keyQuit) == false)
            {
                var targetTicks = Glfw.Time * (int)Retro.Game.SystemAVInfo.timing.fps;
                while (ticks < targetTicks)
                {
                    Retro.Update();
                    ticks++;
                }

                if (IsKeyJustPressed(_keyFullscreen, ref fullscreenKeyState))
                {

                    //Regenerate window here
                    if (Window.State.GlfwWindow.IsFullscreen)
                    {
                        Window.State.GlfwWindow.SetWindowed(middleOfScreen - (size / 2), size);
                    }
                    else
                    {
                        Window.State.GlfwWindow.SetFullscreen(Glfw.PrimaryMonitor, size, (int)Retro.Game.SystemAVInfo.timing.fps);
                    }
                }
            }

        }

        private bool IsKeyJustPressed(GlfwKey key, ref bool lastState)
        {
            var newState = IsKeyDown(key);
            var result = lastState == false && newState == true;
            lastState = result;
            return result;
        }

        private bool IsKeyDown(GlfwKey key)
        {
            return Window.State.GlfwWindow.GetKey(key) != GlfwKeyAction.Release;
        }

        private void ReadConfig(CoreOptionsList coreOptionsList, Dictionary<uint, uint> portDevices)
        {
            var path = "config.txt";
            if (File.Exists(path) == false)
            {
                return;
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.StartsWith('#'))
                {
                    continue;
                }

                var com = line.IndexOf('#');
                if (com > -1)
                {
                    line = line[..(com - 1)].Trim();
                    if (line.Length == 0)
                        continue;
                }

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                line = line.Trim();
                if (line.Length == 0)
                    continue;

                var key = line[..eq].Trim().ToLowerInvariant();
                var value = line[(eq + 1)..].Trim();

                switch (key)
                {
                    case "title":
                        _title = value;
                        break;

                    case "core":
                        _core = value;
                        break;

                    case "game":
                        _game = value;
                        break;

                    case "volume":
                        if (int.TryParse(value, out var vol))
                        {
                            _volume = vol;
                        }
                        break;

                    case "key_fullscreen":
                        _keyFullscreen = ParseKey(value);
                        break;

                    case "key_quit":
                        _keyQuit = ParseKey(value);
                        break;

                    case "key_vol+":
                        _keyVolUp = ParseKey(value);
                        break;

                    case "key_vol-":
                        _keyVolDown = ParseKey(value);
                        break;

                    case "fullscreen":
                        bool.TryParse(value, out _isFullScreen);
                        break;

                    default:

                        if (key.StartsWith("c_"))
                        {
                            key = key["c_".Length..];
                            var core = coreOptionsList.Cores.FirstOrDefault(p => p.CoreName == key);
                            if (core == null)
                            {
                                core = new CoreOptions() { CoreName = key, Options = new List<string>() };
                                coreOptionsList.Cores.Add(core);
                            }
                            core.Options.Add(value);
                        }
                        else if (key.StartsWith("key_retro_device_id"))
                        {
                            key = key["key_".Length..];
                            var lastDash = key.LastIndexOf('_');

                            //This is for a specific controller
                            if (int.TryParse(key.AsSpan(lastDash + 1), out var controller))
                            {
                                key = key[0..lastDash];
                            }
                            if (Enum.TryParse<retro_device_id_joypad>(key, true, out var result))
                            {
                                var keyResult = ParseKey(value);
                                if (KeyDictionary.ContainsKey(controller) == false)
                                {
                                    KeyDictionary[controller] = [];
                                }
                                KeyDictionary[controller][result] = keyResult;
                            }
                        }
                        else if (key.StartsWith("port_"))
                        {
                            key = key["port_".Length..];
                            if (uint.TryParse(key, out var port))
                            {
                                uint device = (uint)retro_device.RETRO_DEVICE_NONE;
                                if (Enum.TryParse<retro_device>(value, out var retro))
                                {
                                    //Used enum
                                    device = (uint)retro;
                                }
                                else
                                {
                                    //Try to read it as an exact device id
                                    uint.TryParse(value, out var number);
                                }

                                portDevices[port] = device;
                            }
                        }
                        break;
                }
            }
        }

        private static GlfwKey ParseKey(string value)
        {
            if (Enum.TryParse<GlfwKey>(value, true, out var result))
            {
                return result;
            }
            throw new Exception("Invalid key:" + value);
        }

        public void Dispose()
        {
            Window.Dispose();
            if (Retro != null)
            {
                Retro.StopGame();
            }
        }

    }

}