using Brutal.GlfwApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Scorpion_Box_Brutal_Core;

public class WindowState
{
    public int ApiVersion = (int)VulkanHelpers.Api.VERSION_1_0;
    public readonly HashSet<string> RequiredExtensions = [];
    public Instance Vulkan;
    public GlfwWindow Window;
    public VkSurfaceKHR Surface;
    public PhysicalDevice PhysicalDevice;
    public int QueueFamily;
    public FrameRenderer Renderer;
    public Device Device;
    public VkSwapchainKHR Swapchain;
    public VkExtent2D Extent;
    public VkSurfaceFormatKHR SurfaceFormat;
    public VkRenderPass RenderPass;
    public VkSemaphore[] RenderFinished = [];
    public VkFramebuffer[] Framebuffers = [];

}
