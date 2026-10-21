using Brutal.Numerics;
using Brutal.OpenGlApi;
using ScorpionBox.Windows;
using static SK.Libretro.Wrapper;

namespace ScorpionBox
{

    /// <summary>
    /// OpenGL renderer that uploads a CPU-side framebuffer each frame and stretches it over the whole window
    /// </summary>
    public sealed unsafe class FrameRenderer : IDisposable
    {
        // Fullscreen triangle generated from the vertex index, no vertex buffer needed.
        // GL's clip space is bottom-up while the frame's first row is the top, so V is flipped.
        private const string VertexShaderSource = """
        #version 330 core
        out vec2 uv;

        void main() {
            vec2 pos = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            uv = vec2(pos.x, 1.0 - pos.y);
            gl_Position = vec4(pos * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

        // Alpha is forced to 1 since the X channel of the source formats is undefined
        private const string FragmentShaderSource = """
        #version 330 core
        uniform sampler2D frame;
        in vec2 uv;
        out vec4 outColor;

        void main() {
            outColor = vec4(texture(frame, uv).rgb, 1.0);
        }
        """;

        private readonly WindowState _state;
        private readonly GlShader _vertexShader;

        private readonly GlProgram _program;

        // Core profile refuses to draw without a vertex array bound, even an empty one
        private readonly GlVertexArray _vertexArray;

        // The texture the frame is sampled from, reallocated whenever the frame size or format changes
        private readonly GlTexture _texture;
        private retro_pixel_format _textureFormat;
        private uint _textureWidth;
        private uint _textureHeight;

        public FrameRenderer(WindowState state, bool linearFiltering = false)
        {
            _state = state;
            _vertexShader = OpenGl.CreateShader(GlShaderType.Vertex).SetSource(VertexShaderSource).Compile();
            var fragmentShader = OpenGl.CreateShader(GlShaderType.Fragment).SetSource(FragmentShaderSource).Compile();
            try
            {
                _program = OpenGl.CreateProgram().Attach(_vertexShader).Attach(fragmentShader).Link();
            }
            finally
            {
                _vertexShader.Dispose();
                fragmentShader.Dispose();
            }

            _vertexArray = OpenGl.GenVertexArray();

            GlTexture texture;
            OpenGl.GenTextures(new ReadOnlySpan<GlTexture>(&texture, 1));
            _texture = texture;

            var filter = (int)(linearFiltering ? OpenGl.GL_LINEAR : OpenGl.GL_NEAREST);
            OpenGl.BindTexture(OpenGl.GL_TEXTURE_2D, _texture.Id);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MIN_FILTER, filter);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MAG_FILTER, filter);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_WRAP_S, (int)OpenGl.GL_CLAMP_TO_EDGE);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_WRAP_T, (int)OpenGl.GL_CLAMP_TO_EDGE);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MAX_LEVEL, 0);

            // Rows are addressed by GL_UNPACK_ROW_LENGTH, so don't let GL round row strides up
            OpenGl.PixelStorei(OpenGl.GL_UNPACK_ALIGNMENT, 1);
        }

        private static (uint InternalFormat, uint Format, uint Type) GetFormatInfo(retro_pixel_format format)
        {
            return format switch
            {
                retro_pixel_format.RETRO_PIXEL_FORMAT_0RGB1555 => (OpenGl.GL_RGB5_A1, OpenGl.GL_BGRA, OpenGl.GL_UNSIGNED_SHORT_1_5_5_5_REV),
                retro_pixel_format.RETRO_PIXEL_FORMAT_XRGB8888 => (OpenGl.GL_RGBA8, OpenGl.GL_BGRA, OpenGl.GL_UNSIGNED_INT_8_8_8_8_REV),
                retro_pixel_format.RETRO_PIXEL_FORMAT_RGB565 => (OpenGl.GL_RGB8, OpenGl.GL_RGB, OpenGl.GL_UNSIGNED_SHORT_5_6_5),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };
        }

        public void DrawFrame(void* pixels, uint width, uint height, uint pitch, retro_pixel_format format)
        {
            OpenGl.BindTexture(OpenGl.GL_TEXTURE_2D, _texture.Id);
            Upload(pixels, width, height, pitch, format);

            // Read every frame, so resizes need no special handling
            var size = _state.GlfwWindow.FramebufferSize;
            OpenGl.Viewport(0, 0, size.X, size.Y);

            OpenGl.ClearColor(float4.Zero);
            OpenGl.Clear(GlClearBit.Color);

            _program.Use();
            _vertexArray.Bind();
            OpenGl.Native.glDrawArrays(OpenGl.GL_TRIANGLES, 0, 3);
            _state.GlfwWindow.SwapBuffers();
        }

        private void Upload(void* pixels, uint width, uint height, uint pitch, retro_pixel_format format)
        {
            var (internalFormat, glFormat, glType) = GetFormatInfo(format);
            if (width != _textureWidth || height != _textureHeight || format != _textureFormat)
            {
                OpenGl.TexImage2D(OpenGl.GL_TEXTURE_2D, 0, (int)internalFormat, (int)width, (int)height, 0, glFormat, glType, null);
                _textureWidth = width;
                _textureHeight = height;
                _textureFormat = format;
            }

            OpenGl.PixelStorei(OpenGl.GL_UNPACK_ROW_LENGTH, (int)(pitch / format.BytesPerPixel()));
            OpenGl.TexSubImage2D(OpenGl.GL_TEXTURE_2D, 0, 0, 0, (int)width, (int)height, glFormat, glType, pixels);
            OpenGl.PixelStorei(OpenGl.GL_UNPACK_ROW_LENGTH, 0);
        }

        public void Dispose()
        {
            OpenGl.DeleteTextures(new ReadOnlySpan<GlTexture>(in _texture));
            OpenGl.DeleteVertexArrays(new ReadOnlySpan<GlVertexArray>(in _vertexArray));
            _program.Dispose();
        }
    }

}