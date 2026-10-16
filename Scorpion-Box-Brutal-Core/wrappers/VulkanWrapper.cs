using System.Runtime.InteropServices;
using Brutal.GlfwApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

public unsafe class VulkanWrapper : IDisposable
{
    public readonly SurfaceWrapper SurfaceWrapper;
    private WindowState _state;

    public VulkanWrapper(WindowState state)
    {
        _state = state;
        if (!Glfw.VulkanSupported())
        {
            throw new Exception("Vulkan not supported");
        }

        VulkanHelpers.AddPlatformRequiredExtensions(_state.RequiredExtensions);
        var glfwExtensions = Glfw.GetRequiredInstanceExtensions(out var extensionCount);
        for (var i = 0; i < extensionCount; i++)
        {
            _state.RequiredExtensions.Add(Marshal.PtrToStringUTF8((nint)glfwExtensions[i])!);
        }

        // Validation layers are only present when the Vulkan SDK is installed
        var layers = new HashSet<string>();
        if (VulkanHelpers.Validation.IsSupported)
        {
            VulkanHelpers.AddValidationLayers(layers);
        }

        Instance.CreateInfo createInfo;
        fixed (byte* appName = "Hello Triangle"u8)
        fixed (byte* engineName = "No Engine"u8)
        {
            createInfo = new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo
                {
                    ApplicationName = appName,
                    ApplicationVersion = 0,
                    EngineName = engineName,
                    EngineVersion = 0,
                    ApiVersion = _state.ApiVersion,
                },
                Flags = VulkanHelpers.GetPlatformRequiredInstanceCreateFlags(),
                EnabledExtensions = _state.RequiredExtensions,
                EnabledLayers = layers,
            };
        }

        _state.Vulkan = Vulkan.CreateInstance(createInfo);
        SurfaceWrapper = new SurfaceWrapper(_state);

    }

    public void Dispose()
    {
        SurfaceWrapper.Dispose();
        _state.Vulkan.Dispose();
    }
}