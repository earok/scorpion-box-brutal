using System.Runtime.InteropServices;
using Brutal;
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

        public WindowWrapper(int2 size, string title)
        {
            State = new WindowState();

            Glfw.WindowHint(GlfwWindowHint.ContextVersionMajor, 4);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMinor, 1);
            Glfw.WindowHint(GlfwWindowHint.OpenGlProfile, Glfw.Constants.GLFW_OPENGL_CORE_PROFILE);

            if (Platform.IsMacOS)
            {
                Glfw.WindowHint(GlfwWindowHint.OpenGlForwardCompat, 1);
            }

            State.GlfwWindow = Glfw.CreateWindow(new GlfwWindow.CreateInfo
            {
                Title = title + " - BRUTAL",
                Size = size,
            });

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
    }

}