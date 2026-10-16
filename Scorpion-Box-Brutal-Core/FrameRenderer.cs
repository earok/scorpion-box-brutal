using System.Data;
using Brutal;
using Brutal.GlfwApi;
using Brutal.Pointers;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Scorpion_Box_Brutal_Core;

/// <summary>
/// Pixel layouts a frame can be supplied in, matching libretro's <c>retro_pixel_format</c>
/// </summary>
public enum FramePixelFormat
{
    /// <summary> 16-bit 0RGB1555, the libretro default </summary>
    XRGB1555 = 0,

    /// <summary> 32-bit XRGB8888, stored as B,G,R,X bytes in memory </summary>
    XRGB8888 = 1,

    /// <summary> 16-bit RGB565 </summary>
    RGB565 = 2,
}

/// <summary>
/// Vulkan renderer that uploads a CPU-side framebuffer each frame and stretches it over the whole window
/// </summary>
public sealed unsafe class FrameRenderer : IDisposable
{
    private const int MaxFramesInFlight = 2;

    // Fullscreen triangle generated from the vertex index, no vertex buffer needed
    private const string VertexShaderSource = """
        #version 450
        layout(location = 0) out vec2 uv;

        void main() {
            uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
            gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    // Alpha is forced to 1 since the X channel of the source formats is undefined
    private const string FragmentShaderSource = """
        #version 450
        layout(set = 0, binding = 0) uniform sampler2D frame;
        layout(location = 0) in vec2 uv;
        layout(location = 0) out vec4 outColor;

        void main() {
            outColor = vec4(texture(frame, uv).rgb, 1.0);
        }
        """;


    private Device Device => _state.Device;
    private VkSwapchainKHR Swapchain => _state.Swapchain;
    private VkExtent2D Extent => _state.Extent;

    private readonly Queue _queue;

    private readonly VkDescriptorSetLayout _descriptorSetLayout;
    private readonly VkPipelineLayout _pipelineLayout;
    private readonly VkPipeline _pipeline;
    private readonly VkCommandPool _commandPool;
    private readonly VkSampler _sampler;
    private readonly VkDescriptorPool _descriptorPool;
    private readonly VkDescriptorSet _descriptorSet;



    // One "render finished" semaphore per swapchain image, as the image may still be queued for present

    private readonly CommandBuffer[] _commandBuffers = new CommandBuffer[MaxFramesInFlight];
    private readonly VkSemaphore[] _imageAvailable = new VkSemaphore[MaxFramesInFlight];
    private readonly VkFence[] _inFlight = new VkFence[MaxFramesInFlight];
    private int _frame;

    // The texture the frame is sampled from, recreated whenever the frame size or format changes
    private VkImage _texture;
    private VkDeviceMemory _textureMemory;
    private VkImageView _textureView;
    private VkFormat _textureFormat;
    private VkExtent2D _textureExtent;
    private bool _textureHasContents;
    private WindowState _state;

    // Persistently mapped host-visible buffers the CPU writes frames into, one per frame in flight

    private readonly StagingBuffer[] _staging = new StagingBuffer[MaxFramesInFlight];

    private struct StagingBuffer
    {
        public VkBuffer Buffer;
        public VkDeviceMemory Memory;
        public Ptr Mapped;
        public int Size;
    }

    public FrameRenderer(WindowState state, bool linearFiltering = false)
    {
        _state = state;
        _queue = Device.GetQueue(_state.QueueFamily, 0);

        var filter = linearFiltering ? VkFilter.Linear : VkFilter.Nearest;
        _sampler = Device.CreateSampler(
            new VkSamplerCreateInfo
            {
                MagFilter = filter,
                MinFilter = filter,
                MipmapMode = VkSamplerMipmapMode.Nearest,
                AddressModeU = VkSamplerAddressMode.ClampToEdge,
                AddressModeV = VkSamplerAddressMode.ClampToEdge,
                AddressModeW = VkSamplerAddressMode.ClampToEdge,
                MaxLod = 0,
            },
            null
        );

        var binding = new VkDescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = VkShaderStageFlags.FragmentBit,
        };
        _descriptorSetLayout = Device.CreateDescriptorSetLayout(
            new VkDescriptorSetLayoutCreateInfo { BindingCount = 1, Bindings = &binding },
            null
        );

        var poolSize = new VkDescriptorPoolSize
        {
            Type = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
        };
        _descriptorPool = Device.CreateDescriptorPool(
            new VkDescriptorPoolCreateInfo
            {
                MaxSets = 1,
                PoolSizeCount = 1,
                PoolSizes = &poolSize,
            },
            null
        );

        var descriptorSetLayout = _descriptorSetLayout;
        VkDescriptorSet descriptorSet;
        Device.AllocateDescriptorSets(
            new VkDescriptorSetAllocateInfo
            {
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                SetLayouts = &descriptorSetLayout,
            },
            new ReadOnlySpan<VkDescriptorSet>(&descriptorSet, 1)
        );
        _descriptorSet = descriptorSet;

        _pipelineLayout = Device.CreatePipelineLayout([_descriptorSetLayout], [], null);
        _pipeline = CreatePipeline();

        _commandPool = Device.CreateCommandPool(
            new VkCommandPoolCreateInfo
            {
                Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit,
                QueueFamilyIndex = _state.QueueFamily,
            },
            null
        );

        Device.AllocateCommandBuffers(
            new VkCommandBufferAllocateInfo { CommandPool = _commandPool, Level = VkCommandBufferLevel.Primary },
            _commandBuffers
        );

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            _imageAvailable[i] = Device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
            _inFlight[i] = Device.CreateFence(VkFenceCreateFlags.SignaledBit, null);
        }

    }

    /*
    private (PhysicalDevice, int) PickPhysicalDevice()
    {
        PhysicalDevice? fallback = null;
        var fallbackFamily = -1;

        foreach (var physicalDevice in _instance.PhysicalDevices)
        {
            if (!physicalDevice.SupportsExtension(VK.KHR_SWAPCHAIN_EXTENSION_NAME))
                continue;

            var family = physicalDevice.GetFirstQueueFamilyWithFlag(VkQueueFlags.GraphicsBit);
            if (family < 0 || !physicalDevice.GetSurfaceSupportKHR(family, _surface))
                continue;

            // Prefer a discrete GPU, but take anything that can draw to the window
            if (physicalDevice.GetProperties().DeviceType == VkPhysicalDeviceType.DiscreteGpu)
                return (physicalDevice, family);

            if (fallback == null)
            {
                fallback = physicalDevice;
                fallbackFamily = family;
            }
        }

        return fallback != null
            ? (fallback, fallbackFamily)
            : throw new Exception("No Vulkan device can present to this window");
    }
    */

    private VkShaderModule CompileShader(string source, ShaderKind kind, string name)
    {
        var compiler = ShaderC.CreateCompiler();
        try
        {
            using var result = compiler.Compile(source, kind, name);
            var spirv = new ReadOnlySpan<byte>(result.GetBytes(), (int)result.GetLength());
            return ShaderModuleExtensions.FromSpirv(Device, spirv, null);
        }
        finally
        {
            compiler.Dispose();
        }
    }

    private VkPipeline CreatePipeline()
    {
        var vertexShader = CompileShader(VertexShaderSource, ShaderKind.VertexShader, "frame.vert");
        var fragmentShader = CompileShader(FragmentShaderSource, ShaderKind.FragmentShader, "frame.frag");

        try
        {
            fixed (byte* entryPoint = "main"u8)
            {
                var stages = stackalloc VkPipelineShaderStageCreateInfo[2]
                {
                    new()
                    {
                        Stage = VkShaderStageFlags.VertexBit,
                        Module = vertexShader,
                        Name = entryPoint,
                    },
                    new()
                    {
                        Stage = VkShaderStageFlags.FragmentBit,
                        Module = fragmentShader,
                        Name = entryPoint,
                    },
                };

                // Viewport and scissor are dynamic so the pipeline survives window resizes
                var dynamicStates = stackalloc VkDynamicState[2] { VkDynamicState.Viewport, VkDynamicState.Scissor };
                var dynamicState = new VkPipelineDynamicStateCreateInfo
                {
                    DynamicStateCount = 2,
                    DynamicStates = dynamicStates,
                };
                var viewportState = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };
                var vertexInput = new VkPipelineVertexInputStateCreateInfo();

                return Device.CreateGraphicsPipeline(
                    default,
                    new VkGraphicsPipelineCreateInfo
                    {
                        StageCount = 2,
                        Stages = stages,
                        VertexInputState = &vertexInput,
                        InputAssemblyState = Presets.InputAssembly.TriangleList,
                        ViewportState = &viewportState,
                        RasterizationState = Presets.Rasterization.Fill.CullNone,
                        MultisampleState = Presets.Multisample.None,
                        DepthStencilState = Presets.DepthStencil.NoDepthTest,
                        ColorBlendState = Presets.BlendState.BlendNone,
                        DynamicState = &dynamicState,
                        Layout = _pipelineLayout,
                        RenderPass = _state.RenderPass,
                        Subpass = 0,
                    },
                    null
                );
            }
        }
        finally
        {
            Device.DestroyShaderModule(vertexShader, null);
            Device.DestroyShaderModule(fragmentShader, null);
        }
    }

    private static (VkFormat Format, int BytesPerPixel) GetFormatInfo(FramePixelFormat format)
    {
        return format switch
        {
            FramePixelFormat.XRGB1555 => (VkFormat.A1R5G5B5UNormPack16, 2),
            FramePixelFormat.XRGB8888 => (VkFormat.B8G8R8A8UNorm, 4),
            FramePixelFormat.RGB565 => (VkFormat.R5G6B5UNormPack16, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    private void EnsureTexture(int width, int height, VkFormat format)
    {
        if (_texture.VkHandle != 0 && _textureExtent.Width == width && _textureExtent.Height == height && _textureFormat == format)
            return;

        // Frame size changes are rare (e.g. a core switching resolution), so simply stall the GPU
        Device.WaitIdle();
        DestroyTexture();

        _texture = Device.CreateImage(
            new VkImageCreateInfo
            {
                ImageType = VkImageType._2D,
                Format = format,
                Extent = new VkExtent3D { Width = width, Height = height, Depth = 1 },
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = VkSampleCountFlags._1Bit,
                Tiling = VkImageTiling.Optimal,
                Usage = VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.SampledBit,
                SharingMode = VkSharingMode.Exclusive,
                InitialLayout = VkImageLayout.Undefined,
            },
            null
        );
        _textureMemory = Device.AllocateMemory(
            _state.PhysicalDevice,
            _texture,
            VkMemoryPropertyFlags.DeviceLocalBit,
            null
        );
        Device.BindImageMemory(_texture, _textureMemory, default);
        _textureView = SwapChainWrapper.CreateImageView(_state.Device, _texture, format);
        _textureFormat = format;
        _textureExtent = new VkExtent2D(width, height);
        _textureHasContents = false;

        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = _sampler,
            ImageView = _textureView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        Device.UpdateDescriptorSets(
            [
                new VkWriteDescriptorSet
                {
                    DstSet = _descriptorSet,
                    DstBinding = 0,
                    DescriptorCount = 1,
                    DescriptorType = VkDescriptorType.CombinedImageSampler,
                    ImageInfo = &imageInfo,
                },
            ],
            []
        );
    }

    private void DestroyTexture()
    {
        if (_texture.VkHandle == 0)
            return;

        Device.DestroyImageView(_textureView, null);
        Device.DestroyImage(_texture, null);
        Device.FreeMemory(_textureMemory, null);
        _texture = default;
    }

    private ref StagingBuffer EnsureStaging(int size)
    {
        // Only called after this slot's fence has been waited on, so the old buffer is no longer in use
        ref var staging = ref _staging[_frame];
        if (staging.Size >= size)
            return ref staging;

        DestroyStaging(ref staging);

        staging.Buffer = Device.CreateBuffer(
            new VkBufferCreateInfo
            {
                Size = (ByteSize64)size,
                Usage = VkBufferUsageFlags.TransferSrcBit,
                SharingMode = VkSharingMode.Exclusive,
            },
            null
        );
        staging.Memory = Device.AllocateMemory(
            _state.PhysicalDevice,
            staging.Buffer,
            VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit,
            null
        );
        Device.BindBufferMemory(staging.Buffer, staging.Memory, default);
        staging.Mapped = Device.MapMemory(new MapInfo(staging.Memory));
        staging.Size = size;
        return ref staging;
    }

    private void DestroyStaging(ref StagingBuffer staging)
    {
        if (staging.Size == 0)
            return;

        Device.UnmapMemory(staging.Memory);
        Device.DestroyBuffer(staging.Buffer, null);
        Device.FreeMemory(staging.Memory, null);
        staging = default;
    }

    /// <summary>
    /// Re-presents the last uploaded frame, e.g. when a libretro core reports a duplicate frame
    /// </summary>
    public void DrawFrame()
    {
        DrawFrame([], 0, 0, 0, FramePixelFormat.XRGB8888);
    }

    /// <summary>
    /// Uploads a frame and presents it stretched over the window. Mirrors libretro's
    /// <c>retro_video_refresh_t</c>: <paramref name="pitch"/> is the byte length of a row in
    /// <paramref name="pixels"/>, and empty <paramref name="pixels"/> re-presents the previous frame.
    /// </summary>
    public void DrawFrame(ReadOnlySpan<byte> pixels, int width, int height, int pitch, FramePixelFormat format)
    {
        var fence = _inFlight[_frame];
        Device.WaitForFence(fence, nint.MaxValue);

        int imageIndex;
        try
        {
            var acquireResult = Device.AcquireNextImageKHR(
                Swapchain,
                nint.MaxValue,
                _imageAvailable[_frame],
                default,
                out imageIndex
            );
            if (acquireResult != VkResult.Success && acquireResult != VkResult.SuboptimalKHR)
                throw new Exception("Failed to acquire swapchain image: " + acquireResult);
        }
        catch (VkResultError e) when (e.Result == VkResult.ErrorOutOfDateKHR)
        {
            //            RecreateSwapchain();
            return;
        }

        // Copy the new frame into this slot's staging buffer, the GPU copies it into the texture below
        var upload = !pixels.IsEmpty;
        if (upload)
        {
            var (vkFormat, bytesPerPixel) = GetFormatInfo(format);
            if (pitch % bytesPerPixel != 0)
                throw new ArgumentException("Pitch must be a multiple of the pixel size", nameof(pitch));

            var byteCount = (height - 1) * pitch + width * bytesPerPixel;
            if (pixels.Length < byteCount)
                throw new ArgumentException("Frame data is smaller than width, height and pitch require", nameof(pixels));

            EnsureTexture(width, height, vkFormat);
            ref var staging = ref EnsureStaging(byteCount);
            pixels[..byteCount].CopyTo(staging.Mapped.AsSpan<byte>(byteCount));
        }

        // Only reset the fence once we know work will be submitted, otherwise the next wait deadlocks
        Device.ResetFences([fence]);

        var commandBuffer = _commandBuffers[_frame];
        commandBuffer.Reset(VkCommandBufferResetFlags.None);
        RecordCommands(commandBuffer, imageIndex, upload ? pitch / GetFormatInfo(format).BytesPerPixel : 0);

        var renderFinished = _state.RenderFinished[imageIndex];
        _queue.Submit(
            [_imageAvailable[_frame]],
            [VkPipelineStageFlags.ColorAttachmentOutputBit],
            [commandBuffer],
            [renderFinished],
            fence
        );

        var swapchain = Swapchain;
        var presentResult = _queue.PresentKHR(
            new VkPresentInfoKHR
            {
                WaitSemaphoreCount = 1,
                WaitSemaphores = &renderFinished,
                SwapchainCount = 1,
                Swapchains = &swapchain,
                ImageIndices = &imageIndex,
            }
        );

        if (presentResult is VkResult.ErrorOutOfDateKHR or VkResult.SuboptimalKHR || IsResized())
        {
            //            RecreateSwapchain();
        }
        else if (presentResult != VkResult.Success)
            throw new Exception("Failed to present swapchain image: " + presentResult);

        _frame = (_frame + 1) % MaxFramesInFlight;
    }

    private bool IsResized()
    {
        var size = _state.Window.FramebufferSize;
        return size.X != Extent.Width || size.Y != Extent.Height;
    }

    private void RecordUpload(CommandBuffer commandBuffer, int rowLengthPixels)
    {
        var range = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            LevelCount = 1,
            LayerCount = 1,
        };

        // Discard the old contents; waiting on the fragment stage stops us overwriting a frame still being sampled
        commandBuffer.PipelineBarrier(
            VkPipelineStageFlags.FragmentShaderBit,
            VkPipelineStageFlags.TransferBit,
            VkDependencyFlags.None,
            [],
            [],
            [
                new VkImageMemoryBarrier
                {
                    SrcAccessMask = VkAccessFlags.None,
                    DstAccessMask = VkAccessFlags.TransferWriteBit,
                    OldLayout = VkImageLayout.Undefined,
                    NewLayout = VkImageLayout.TransferDstOptimal,
                    SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    Image = _texture,
                    SubresourceRange = range,
                },
            ]
        );

        // BufferRowLength lets the GPU skip any padding at the end of each row, so the frame is copied as-is
        commandBuffer.CopyBufferToImage(
            _staging[_frame].Buffer,
            _texture,
            VkImageLayout.TransferDstOptimal,
            [
                new VkBufferImageCopy
                {
                    BufferOffset = default,
                    BufferRowLength = rowLengthPixels,
                    BufferImageHeight = 0,
                    ImageSubresource = new VkImageSubresourceLayers
                    {
                        AspectMask = VkImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                    ImageExtent = new VkExtent3D
                    {
                        Width = _textureExtent.Width,
                        Height = _textureExtent.Height,
                        Depth = 1,
                    },
                },
            ]
        );

        commandBuffer.PipelineBarrier(
            VkPipelineStageFlags.TransferBit,
            VkPipelineStageFlags.FragmentShaderBit,
            VkDependencyFlags.None,
            [],
            [],
            [
                new VkImageMemoryBarrier
                {
                    SrcAccessMask = VkAccessFlags.TransferWriteBit,
                    DstAccessMask = VkAccessFlags.ShaderReadBit,
                    OldLayout = VkImageLayout.TransferDstOptimal,
                    NewLayout = VkImageLayout.ShaderReadOnlyOptimal,
                    SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    Image = _texture,
                    SubresourceRange = range,
                },
            ]
        );

        _textureHasContents = true;
    }

    private void RecordCommands(CommandBuffer commandBuffer, int imageIndex, int uploadRowLengthPixels)
    {
        commandBuffer.Begin(new VkCommandBufferBeginInfo());

        // Texture uploads must happen outside the render pass
        if (uploadRowLengthPixels > 0)
            RecordUpload(commandBuffer, uploadRowLengthPixels);

        var clearValue = (VkClearValue)new VkClearColorValue { Float32 = new(0.0f, 0.0f, 0.0f, 1.0f) };
        commandBuffer.BeginRenderPass(
            new VkRenderPassBeginInfo
            {
                RenderPass = _state.RenderPass,
                Framebuffer = _state.Framebuffers[imageIndex],
                RenderArea = new VkRect2D { Extent = Extent },
                ClearValueCount = 1,
                ClearValues = &clearValue,
            },
            VkSubpassContents.Inline
        );

        if (_textureHasContents)
        {
            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline);
            commandBuffer.BindDescriptorSets(
                VkPipelineBindPoint.Graphics,
                _pipelineLayout,
                0,
                [_descriptorSet],
                []
            );
            commandBuffer.SetViewport(
                0,
                [
                    new VkViewport
                    {
                        Width = Extent.Width,
                        Height = Extent.Height,
                        MinDepth = 0,
                        MaxDepth = 1,
                    },
                ]
            );
            commandBuffer.SetScissor(0, [new VkRect2D { Extent = Extent }]);
            commandBuffer.Draw(3, 1, 0, 0);
        }

        commandBuffer.EndRenderPass();
        commandBuffer.End();
    }

    public void Dispose()
    {
        Device.WaitIdle();
        DestroyTexture();

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            DestroyStaging(ref _staging[i]);
            Device.DestroySemaphore(_imageAvailable[i], null);
            Device.DestroyFence(_inFlight[i], null);
        }

        Device.DestroyCommandPool(_commandPool, null);
        Device.DestroyPipeline(_pipeline, null);
        Device.DestroyPipelineLayout(_pipelineLayout, null);
        Device.DestroyDescriptorPool(_descriptorPool, null);
        Device.DestroyDescriptorSetLayout(_descriptorSetLayout, null);
        Device.DestroySampler(_sampler, null);
    }
}
