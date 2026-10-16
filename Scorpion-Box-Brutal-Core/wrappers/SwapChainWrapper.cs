using Brutal.VulkanApi;
using Scorpion_Box_Brutal_Core;

public class SwapChainWrapper : IDisposable
{
    private WindowState _state;

    private VkImage[] _images = [];
    private VkImageView[] _imageViews = [];

    public unsafe SwapChainWrapper(WindowState state)
    {
        _state = state;

        var capabilities = _state.PhysicalDevice.GetSurfaceCapabilitiesKHR(_state.Surface);
        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        // A current extent of 0xFFFFFFFF means the surface size is determined by the swapchain
        if (capabilities.CurrentExtent.Width != -1)
        {
            _state.Extent = capabilities.CurrentExtent;
        }
        else
        {
            var size = _state.Window.FramebufferSize;
            _state.Extent = new VkExtent2D(
                Math.Clamp(size.X, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
                Math.Clamp(size.Y, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height)
            );
        }

        _state.SurfaceFormat = ChooseSurfaceFormat();
        _state.RenderPass = CreateRenderPass();

        //Todo create allocator?
        _state.Swapchain = _state.Device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _state.Surface,
                MinImageCount = imageCount,
                ImageFormat = state.SurfaceFormat.Format,
                ImageColorSpace = state.SurfaceFormat.ColorSpace,
                ImageExtent = _state.Extent,
                ImageArrayLayers = 1,
                ImageUsage = VkImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = VkSharingMode.Exclusive,
                PreTransform = capabilities.CurrentTransform,
                CompositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
                PresentMode = VkPresentModeKHR.FifoKHR, // vsync, always supported
                Clipped = true,
            },
            null
        );

        _images = new VkImage[_state.Device.GetSwapchainImagesKHR_Count(_state.Swapchain)];
        _state.Device.GetSwapchainImagesKHR_Write(_state.Swapchain, _images);

        _imageViews = new VkImageView[_images.Length];
        _state.Framebuffers = new VkFramebuffer[_images.Length];
        _state.RenderFinished = new VkSemaphore[_images.Length];        

        for (var i = 0; i < _images.Length; i++)
        {
            _imageViews[i] = CreateImageView(_state.Device, _images[i], _state.SurfaceFormat.Format);

            var attachment = _imageViews[i];
            _state.Framebuffers[i] = _state.Device.CreateFramebuffer(
                new VkFramebufferCreateInfo
                {
                    RenderPass = _state.RenderPass,
                    AttachmentCount = 1,
                    Attachments = &attachment,
                    Width = _state.Extent.Width,
                    Height = _state.Extent.Height,
                    Layers = 1,
                },
                null
            );

            _state.RenderFinished[i] = _state.Device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        }        

        _state.Renderer = new FrameRenderer(_state);
    }

    private unsafe VkRenderPass CreateRenderPass()
    {
        var colorAttachment = new VkAttachmentDescription
        {
            Format = _state.SurfaceFormat.Format,
            Samples = VkSampleCountFlags._1Bit,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            StencilLoadOp = VkAttachmentLoadOp.DontCare,
            StencilStoreOp = VkAttachmentStoreOp.DontCare,
            InitialLayout = VkImageLayout.Undefined,
            FinalLayout = VkImageLayout.PresentSrcKHR,
        };

        var colorReference = new VkAttachmentReference
        {
            Attachment = 0,
            Layout = VkImageLayout.ColorAttachmentOptimal,
        };

        var subpass = new VkSubpassDescription
        {
            PipelineBindPoint = VkPipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            ColorAttachments = &colorReference,
        };

        // Wait for the acquired image to be released by the presentation engine before writing to it
        var dependency = new VkSubpassDependency
        {
            SrcSubpass = VK.SUBPASS_EXTERNAL,
            DstSubpass = 0,
            SrcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = VkAccessFlags.None,
            DstAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
        };

        return _state.Device.CreateRenderPass(
            new VkRenderPassCreateInfo
            {
                AttachmentCount = 1,
                Attachments = &colorAttachment,
                SubpassCount = 1,
                Subpasses = &subpass,
                DependencyCount = 1,
                Dependencies = &dependency,
            },
            null
        );
    }

    public static VkImageView CreateImageView(Device device, VkImage image, VkFormat format)
    {
        return device.CreateImageView(
            new VkImageViewCreateInfo
            {
                Image = image,
                ViewType = VkImageViewType._2D,
                Format = format,
                SubresourceRange = new VkImageSubresourceRange
                {
                    AspectMask = VkImageAspectFlags.ColorBit,
                    LevelCount = 1,
                    LayerCount = 1,
                },
            },
            null
        );
    }

    public void Dispose()
    {
        _state.Renderer.Dispose();
        _state.Device.DestroySwapchainKHR(_state.Swapchain, null);
        for (var i = 0; i < _images.Length; i++)
        {
            _state.Device.DestroyFramebuffer(_state.Framebuffers[i], null);
            _state.Device.DestroyImageView(_imageViews[i], null);
            _state.Device.DestroySemaphore(_state.RenderFinished[i], null);
        }        
        _state.Device.DestroyRenderPass(_state.RenderPass, null);
    }

    private VkSurfaceFormatKHR ChooseSurfaceFormat()
    {
        Span<VkSurfaceFormatKHR> formats = stackalloc VkSurfaceFormatKHR[
            _state.PhysicalDevice.GetSurfaceFormatsKHR_Count(_state.Surface)
        ];
        _state.PhysicalDevice.GetSurfaceFormatsKHR_Write(_state.Surface, formats);

        // Emulator output is already gamma encoded, so use a UNORM swapchain to pass pixel values through unchanged
        foreach (var format in formats)
            if (
                format.Format is VkFormat.B8G8R8A8UNorm or VkFormat.R8G8B8A8UNorm
                && format.ColorSpace == VkColorSpaceKHR.SRGBNonlinearKHR
            )
                return format;

        return formats[0];
    }

/*
    private void RecreateSwapchain()
    {
        // A minimised window has a zero-sized framebuffer, wait until it is restored
        var size = _state.Window.FramebufferSize;
        while (size.X == 0 || size.Y == 0)
        {
            Glfw.WaitEvents();
            size = _state.Window.FramebufferSize;
        }

        _device.WaitIdle();
        DestroySwapchain();
        CreateSwapchain();
    }
*/

}