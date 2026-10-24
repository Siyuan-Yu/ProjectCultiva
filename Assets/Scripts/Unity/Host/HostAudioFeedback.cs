using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>Small local feedback cues; presentation only, with no effect on simulation timing.</summary>
    public static class HostAudioFeedback
    {
        public enum Cue { Confirm, Hit, Harvest }
        static readonly AudioClip[] Clips = new AudioClip[3];
        static readonly float[] LastPlayed = { -10f, -10f, -10f };

        public static void Play(GameObject host, Cue cue)
        {
            if (host == null || !Application.isPlaying) return;
            var index = (int)cue;
            if (Time.unscaledTime - LastPlayed[index] < (cue == Cue.Hit ? .08f : .18f)) return;
            LastPlayed[index] = Time.unscaledTime;
            var source = host.GetComponent<AudioSource>();
            // Unity's missing/destroyed native component is not a CLR null (so ?? is insufficient).
            if (source == null) source = host.AddComponent<AudioSource>();
            if (source == null) return;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = .14f;
            if (Clips[index] == null) Clips[index] = MakeClip(cue);
            source.PlayOneShot(Clips[index]);
        }

        static AudioClip MakeClip(Cue cue)
        {
            const int rate = 22050;
            var seconds = cue == Cue.Hit ? .11f : .16f;
            var samples = new float[Mathf.CeilToInt(rate * seconds)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                var envelope = Mathf.Sin(Mathf.PI * i / samples.Length);
                envelope *= envelope;
                var frequency = cue == Cue.Confirm ? 620f + 160f * t / seconds :
                    cue == Cue.Harvest ? 430f + 100f * t / seconds : 180f - 70f * t / seconds;
                var tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
                var texture = cue == Cue.Hit ? Mathf.Sin(2f * Mathf.PI * 43f * t) * .22f :
                    Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * t) * .15f;
                samples[i] = (tone + texture) * envelope * .6f;
            }
            var clip = AudioClip.Create("HostFeedback_" + cue, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
