
using SK.Libretro;

namespace ScorpionBox.Processors
{

    internal class ScorpionGraphicsProcessor : IGraphicsProcessor
    {
        private ScorpionBoxGame _box;

        public ScorpionGraphicsProcessor(ScorpionBoxGame box)
        {
            _box = box;
            _box.Retro.ActivateGraphics(this);
        }

        public unsafe void ProcessFrame0RGB1555(ushort* data, int width, int height, int pitch)
            => FinishFrame(data, width, height, pitch);

        public unsafe void ProcessFrameARGB8888(uint* data, int width, int height, int pitch)
            => FinishFrame(data, width, height, pitch);

        public unsafe void ProcessFrameRGB565(ushort* data, int width, int height, int pitch)
            => FinishFrame(data, width, height, pitch);

        private unsafe void FinishFrame(void* data, int width, int height, int pitch)
        {
            var format = _box.Retro.Game.PixelFormat;
            var bytesPerPixel = format.BytesPerPixel();
            var pitchInBytes = pitch * bytesPerPixel;
            _box.Window.State.Renderer.DrawFrame(data, (uint)width, (uint)height, (uint)pitchInBytes, format);
        }

    }

}