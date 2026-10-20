
using SK.Libretro;
using Brutal.OpenAlApi;
using System.Runtime.InteropServices;

namespace ScorpionBox.Processors
{
    public class ScorpionAudioProcessor : IAudioProcessor
    {
        const int Buffers = 4;

        private Device _device;
        private Context _context;
        private AlBuffer[] _buffers;
        private AlBuffer[] _recoveredBuffers;
        private AlSource _source;
        private int _sampleRate;
        private int _minSamples;
        Queue<AlBuffer> _freeBuffers = new();
        private AlBuffer _backBuffer;

        public ScorpionAudioProcessor(Wrapper retro)
        {
            retro.ActivateAudio(this);
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
            _device = Alc.OpenDevice();
            _context = _device.CreateContext();
            _context.MakeCurrent();
            _buffers = Al.GenBuffers(Buffers);
            _recoveredBuffers = new AlBuffer[Buffers];
            _source = Al.GenSource();
            _sampleRate = sampleRate;

            _minSamples = sampleRate / 100; //Collect 1/100th of a second worth of samples before playing
            foreach (var buffer in _buffers)
            {
                _freeBuffers.Enqueue(buffer);
            }
            _backBuffer = _freeBuffers.Dequeue();
        }

        public void ProcessSamples(float[] samples)
        {
            _backBuffer.SetData(AlFormat.StereoFloat32, MemoryMarshal.AsBytes(samples), _sampleRate);

            //Haven't collected enough samples yet
            if (_backBuffer.SampleLength < _minSamples)
            {
                return;
            }

            //Get our old buffers back
            if (_source.BuffersProcessed > 0)
            {
                var freedBuffers = new Span<AlBuffer>(_recoveredBuffers, 0, _source.BuffersProcessed);
                _source.UnqueueBuffers(freedBuffers);
                foreach (var freedBuffer in freedBuffers)
                {
                    _freeBuffers.Enqueue(freedBuffer);
                }
            }

            //We don't have any free buffers yet, so keep collecting on the back buffer
            if (_freeBuffers.TryDequeue(out var nextBuffer) == false)
            {
                return;
            }

            _source.QueueBuffers(new ReadOnlySpan<AlBuffer>(in _backBuffer));
            _backBuffer = nextBuffer;

            if (_source.State != AlSourceState.Playing)
            {
                _source.Play();
            }
        }
    }
}
