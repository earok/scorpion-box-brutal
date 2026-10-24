using Brutal.Numerics;
using Brutal.OpenGlApi;
using CommunityToolkit.HighPerformance;
using ScorpionBox.Windows;
using static SK.Libretro.Wrapper;

namespace ScorpionBox
{
    public sealed unsafe class FrameRenderer : IDisposable
    {
        private const string VertexShaderSource = """
            #version 330 core
            layout (location = 0) in vec2 aPos;
            layout (location = 1) in vec2 aUv;
            out vec2 uv;
            void main()
            {
                uv = aUv;
                gl_Position = vec4(aPos, 0.0, 1.0);
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


        private readonly GlProgram _program;

        // Core profile refuses to draw without a vertex array bound, even an empty one
        private readonly GlVertexArray _vertexArray;
        private readonly GlBuffer _vertexBuffer;

        // The texture the frame is sampled from, reallocated whenever the frame size or format changes

        private readonly GlTexture _texture;
        private retro_pixel_format _textureFormat;
        private uint _textureWidth;
        private uint _textureHeight;
        private ScorpionBoxGame _box;

        public FrameRenderer(ScorpionBoxGame box)
        {
            _box = box;
            var vertexShader = OpenGl.CreateShader(GlShaderType.Vertex).SetSource(VertexShaderSource).Compile();
            var fragmentShader = OpenGl.CreateShader(GlShaderType.Fragment).SetSource(FragmentShaderSource).Compile();
            _program = OpenGl.CreateProgram().Attach(vertexShader).Attach(fragmentShader).Link();
            _vertexArray = OpenGl.GenVertexArray();
            _vertexArray.Bind();

            vertexShader.Dispose();
            fragmentShader.Dispose();

            _vertexBuffer = OpenGl.GenBuffer();
            _vertexBuffer.Bind(GlBindTarget.ArrayBuffer);

            OpenGl.VertexAttribute(new GlAttribute(0, 2, OpenGl.GL_FLOAT, GlScaling.Float, sizeof(Vertex), 0));
            OpenGl.VertexAttribute(
                new GlAttribute(1, 2, OpenGl.GL_FLOAT, GlScaling.Float, sizeof(Vertex), sizeof(float2))
            );
            OpenGl.EnableVertexAttribArray(0);
            OpenGl.EnableVertexAttribArray(1);

            _texture = _box.TextureService.GenerateTexture();

            var filter = (int)OpenGl.GL_LINEAR;
            OpenGl.BindTexture(OpenGl.GL_TEXTURE_2D, _texture.Id);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MIN_FILTER, filter);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MAG_FILTER, filter);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_WRAP_S, (int)OpenGl.GL_CLAMP_TO_EDGE);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_WRAP_T, (int)OpenGl.GL_CLAMP_TO_EDGE);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MAX_LEVEL, 0);

            OpenGl.PixelStorei(OpenGl.GL_UNPACK_ALIGNMENT, 1);
            OpenGl.ClearColor(float4.Zero);
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
            // Read every frame, so resizes need no special handling
            var size = _box.Window.State.GlfwWindow.FramebufferSize;

            /*
            var textureAspect = width / (float)height;
            var screenAspect = size.X / (float)size.Y;
            var xAspect = textureAspect / screenAspect;
            var yAspect = 1f;

            if (xAspect > 1)
            {
                yAspect = 1 / xAspect;
                xAspect = 1;
            }
            */

            var intScaleX = size.X / width;
            var intScaleY = size.Y / height;
            var minScale = Math.Min(intScaleX, intScaleY);
            var xAspect = (width * minScale) / (float)size.X;
            var yAspect = (height * minScale) / (float)size.Y;

            OpenGl.BufferData(
                GlBindTarget.ArrayBuffer,
                new Vertex[]
                {
                    new(new float2(-xAspect, -yAspect), new float2(0.0f, 1.0f)),
                    new(new float2( xAspect, -yAspect), new float2(1.0f, 1.0f)),
                    new(new float2(-xAspect,  yAspect), new float2(0.0f, 0.0f)),
                    new(new float2( xAspect,  yAspect), new float2(1.0f, 0.0f)),
                },
                GlBufferUsage.StaticDraw
            );

            OpenGl.BindTexture(OpenGl.GL_TEXTURE_2D, _texture.Id);
            Upload(pixels, width, height, pitch, format);
            OpenGl.Viewport(0, 0, size.X, size.Y);

            OpenGl.Clear(GlClearBit.Color);

            _program.Use();
            _vertexArray.Bind();
            OpenGl.Native.glDrawArrays(OpenGl.GL_TRIANGLE_STRIP, 0, 4);
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
            OpenGl.DeleteVertexArrays([_vertexArray]);
            OpenGl.DeleteBuffers([_vertexBuffer]);
            OpenGl.DeleteTextures(new ReadOnlySpan<GlTexture>(in _texture));
            _program.Dispose();
        }
    }

    record struct Vertex(float2 Position, float2 UV);

}