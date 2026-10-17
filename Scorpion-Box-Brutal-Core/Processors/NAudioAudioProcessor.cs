/* MIT License

 * Copyright (c) 2020 Skurdt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 

using System;
using System.Diagnostics;
using NAudio.Wave;
using SK.Libretro;

namespace ScorpionBox.Core.Processors
{
    public class NAudioAudioProcessor : IAudioProcessor
    {
        private const int AUDIO_BUFFER_SIZE = 65536;

        private IWavePlayer _audioDevice;
        private BufferedWaveProvider _bufferedWaveProvider;
        private float _volume;

        public NAudioAudioProcessor(float volume)
        {
            _volume = volume;
        }

        public void Init(int sampleRate)
        {
            try
            {
                DeInit();

                _audioDevice = new WaveOutEvent
                {
                    DesiredLatency = 140
                };

                var audioFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate > 0 ? sampleRate : 44100, 2);
                _bufferedWaveProvider = new BufferedWaveProvider(audioFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferLength = AUDIO_BUFFER_SIZE
                };

                _audioDevice.Init(_bufferedWaveProvider);
                Volume = _volume;
                _audioDevice.Play();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public float Volume
        {
            get => _audioDevice.Volume;
            set => _audioDevice.Volume = value;
        }

        public void DeInit()
        {
            _audioDevice?.Stop();
            _audioDevice?.Dispose();
        }

        public void ProcessSamples(float[] samples)
        {
            if (_bufferedWaveProvider != null)
            {
                var byteBuffer = new byte[samples.Length * sizeof(float)];
                Buffer.BlockCopy(samples, 0, byteBuffer, 0, byteBuffer.Length);
                _bufferedWaveProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);
            }
        }
    }
}
*/