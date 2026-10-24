using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.OpenGlApi;
using Brutal.TextureApi;

namespace ScorpionBox.Services
{

    public class TextureService : IDisposable
    {
        private readonly List<GlTexture> _textures = [];

        public void Dispose()
        {        
            OpenGl.DeleteTextures(new ReadOnlySpan<GlTexture>(_textures.ToArray()));
        }

        public unsafe (ImTextureRef, float2) FromFile(string path)
        {
            var newTexData = TextureLoader.LoadFromFile(path);
            var logoTexture = GenerateTexture();

            fixed (byte* data = newTexData.Data)
            {
                OpenGl.TexImage2D(OpenGl.GL_TEXTURE_2D, 0, (int)OpenGl.GL_RGBA8, newTexData.Width, newTexData.Height, 0, OpenGl.GL_RGBA, OpenGl.GL_UNSIGNED_BYTE, data);
            }
            TextureLoader.Unload(newTexData);

            return (new ImTextureRef { _TexID = (ImTextureID)(nint)logoTexture.Id }, new float2(newTexData.Width, newTexData.Height));
        }

        public unsafe GlTexture GenerateTexture()
        {
            GlTexture logoTexture;
            OpenGl.GenTextures(new ReadOnlySpan<GlTexture>(&logoTexture, 1));
            OpenGl.BindTexture(OpenGl.GL_TEXTURE_2D, logoTexture.Id);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MIN_FILTER, (int)OpenGl.GL_LINEAR);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MAG_FILTER, (int)OpenGl.GL_LINEAR);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_WRAP_S, (int)OpenGl.GL_CLAMP_TO_EDGE);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_WRAP_T, (int)OpenGl.GL_CLAMP_TO_EDGE);
            OpenGl.Native.glTexParameteri(OpenGl.GL_TEXTURE_2D, OpenGl.GL_TEXTURE_MAX_LEVEL, 0);
            _textures.Add(logoTexture);
            return logoTexture;
        }
    }

}