using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Scorpion_Box_Brutal_Core;

public class DeviceWrapper : IDisposable
{
    private VkPhysicalDeviceProperties _properties;
    private WindowState _state;
    private SwapChainWrapper _swapChainWrapper;

    public unsafe DeviceWrapper(WindowState state)
    {
        _state = state;
        var deviceExtensions = new HashSet<string>();
        VulkanHelpers.AddSwapchainExtensions(deviceExtensions);

        //Find a physical device
        foreach (var physicalDevice in _state.Vulkan.PhysicalDevices)
        {
            var queueFamily = SupportedQueueFamily(physicalDevice, deviceExtensions);
            if (queueFamily > -1)
            {
                _state.PhysicalDevice = physicalDevice;
                _state.QueueFamily = queueFamily;
            }
        }

        if (_state.PhysicalDevice == null)
        {
            throw new Exception("Failed to find a suitable GPU!");
        }
        VulkanHelpers.AddMandatoryIfSupportedDeviceExtensions(deviceExtensions, _state.PhysicalDevice);

        var priorities = 1f;
        _state.Device = _state.PhysicalDevice.CreateDevice(new Device.CreateInfo()
        {
            Extensions = deviceExtensions,
            QueueCreateInfos = [new VkDeviceQueueCreateInfo(){
                QueueFamilyIndex = _state.QueueFamily,
                QueueCount = 1,
                QueuePriorities = &priorities,
            }]
        });

        _swapChainWrapper = new SwapChainWrapper(_state);
    }

    private int SupportedQueueFamily(PhysicalDevice physicalDevice, HashSet<string> deviceExtensions)
    {
        physicalDevice.GetProperties(ref _properties);

        if (_properties.ApiVersion < _state.ApiVersion)
        {
            return -1; //API support is too old
        }

        foreach (var extension in deviceExtensions)
        {
            if (physicalDevice.SupportsExtension(extension) == false)
            {
                return -1; //Doesn't support this specific extension
            }
        }

        //Go with the first queue family that supports graphics
        //Todo - need to do a for loop here
        var result = physicalDevice.GetFirstQueueFamilyWithFlag(VkQueueFlags.GraphicsBit);

        //Make sure it actually works with the surface
        if (physicalDevice.GetSurfaceSupportKHR(result, _state.Surface) == false)
        {
            return -1;
        }

        return result;
    }

    public void Dispose()
    {
        _swapChainWrapper.Dispose();
        _state.Device.Dispose();
    }

            

}