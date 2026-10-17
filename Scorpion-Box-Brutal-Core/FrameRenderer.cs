using Brutal.Numerics;
using Brutal.OpenGlApi;
using static SK.Libretro.Wrapper;

namespace Scorpion_Box_Brutal_Core;

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
    private readonly GlProgram _program;

    // Core profile refuses to draw without a vertex array bound, even an empty one
    private readonly GlVertexArray _vertexArray;

    // The texture the frame is sampled from, reallocated whenever the frame size or format changes
    private readonly GlTexture _texture;
    private retro_pixel_format _textureFormat;
    private int _textureWidth;
    private int _textureHeight;
    private bool _textureHasContents;

    public FrameRenderer(WindowState state, bool linearFiltering = false)
    {
        _state = state;

        var vertexShader = OpenGl.CreateShader(GlShaderType.Vertex).SetSource(VertexShaderSource).Compile();
        var fragmentShader = OpenGl.CreateShader(GlShaderType.Fragment).SetSource(FragmentShaderSource).Compile();
        try
        {
            _program = OpenGl.CreateProgram().Attach(vertexShader).Attach(fragmentShader).Link();
        }
        finally
        {
            vertexShader.Dispose();
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

    private static (uint InternalFormat, uint Format, uint Type, int BytesPerPixel) GetFormatInfo(retro_pixel_format format)
    {
        return format switch
        {
            retro_pixel_format.RETRO_PIXEL_FORMAT_0RGB1555 => (OpenGl.GL_RGB5_A1, OpenGl.GL_BGRA, OpenGl.GL_UNSIGNED_SHORT_1_5_5_5_REV, 2),
            retro_pixel_format.RETRO_PIXEL_FORMAT_XRGB8888 => (OpenGl.GL_RGBA8, OpenGl.GL_BGRA, OpenGl.GL_UNSIGNED_INT_8_8_8_8_REV, 4),
            retro_pixel_format.RETRO_PIXEL_FORMAT_RGB565 => (OpenGl.GL_RGB8, OpenGl.GL_RGB, OpenGl.GL_UNSIGNED_SHORT_5_6_5, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    /// <summary>
    /// Re-presents the last uploaded frame, e.g. when a libretro core reports a duplicate frame
    /// </summary>
    public void DrawFrame()
    {
        DrawFrame([], 0, 0, 0, retro_pixel_format.RETRO_PIXEL_FORMAT_XRGB8888);
    }

    /// <summary>
    /// Uploads a frame and presents it stretched over the window. Mirrors libretro's
    /// <c>retro_video_refresh_t</c>: <paramref name="pitch"/> is the byte length of a row in
    /// <paramref name="pixels"/>, and empty <paramref name="pixels"/> re-presents the previous frame.
    /// </summary>
    public void DrawFrame(ReadOnlySpan<byte> pixels, int width, int height, int pitch, retro_pixel_format format)
    {
        OpenGl.BindTexture(OpenGl.GL_TEXTURE_2D, _texture.Id);

        if (!pixels.IsEmpty)
        {
            Upload(pixels, width, height, pitch, format);
        }

        // Read every frame, so resizes need no special handling
        var size = _state.Window.FramebufferSize;
        OpenGl.Viewport(0, 0, size.X, size.Y);
        OpenGl.ClearColor(new float4(0.0f, 0.0f, 0.0f, 1.0f));
        OpenGl.Clear(GlClearBit.Color);

        if (_textureHasContents)
        {
            _program.Use();
            _vertexArray.Bind();
            OpenGl.Native.glDrawArrays(OpenGl.GL_TRIANGLES, 0, 3);
        }

        _state.Window.SwapBuffers();
    }

    private void Upload(ReadOnlySpan<byte> pixels, int width, int height, int pitch, retro_pixel_format format)
    {
        var (internalFormat, glFormat, glType, bytesPerPixel) = GetFormatInfo(format);
        if (pitch % bytesPerPixel != 0)
            throw new ArgumentException("Pitch must be a multiple of the pixel size", nameof(pitch));

        var byteCount = (height - 1) * pitch + width * bytesPerPixel;
        if (pixels.Length < byteCount)
            throw new ArgumentException("Frame data is smaller than width, height and pitch require", nameof(pixels));

        // Frame size changes are rare (e.g. a core switching resolution), so just reallocate the texture
        if (width != _textureWidth || height != _textureHeight || format != _textureFormat || !_textureHasContents)
        {
            OpenGl.TexImage2D(OpenGl.GL_TEXTURE_2D, 0, (int)internalFormat, width, height, 0, glFormat, glType, null);
            _textureWidth = width;
            _textureHeight = height;
            _textureFormat = format;
        }

        // Row length lets GL skip any padding at the end of each row, so the frame is uploaded as-is
        OpenGl.PixelStorei(OpenGl.GL_UNPACK_ROW_LENGTH, pitch / bytesPerPixel);
        fixed (byte* data = pixels)
        {
            OpenGl.TexSubImage2D(OpenGl.GL_TEXTURE_2D, 0, 0, 0, width, height, glFormat, glType, data);
        }
        OpenGl.PixelStorei(OpenGl.GL_UNPACK_ROW_LENGTH, 0);

        _textureHasContents = true;
    }

    public void Dispose()
    {
        var texture = _texture;
        OpenGl.DeleteTextures(new ReadOnlySpan<GlTexture>(&texture, 1));
        var vertexArray = _vertexArray;
        OpenGl.DeleteVertexArrays(new ReadOnlySpan<GlVertexArray>(&vertexArray, 1));
        _program.Dispose();
    }
}
