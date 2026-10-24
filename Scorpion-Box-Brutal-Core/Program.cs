using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.Numerics;
using Brutal.OpenGlApi;
using Brutal.TextureApi;
using Brutal.TextureApi.Gli;
using Brutal.VulkanApi.Abstractions;
using ScorpionBox;
using ScorpionBox.Processors;
using ScorpionBox.Services;
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
    scorpionBox.Dispose();
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
        private string _title = "BRUTAL";
        private GlfwKey _keyFullscreen;
        private GlfwKey _keyQuit;
        private GlfwKey _keyVolUp;
        private GlfwKey _keyVolDown;
        private bool _isFullScreen;
        private Dictionary<GlfwKey, bool> _lastKeyState = new Dictionary<GlfwKey, bool>();
        private int _configVolume = 100;
        public TextureService TextureService = new();

        public unsafe void Run()
        {
            if (!Glfw.Init())
            {
                throw new Exception("Failed to initialize GLFW");
            }

            var dll = new DllModuleWindows();
            Retro = new Wrapper(".", dll);
            var portDevices = new Dictionary<uint, uint>();
            var coreOptionsList = new CoreOptionsList();
            ReadConfig(coreOptionsList, portDevices);

            if (Retro.StartGame("Cores", _core, "Games", _game, coreOptionsList, portDevices) == false)
            {
                throw new Exception("Could not start game");
            }

            Window = new WindowWrapper(this, _title, _isFullScreen);
            var graphics = new ScorpionGraphicsProcessor(this);
            _ = new ScorpionInputProcessor(this).SetDeadZone(0.5f);
            var audio = new ScorpionAudioProcessor(this, _configVolume);

            var lastFrameTime = 0.0;
            Glfw.SetTime(0);
            ImGui.CreateContext();
            ImGui.GetIO().IniFilename = null; // Don't write imgui.ini.

            using var glfwBackend = new GlfwBackend(Window.State.GlfwWindow, GlfwBackend.ClientApi.OpenGL);
            using var openGlBackend = new OpenGlBackend();

            var (logoRef, logoSize) = TextureService.FromFile("./Media/LOGO_BRUTAL_WHT.png");
            var volumeTime = 0.0;
            var guiTime = 3;
            var ticks = 0;

            while (Window.IsRunning && IsKeyDown(_keyQuit) == false)
            {
                var time = Glfw.Time;
                var delta = time - lastFrameTime;
                lastFrameTime = time;

                var targetTicks = (int)(time * Retro.FPS);
                if (ticks < targetTicks)
                {
                    Retro.Update();
                    ticks = targetTicks; //Skip over slow frames
                }

                //Toggle fullscreen
                if (IsKeyJustPressed(_keyFullscreen))
                {
                    Window.ToggleFullscreen();
                }

                if (IsKeyDown(_keyVolUp))
                {
                    audio.Volume += (float)(delta * 100);
                    volumeTime = guiTime;
                }
                if (IsKeyDown(_keyVolDown))
                {
                    audio.Volume -= (float)(delta * 100);
                    volumeTime = guiTime;
                }

                //We've received a new graphics buffer
                if (graphics.Pixels != null)
                {
                    glfwBackend.NewFrame();
                    openGlBackend.NewFrame();
                    ImGui.NewFrame();
                    // Recycle the scratch memory that interpolated ImGui strings ($"...") use, once per frame.
                    ImString.SharedStorage.Reset();

                    ImGui.SetNextWindowSize(new float2(Window.State.GlfwWindow.Width, 0));
                    if (ImGui.Begin("Hello ImGui"u8, ImGuiWindowFlags.NoTitleBar
                        | ImGuiWindowFlags.NoBackground
                        | ImGuiWindowFlags.NoMove
                        | ImGuiWindowFlags.NoResize))
                    {
                        if (volumeTime > 0)
                        {
                            int sliderInt = (int)audio.Volume;
                            ImGui.PushItemWidth(-1);
                            if (ImGui.SliderInt(""u8, ref sliderInt, 0, 100))
                            {
                                audio.Volume = sliderInt;
                                volumeTime = guiTime;
                            }
                            ImGui.PopItemWidth();
                            volumeTime -= delta;
                        }

                        if (time < 3)
                        {
                            ImGui.Image(logoRef, logoSize);
                        }
                    }
                    ImGui.SetWindowPos(new float2(0, Window.State.GlfwWindow.Height - ImGui.GetWindowHeight()));
                    ImGui.End();

                    ImGui.Render();

                    var bytesPerPixel = Retro.Game.PixelFormat.BytesPerPixel();
                    var pitchInBytes = graphics.Pitch * bytesPerPixel;
                    Window.State.Renderer.DrawFrame(graphics.Pixels, (uint)graphics.Width, (uint)graphics.Height, (uint)pitchInBytes, Retro.Game.PixelFormat);
                    openGlBackend.RenderDrawData(ImGui.GetDrawData());
                    Window.State.GlfwWindow.SwapBuffers();
                }


            }


        }

        private bool IsKeyJustPressed(GlfwKey key)
        {
            _lastKeyState.TryGetValue(key, out var lastState);
            var newState = IsKeyDown(key);
            var result = lastState == false && newState == true;
            _lastKeyState[key] = newState;
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
                            _configVolume = vol;
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
            TextureService.Dispose();
            if (Window != null)
            {
                Window.Dispose();
            }
            if (Retro != null)
            {
                Retro.StopGame();
            }
            Glfw.Terminate();
        }

    }

}