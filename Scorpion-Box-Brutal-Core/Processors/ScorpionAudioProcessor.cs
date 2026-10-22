
using SK.Libretro;
using Brutal.OpenAlApi;
using System.Runtime.InteropServices;

namespace ScorpionBox.Processors
{
    public class ScorpionAudioProcessor : IAudioProcessor
    {
        private Device _device;
        private Context _context;
        private List<AlBuffer> _buffers = [];
        private Queue<AlBuffer> _freeBuffers = [];
        private AlSource _source;
        private int _sampleRate;
        private AlBuffer _currentBuffer;

        public ScorpionAudioProcessor(ScorpionBoxGame box)
        {
            _device = Alc.OpenDevice();
            _context = _device.CreateContext();
            box.Retro.ActivateAudio(this);
        }

        public void DeInit()
        {
            _source.Stop();
            _source.Delete();
            foreach (var buffer in _buffers)
            {
                buffer.Delete();
            }
            _context.Dispose();
            _device.Dispose();
        }

        public void Init(int sampleRate)
        {
            _context.MakeCurrent();
            _source = Al.GenSource();
            _sampleRate = sampleRate;
        }

        public void ProcessSamples(float[] samples)
        {
            //Reclaim all buffers
            var freeBufferSpan = new Span<AlBuffer>(ref _currentBuffer);
            while (_source.BuffersProcessed > 0)
            {
                _source.UnqueueBuffers(freeBufferSpan);
                _freeBuffers.Enqueue(_currentBuffer);
            }

            if (_freeBuffers.Count > 0)
            {
                _currentBuffer = _freeBuffers.Dequeue();
            }
            else
            {
                _currentBuffer = Al.GenBuffer();
                _buffers.Add(_currentBuffer);
            }


            _currentBuffer.SetData(AlFormat.StereoFloat32, MemoryMarshal.AsBytes(samples), _sampleRate);
            _source.QueueBuffers(new ReadOnlySpan<AlBuffer>(in _currentBuffer));
            if (_source.State != AlSourceState.Playing)
            {
                _source.Play();
            }            
        }
    }
}
