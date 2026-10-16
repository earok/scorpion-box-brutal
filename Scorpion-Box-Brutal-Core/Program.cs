using System.Runtime.InteropServices;
using Brutal.GlfwApi;
using Brutal.VulkanApi;
using Scorpion_Box_Brutal_Core;

using var window = new WindowWrapper();

// Stand-in for a libretro core: a 320x240 XRGB8888 frame with padded rows, as cores often supply
const int frameWidth = 320;
const int frameHeight = 240;
const int framePitch = 384 * 4;
var pixels = new uint[framePitch / 4 * frameHeight];

var tick = 0;
while (window.IsRunning)
{
    DrawTestPattern(pixels, frameWidth, frameHeight, framePitch / 4, tick++);
    window.State.Renderer.DrawFrame(MemoryMarshal.AsBytes(pixels.AsSpan()), frameWidth, frameHeight, framePitch, FramePixelFormat.XRGB8888);
}

// Scrolling XOR pattern with a white border, so stretching and edge alignment are easy to check
static void DrawTestPattern(Span<uint> pixels, int width, int height, int stride, int tick)
{
    for (var y = 0; y < height; y++)
    {
        var row = pixels.Slice(y * stride, width);
        for (var x = 0; x < width; x++)
        {
            if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
            {
                row[x] = 0xFFFFFF;
                continue;
            }

            var v = (uint)(((x + tick) ^ y) & 0xFF);
            row[x] = (v << 16) | ((uint)(y * 255 / height) << 8) | (255 - v);
        }
    }
}
