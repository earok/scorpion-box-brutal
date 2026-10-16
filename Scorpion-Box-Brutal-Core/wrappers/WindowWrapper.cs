using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Scorpion_Box_Brutal_Core;

public class WindowWrapper : IDisposable
{
    public VulkanWrapper VulkanWrapper;
    public readonly WindowState State;

    public WindowWrapper()
    {
        State = new WindowState();

        if (!Glfw.Init())
        {
            throw new Exception("Failed to initialize GLFW");
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, 0);
        State.Window = Glfw.CreateWindow(new GlfwWindow.CreateInfo
        {
            Title = "Brutal",
            Size = new int2(800, 600),
        });

        VulkanWrapper = new VulkanWrapper(State);
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
        VulkanWrapper.Dispose();
        State.Window.Dispose();
        Glfw.Terminate();
    }
}