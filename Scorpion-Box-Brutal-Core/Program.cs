using Brutal.GlfwApi;
using ScorpionBox;
using ScorpionBox.Processors;
using ScorpionBox.Windows;
using SK.Libretro;
using SK.Libretro.Utilities;
using static SK.Libretro.Wrapper;

new ScorpionBoxGame();

namespace ScorpionBox
{

    public class ScorpionBoxGame
    {
        private string _core;
        private string _game;
        public Dictionary<int, Dictionary<retro_device_id_joypad, GlfwKey>> KeyDictionary = [];
        private int _volume = 100;
        private string _title;
        private GlfwKey _keyFullscreen;
        private GlfwKey _keyQuit;
        private GlfwKey _keyVolUp;
        private GlfwKey _keyVolDown;
        private bool _startFullScreen;

        public ScorpionBoxGame()
        {
            try
            {
                var dll = new DllModuleWindows();
                var retro = new Wrapper(".", dll);

                var portDevices = new Dictionary<uint, uint>();
                var coreOptionsList = new CoreOptionsList();
                ReadConfig(coreOptionsList, portDevices);

                if (retro.StartGame("Cores", _core, "Games", _game, coreOptionsList, portDevices) == false)
                {
                    throw new Exception("Could not start game");
                }

                using var window = new WindowWrapper(retro.Width * 3, retro.Height * 3, _title);
                var graphicsProcessor = new ScorpionGraphicsProcessor(retro, window.State.Renderer);
                var inputProcessor = new ScorpionInputProcessor(this, retro, window.State.Window).SetDeadZone(0.5f);
                var audioProcessor = new ScorpionAudioProcessor(retro);

                var ticks = 0;

                while (window.IsRunning)
                {
                    var targetTicks = Glfw.Time * (int)retro.Game.SystemAVInfo.timing.fps;
                    while (ticks < targetTicks)
                    {
                        retro.Update();
                        ticks++;
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal error: " + ex.Message);
            }
            finally
            {

            }
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
                        bool.TryParse(value, out _startFullScreen);
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
    }

}