using Brutal.GlfwApi;

namespace ScorpionBox.Windows
{
    public class WindowState
    {
        public WindowState(GlfwWindow glfwWindow, FrameRenderer renderer)
        {
            GlfwWindow = glfwWindow;
            Renderer = renderer;
        }

        public GlfwWindow GlfwWindow;
        public FrameRenderer Renderer;
    }

}