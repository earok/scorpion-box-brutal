
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SK.Libretro;

namespace ScorpionBox.Core.Processors;

internal class ScorpionGraphicsProcessor : IGraphicsProcessor
{
    private ScorpionBoxGame _box;

    public ScorpionGraphicsProcessor(ScorpionBoxGame scorpionBoxGame)
    {
        _box = scorpionBoxGame;
    }

    public unsafe void ProcessFrame0RGB1555(ushort* data, int width, int height, int pitchInPixels)
        => OnShortFrame(data, width, height, pitchInPixels);

    public unsafe void ProcessFrameARGB8888(uint* data, int width, int height, int pitchInPixels)
        => OnIntFrame(data, width, height, pitchInPixels);

    public unsafe void ProcessFrameRGB565(ushort* data, int width, int height, int pitchInPixels)
        => OnShortFrame(data, width, height, pitchInPixels);

    public unsafe void OnShortFrame(ushort* data, int width, int height, int pitch)
    {
        try
        {
            pitch *= 2;
            int totalWidth = (int)width * 2;

            byte[] raw = new byte[(uint)pitch * height];
            Marshal.Copy((IntPtr)data, raw, 0, (int)pitch * (int)height);

            byte[] result = new byte[totalWidth * height];
            var destinationIndex = 0;
            for (var sourceIndex = 0; sourceIndex < (uint)pitch * height; sourceIndex += (int)pitch)
            {
                Array.Copy(raw, sourceIndex, result, destinationIndex, totalWidth);
                destinationIndex += totalWidth;
            }
            FinishFrame(width, height, result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Unable to process frame: " + ex.Message);
        }
    }

    public unsafe void OnIntFrame(uint* data, int width, int height, int pitch)
    {
        try
        {
            pitch *= 4;
            int totalWidth = (int)width * 4;

            byte[] raw = new byte[(uint)pitch * height];
            Marshal.Copy((IntPtr)data, raw, 0, (int)pitch * (int)height);

            byte[] result = new byte[totalWidth * height];
            var destinationIndex = 0;
            for (var sourceIndex = 0; sourceIndex < (uint)pitch * height; sourceIndex += (int)pitch)
            {
                Array.Copy(raw, sourceIndex, result, destinationIndex, totalWidth);
                destinationIndex += totalWidth;
            }

            //Swap Red and Green for OpenGL
            //if (_box.PixelFormat == SurfaceFormat.Color)
            //{
            /*
                var i = result.Length;
                while (i > 0)
                {
                    i -= 4;
                    var tmp = result[i];
                    result[i] = result[i + 2];
                    result[i + 2] = tmp;
                }
            //}
            */

            FinishFrame(width, height, result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Unable to process frame:" + ex.ToString());
        }
    }

    private unsafe void FinishFrame(int width, int height, byte[] result)
    {
        _box.Pixels = result;
    }
}

