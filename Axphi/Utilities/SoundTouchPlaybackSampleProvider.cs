using NAudio.Wave;
using SoundTouch;
using System;

namespace Axphi.Utilities
{
    /// <summary>
    /// Real-time speed control based on SoundTouch.
    /// PreservePitch=true: tempo changes while pitch stays stable.
    /// PreservePitch=false: rate changes and pitch follows speed.
    /// </summary>
    public sealed class SoundTouchPlaybackSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly SoundTouchProcessor _processor;
        private readonly int _channels;
        private readonly object _syncRoot = new();

        private float[] _inputBuffer;
        private bool _sourceEnded;
        private bool _flushed;
        private float _speed = 1.0f;
        private bool _preservePitch = true;

        public SoundTouchPlaybackSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = Math.Max(1, source.WaveFormat.Channels);
            _inputBuffer = new float[_channels * 4096];

            _processor = new SoundTouchProcessor
            {
                SampleRate = source.WaveFormat.SampleRate,
                Channels = _channels
            };

            ApplySettings();
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public float Speed
        {
            get
            {
                lock (_syncRoot)
                {
                    return _speed;
                }
            }
            set
            {
                lock (_syncRoot)
                {
                    _speed = Math.Clamp(value, 0.1f, 4.0f);
                    ApplySettings();
                }
            }
        }

        public bool PreservePitch
        {
            get
            {
                lock (_syncRoot)
                {
                    return _preservePitch;
                }
            }
            set
            {
                lock (_syncRoot)
                {
                    _preservePitch = value;
                    ApplySettings();
                }
            }
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                _processor.Clear();
                _sourceEnded = false;
                _flushed = false;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            lock (_syncRoot)
            {
                if (count % _channels != 0)
                {
                    count -= count % _channels;
                }

                int targetFrames = count / _channels;
                int writtenFrames = 0;

                while (writtenFrames < targetFrames)
                {
                    int remainingFrames = targetFrames - writtenFrames;
                    int writeOffset = offset + writtenFrames * _channels;
                    Span<float> outputSlice = buffer.AsSpan(writeOffset, remainingFrames * _channels);
                    int receivedFrames = _processor.ReceiveSamples(outputSlice, remainingFrames);
                    if (receivedFrames > 0)
                    {
                        writtenFrames += receivedFrames;
                        continue;
                    }

                    if (_sourceEnded)
                    {
                        if (!_flushed)
                        {
                            _processor.Flush();
                            _flushed = true;
                            continue;
                        }

                        break;
                    }

                    int readSamples = _source.Read(_inputBuffer, 0, _inputBuffer.Length);
                    if (readSamples <= 0)
                    {
                        _sourceEnded = true;
                        continue;
                    }

                    int readFrames = readSamples / _channels;
                    if (readFrames <= 0)
                    {
                        _sourceEnded = true;
                        continue;
                    }

                    _processor.PutSamples(_inputBuffer.AsSpan(0, readFrames * _channels), readFrames);
                }

                return writtenFrames * _channels;
            }
        }

        private void ApplySettings()
        {
            if (_preservePitch)
            {
                _processor.Pitch = 1.0;
                _processor.Rate = 1.0;
                _processor.Tempo = _speed;
                return;
            }

            _processor.Pitch = 1.0;
            _processor.Tempo = 1.0;
            _processor.Rate = _speed;
        }
    }
}
