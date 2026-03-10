using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlaybackHandler : MonoBehaviour
    {
        private AudioSource _audioSource;

        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;

        public event Action OnPlaybackComplete;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        public async Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioPlaybackHandler] Null AudioClip, skipping playback.");
                return;
            }

            _audioSource.clip = clip;
            _audioSource.Play();
            Debug.Log($"[AudioPlaybackHandler] Playing {clip.length:F2}s audio.");

            while (_audioSource.isPlaying)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _audioSource.Stop();
                    return;
                }
                await Task.Yield();
            }

            OnPlaybackComplete?.Invoke();
        }

        public void StopPlayback()
        {
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
    }
}
