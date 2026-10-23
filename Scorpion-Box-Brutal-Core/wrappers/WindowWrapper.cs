using System.Runtime.InteropServices;
using Brutal;
using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.OpenGlApi;
using SK.Libretro;

namespace ScorpionBox.Windows
{

    public unsafe class WindowWrapper : IDisposable
    {
        public readonly WindowState State;
        private bool _fullscreen;
        private int2 _oldSize;
        private int2 _oldPosition;
        private int2 _fullscreenSize;

        private static readonly GlGetProcAddressFunc GetProcAddress = name =>
            Glfw.GetProcAddress(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(name));

        public WindowWrapper(Wrapper retro, string title, bool fullscreen)
        {
            State = new WindowState();
            _fullscreen = fullscreen;

            //Remember the default video mode sizes
            var monitor = Glfw.PrimaryMonitor;
            _fullscreenSize = new int2(monitor.VideoMode.Width, monitor.VideoMode.Height);
            var bestMultiplierWidth = monitor.VideoMode.Width / (int)retro.Width;
            var bestMultiplierHeight = monitor.VideoMode.Height / (int)retro.Height;
            var bestMultiplier = Math.Min(bestMultiplierWidth, bestMultiplierHeight);

            Glfw.WindowHint(GlfwWindowHint.ContextVersionMajor, 4);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMinor, 1);
            Glfw.WindowHint(GlfwWindowHint.OpenGlProfile, Glfw.Constants.GLFW_OPENGL_CORE_PROFILE);

            if (Platform.IsMacOS)
            {
                Glfw.WindowHint(GlfwWindowHint.OpenGlForwardCompat, 1);
            }

            _oldSize = new int2((int)retro.Width * bestMultiplier, (int)retro.Height * bestMultiplier);
            _oldPosition = (_fullscreenSize / 2) - (_oldSize / 2);

            State.GlfwWindow = Glfw.CreateWindow(new GlfwWindow.CreateInfo
            {
                Title = title + " - BRUTAL",
                Size = _oldSize,
            });

            SetFullscreen(fullscreen);

            State.GlfwWindow.MakeContextCurrent();
            if (!Glad.Initialize(GetProcAddress))
            {
                throw new Exception("Failed to load OpenGL functions");
            }

            // Vsync
            Glfw.SwapInterval(1);

            State.Renderer = new FrameRenderer(State);
        }

        public bool IsRunning
        {
            get
            {
                if (State.GlfwWindow.ShouldClose)
                {
                    return false;
                }
                Glfw.PollEvents();
                return true;
            }
        }


        public void Dispose()
        {
            State.Renderer.Dispose();
            State.GlfwWindow.Dispose();
            Glfw.Terminate();
        }

        internal void ToggleFullscreen()
        {
            SetFullscreen(!_fullscreen);
        }

        private void SetFullscreen(bool fullscreen)
        {
            _fullscreen = fullscreen;

            if (fullscreen)
            {
                _oldPosition = State.GlfwWindow.Position;
                _oldSize = State.GlfwWindow.Size;
                State.GlfwWindow.SetAttribute(GlfwWindowAttribute.Decorated, 0);
                State.GlfwWindow.SetWindowed(int2.Zero - new int2(0, 1), _fullscreenSize + new int2(0, 2));
            }
            else
            {
                State.GlfwWindow.SetAttribute(GlfwWindowAttribute.Decorated, 1);
                State.GlfwWindow.SetWindowed(_oldPosition, _oldSize);
            }
        }


    }

}