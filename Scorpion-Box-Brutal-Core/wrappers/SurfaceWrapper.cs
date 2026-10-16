using Brutal.GlfwApi;
using Brutal.VulkanApi;

public class SurfaceWrapper : IDisposable
{
    public DeviceWrapper FrameWrapper;
    private WindowState _state;

    public SurfaceWrapper(WindowState state)
    {
        _state = state;
        var result = (VkResult)Glfw.CreateWindowSurface(_state.Vulkan.VkHandle, _state.Window, out var surface);
        if (result != VkResult.Success)
        {
            throw new Exception("Unable to create window surface: " + result);
        }
        _state.Surface = new VkSurfaceKHR((nint)surface);
        FrameWrapper = new DeviceWrapper(_state);
    }

    public void Dispose()
    {
        _state.Vulkan.DestroySurfaceKHR(_state.Surface, null);
        FrameWrapper.Dispose();
    }
}