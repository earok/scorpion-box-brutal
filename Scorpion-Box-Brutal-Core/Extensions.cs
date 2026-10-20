using static SK.Libretro.Wrapper;

namespace ScorpionBox
{

    public static class Extensions
    {
        public static int BytesPerPixel(this retro_pixel_format pixelFormat)
        {
            return pixelFormat switch
            {
                retro_pixel_format.RETRO_PIXEL_FORMAT_RGB565 => 2,
                retro_pixel_format.RETRO_PIXEL_FORMAT_0RGB1555 => 2,
                retro_pixel_format.RETRO_PIXEL_FORMAT_XRGB8888 => 4,
                _ => 2,
            };
        }
    }
}