
using SK.Libretro;

namespace ScorpionBox.Processors
{

    internal class ScorpionGraphicsProcessor : IGraphicsProcessor
    {
        private ScorpionBoxGame _box;
        public unsafe void* Pixels;
        public int Width;
        public int Height;
        public int Pitch;

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
            Width = width;
            Height = height;
            Pitch = pitch;
            Pixels = data;
        }

    }

}