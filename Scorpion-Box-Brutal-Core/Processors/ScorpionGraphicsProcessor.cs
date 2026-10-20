
using SK.Libretro;

namespace ScorpionBox.Processors
{

    internal class ScorpionGraphicsProcessor : IGraphicsProcessor
    {
        private Wrapper _retro;
        private FrameRenderer _renderer;

        public ScorpionGraphicsProcessor(Wrapper retro, FrameRenderer renderer)
        {
            _retro = retro;
            _renderer = renderer;
            retro.ActivateGraphics(this);
        }

        public unsafe void ProcessFrame0RGB1555(ushort* data, int width, int height, int pitch)
            => FinishFrame(data, width, height, pitch);

        public unsafe void ProcessFrameARGB8888(uint* data, int width, int height, int pitch)
            => FinishFrame(data, width, height, pitch);

        public unsafe void ProcessFrameRGB565(ushort* data, int width, int height, int pitch)
            => FinishFrame(data, width, height, pitch);

        private unsafe void FinishFrame(void* data, int width, int height, int pitch)
        {
            var bytesPerPixel = _retro.Game.PixelFormat.BytesPerPixel();
            var pitchInBytes = pitch * bytesPerPixel;
            _renderer.DrawFrame(data, (uint)width, (uint)height, (uint)pitchInBytes, _retro.Game.PixelFormat);
        }

    }

}