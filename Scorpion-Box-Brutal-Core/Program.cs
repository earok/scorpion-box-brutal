using System.Runtime.InteropServices;
using Scorpion_Box_Brutal_Core;
using ScorpionBox.Core.Processors;
using SK.Libretro;
using SK.Libretro.Utilities;
using static SK.Libretro.Wrapper;

new ScorpionBoxGame();

public class ScorpionBoxGame
{
    public byte[] Pixels;
    private string _core;
    private string _game;

    public ScorpionBoxGame()
    {
        var dll = new DllModuleWindows();
        var retro = new Wrapper(".", dll);

        var portDevices = new Dictionary<uint, uint>();
        var coreOptionsList = new CoreOptionsList();
        ReadConfig(coreOptionsList, portDevices);

        if(retro.StartGame("Cores", _core, "Games", _game, coreOptionsList, portDevices) == false)
        {
            throw new Exception("ERROR");
        }

        using var window = new WindowWrapper();

        // Stand-in for a libretro core: a 320x240 XRGB8888 frame with padded rows, as cores often supply
        int width = (int)retro.Game.SystemAVInfo.geometry.base_width;
        int height = (int)retro.Game.SystemAVInfo.geometry.base_height;

        Pixels = new byte[width * height * retro.BytesPerPixel];

        var processor = new ScorpionGraphicsProcessor(this);
        retro.ActivateGraphics(processor);


        var tick = 0;
        while (window.IsRunning)
        {
            window.State.Renderer.DrawFrame(MemoryMarshal.AsBytes(Pixels.AsSpan()), width, height, width * retro.BytesPerPixel, retro.Game.PixelFormat);
            retro.Update();
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
//                    Window.Title = value;
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
                       // _volume = vol;
                    }
                    break;

                case "key_fullscreen":
                  //  Enum.TryParse<Keys>(value, true, out _keyFullscreen);
                    break;

                case "key_quit":
                 //   Enum.TryParse<Keys>(value, true, out _keyQuit);
                    break;

                case "key_vol+":
                 //   Enum.TryParse<Keys>(value, true, out _keyVolUp);
                    break;

                case "key_vol-":
                 //   Enum.TryParse<Keys>(value, true, out _keyVolDown);
                    break;

                case "fullscreen":
               //     bool.TryParse(value, out _startFullScreen);
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
                        /*
                        key = key["key_".Length..];
                        var lastDash = key.LastIndexOf('_');

                        //This is for a specific controller
                        if (int.TryParse(key.AsSpan(lastDash + 1), out var controller))
                        {
                            key = key[0..lastDash];
                        }
                        if (Enum.TryParse<retro_device_id_joypad>(key, true, out var result)
                            && Enum.TryParse<Keys>(value, true, out var keyResult))
                        {
                            if (KeyDictionary.ContainsKey(controller) == false)
                            {
                                KeyDictionary[controller] = [];
                            }
                            KeyDictionary[controller][result] = keyResult;
                        }
                        */
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

}

