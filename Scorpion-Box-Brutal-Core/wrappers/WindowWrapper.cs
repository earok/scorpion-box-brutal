using System.Runtime.InteropServices;
using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.OpenGlApi;

namespace ScorpionBox.Windows
{

    public unsafe class WindowWrapper : IDisposable
    {
        public readonly WindowState State;

        // Held in a field so the delegate isn't collected while native code may still call it
        private static readonly GlGetProcAddressFunc GetProcAddress = name =>
            Glfw.GetProcAddress(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(name));

        public WindowWrapper(uint width, uint height, string title)
        {
            State = new WindowState();

            if (!Glfw.Init())
            {
                throw new Exception("Failed to initialize GLFW");
            }

            // OpenGL 3.3 core is the baseline everywhere, forward compatible so macOS accepts it too
            Glfw.WindowHint(GlfwWindowHint.ClientApi, Glfw.Constants.GLFW_OPENGL_API);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMajor, 3);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMinor, 3);
            Glfw.WindowHint(GlfwWindowHint.OpenGlProfile, Glfw.Constants.GLFW_OPENGL_CORE_PROFILE);
            Glfw.WindowHint(GlfwWindowHint.OpenGlForwardCompat, 1);

            State.Window = Glfw.CreateWindow(new GlfwWindow.CreateInfo
            {
                Title = title,
                Size = new int2((int)width, (int)height),
            });

            State.Window.MakeContextCurrent();
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
                if (State.Window.ShouldClose)
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
            State.Window.Dispose();
            Glfw.Terminate();
        }
    }

}