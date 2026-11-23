using System.Collections.Generic;
using UnityEngine;

namespace PaperWings.Audio
{
    /// <summary>Every sound effect in the game.</summary>
    public enum Sfx
    {
        Throw, Boost, Bump, PaperRip, Crumple,
        StapleFire, StapleHit, PencilLaunch, PencilImpact, LockBeep, NoLock,
        RingChime, LandJingle, ShotDown, Victory,
        BossClank, BossClack, BossSlam, BossDefeated,
        UiMove, UiConfirm,
        Alarm, Pop, Ding                      // Stage 29: room events, balloons, the microwave
    }

    /// <summary>
    /// Synthesizes every sound in code the first time it's needed. There are no audio files:
    /// wind is filtered noise, chimes are sine waves, hits are short noise bursts with envelopes.
    /// It's crude compared to recorded sound, but it's free, tiny, and easy to tweak right here.
    ///
    /// Building blocks used below:
    ///   Noise()         random samples -1..1 (white noise: a "shhh")
    ///   LowPass         a one-pole filter: lower cutoff = darker, duller sound
    ///   Env(t, a, d)    envelope: rises over 'a' seconds, then fades with time constant 'd'
    ///   Sine / Square   tones at a frequency
    /// UNITY NOTE: AudioClip.Create + SetData turns an array of samples into a playable clip.
    /// </summary>
    public static class SoundBank
    {
        const int SR = 44100;   // samples per second

        static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        static AudioClip wind, rumble, draft, flutter, lockTone;
        static System.Random rng = new System.Random(1234);

        // ---- Looping beds (used by PlaneAudio) ----
        public static AudioClip WindLoop => wind ??= Loop("WindLoop", 3f, GenWind);
        public static AudioClip RumbleLoop => rumble ??= Loop("RumbleLoop", 3f, GenRumble);
        public static AudioClip DraftLoop => draft ??= Loop("DraftLoop", 3f, GenDraft);
        public static AudioClip FlutterLoop => flutter ??= Loop("FlutterLoop", 2f, GenFlutter);
        public static AudioClip LockToneLoop => lockTone ??= Make("LockTone", GenLockTone());

        public static AudioClip Get(Sfx s)
        {
            if (clips.TryGetValue(s, out var c) && c != null) return c;
            c = Make(s.ToString(), Generate(s));
            clips[s] = c;
            return c;
        }

        // ------------------------------------------------------------------ one-shots

        static float[] Generate(Sfx s)
        {
            switch (s)
            {
                case Sfx.Throw:        return Whoosh(0.35f, 500f, 4000f, 0.05f, 0.12f, 0.8f);
                case Sfx.Boost:        return RubberBandSnap();
                case Sfx.Bump:         return Thump(0.2f, 110f, 0.06f);
                case Sfx.PaperRip:     return Crackle(0.25f, 0.35f, 3500f, 0.08f);
                case Sfx.Crumple:      return Mix(Crackle(0.7f, 0.25f, 3000f, 0.25f), Thump(0.7f, 80f, 0.1f), 1f, 0.6f);
                case Sfx.StapleFire:   return StapleClick();
                case Sfx.StapleHit:    return Mix(Crackle(0.16f, 0.45f, 4000f, 0.045f), BandBurst(0.16f, 900f, 0.4f, 0.001f, 0.015f), 1f, 0.6f);
                case Sfx.PencilLaunch: return Mix(Whoosh(0.8f, 300f, 5000f, 0.25f, 0.3f, 1f), Sweep(0.8f, 300f, 900f, 0.2f, 0.3f), 1f, 0.25f);
                case Sfx.PencilImpact: return PaperBlast();
                case Sfx.LockBeep:     return Tone(0.06f, 1400f, 0.002f, 0.04f, true);
                case Sfx.NoLock:       return Concat(Tone(0.08f, 500f, 0.005f, 0.06f, true), Tone(0.1f, 340f, 0.005f, 0.07f, true));
                case Sfx.RingChime:    return Mix(Tone(0.7f, 1046.5f, 0.005f, 0.25f, false), Tone(0.7f, 1568f, 0.005f, 0.2f, false), 1f, 0.7f);
                case Sfx.LandJingle:   return Arpeggio(new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.11f, 0.25f);
                case Sfx.ShotDown:     return ShotDownCrumple();
                case Sfx.Victory:      return Mix(Arpeggio(new[] { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, 0.1f, 0.6f), Chord(1.6f, new[] { 523.3f, 659.3f, 784f }, 0.5f, 0.6f), 1f, 0.5f);
                case Sfx.BossClank:    return Clank(0.45f, 1f);
                case Sfx.BossClack:    return Clank(0.08f, 1.8f);
                case Sfx.BossSlam:     return Mix(Thump(0.9f, 55f, 0.35f), Clank(0.6f, 0.7f), 1f, 0.5f);
                case Sfx.BossDefeated: return Mix(Crackle(1.6f, 0.3f, 2500f, 0.6f), Mix(Clank(1.6f, 0.6f), Thump(1.6f, 45f, 0.5f), 1f, 1f), 1f, 0.7f);
                case Sfx.UiMove:       return Tone(0.05f, 900f, 0.002f, 0.02f, false);
                case Sfx.UiConfirm:    return Concat(Tone(0.07f, 880f, 0.002f, 0.04f, false), Tone(0.12f, 1320f, 0.002f, 0.06f, false));
                case Sfx.Alarm:        return Concat(Concat(Sweep(0.35f, 700f, 1100f, 0.01f, 0.3f, true), Sweep(0.35f, 700f, 1100f, 0.01f, 0.3f, true)), Sweep(0.35f, 700f, 1100f, 0.01f, 0.3f, true));
                case Sfx.Pop:          return Mix(BandBurst(0.12f, 1800f, 0.3f, 0.0005f, 0.02f), Thump(0.12f, 160f, 0.03f), 1f, 0.6f);
                case Sfx.Ding:         return Mix(Tone(1.2f, 1760f, 0.002f, 0.5f, false), Tone(1.2f, 2637f, 0.002f, 0.35f, false), 1f, 0.4f);
            }
            return new float[1];
        }

        // ------------------------------------------------------------------ recipes

        /// <summary>Noise whose brightness sweeps up then down: a "fwoosh".</summary>
        static float[] Whoosh(float len, float fLow, float fHigh, float attack, float decay, float gain)
        {
            var d = Buf(len);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, p = t / len;
                float cutoff = Mathf.Lerp(fLow, fHigh, Mathf.Sin(p * Mathf.PI));   // up, then down
                y += Coef(cutoff) * (Noise() - y);
                d[i] = y * Env(t, attack, decay) * gain;
            }
            return Normalize(d, 0.8f);
        }

        /// <summary>
        /// Rubber band boost, built only from shaped noise (no pitched notes, so it doesn't sound "chiptune"):
        ///   snap   - a very short, bright crack as the band lets go
        ///   thrum  - noise through a narrow, resonant filter whose centre drops: a rubbery "vwomm"
        ///   rush   - a quick whoosh of acceleration
        /// </summary>
        static float[] RubberBandSnap()
        {
            var d = Buf(0.55f);
            var snapF = new BandPass(); var thrumF = new BandPass();
            float rushY = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float snap = snapF.Run(Noise(), 1800f, 0.7f) * Env(t, 0.0008f, 0.018f);
                float thrumCentre = Mathf.Lerp(190f, 85f, Mathf.Clamp01(t / 0.3f));
                float thrum = thrumF.Run(Noise(), thrumCentre, 0.08f) * Env(t, 0.004f, 0.11f);   // low damping = ringing
                float rushCut = Mathf.Lerp(700f, 3500f, Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI));
                rushY += Coef(rushCut) * (Noise() - rushY);
                float rush = rushY * Env(t, 0.05f, 0.16f);
                d[i] = snap * 1.2f + thrum * 1.6f + rush * 0.5f;
            }
            return Normalize(d, 0.85f);
        }

        /// <summary>
        /// Pencil impact: a punchy "pop" (low band of noise), a body thump that drops in pitch like a
        /// kick drum, then a tail of shredding paper (crackles), bright at first, darker as it fades.
        /// </summary>
        static float[] PaperBlast()
        {
            var d = Buf(0.75f);
            var popF = new BandPass();
            float tailY = 0f, gate = 0f, phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float pop = popF.Run(Noise(), 420f, 0.5f) * Env(t, 0.001f, 0.045f);
                phase += Mathf.Lerp(95f, 45f, Mathf.Clamp01(t / 0.15f)) / SR;
                float body = Mathf.Sin(phase * 2f * Mathf.PI) * Env(t, 0.002f, 0.09f);
                if (i % 140 == 0) gate = rng.NextDouble() < Mathf.Lerp(0.6f, 0.15f, t / 0.75f) ? 1f : 0.1f;
                tailY += Coef(Mathf.Lerp(5000f, 1500f, t / 0.75f)) * (Noise() - tailY);
                float tail = tailY * gate * Env(t, 0.01f, 0.22f);
                d[i] = pop * 1.5f + body * 0.9f + tail * 0.8f;
            }
            return Normalize(d, 0.9f);
        }

        /// <summary>
        /// A rival shot down: two layers of crumpling paper (bright and dull), a soft low "whump",
        /// and a falling whoosh as it drops out of the sky. Noise only, no tones.
        /// </summary>
        static float[] ShotDownCrumple()
        {
            var d = Buf(1.2f);
            var whumpF = new BandPass();
            float brightY = 0f, dullY = 0f, fallY = 0f, gateA = 0f, gateB = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                if (i % 150 == 0) gateA = rng.NextDouble() < 0.35f ? 1f : 0.1f;
                if (i % 330 == 0) gateB = rng.NextDouble() < 0.45f ? 1f : 0.2f;
                float n = Noise();
                brightY += Coef(3200f) * (n - brightY);
                dullY += Coef(900f) * (Noise() - dullY);
                float crumple = (brightY * gateA * 0.8f + dullY * gateB * 1.2f) * Env(t, 0.005f, 0.35f);
                float whump = whumpF.Run(Noise(), 120f, 0.35f) * Env(t, 0.003f, 0.18f);
                fallY += Coef(Mathf.Lerp(2500f, 350f, t / 1.2f)) * (Noise() - fallY);
                float fall = fallY * Env(t, 0.2f, 0.45f);
                d[i] = crumple + whump * 1.6f + fall * 0.45f;
            }
            return Normalize(d, 0.85f);
        }

        /// <summary>A short burst of band-filtered noise (a dry knock or tap).</summary>
        static float[] BandBurst(float len, float centre, float damping, float attack, float decay)
        {
            var d = Buf(len);
            var f = new BandPass();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = f.Run(Noise(), centre, damping) * Env(t, attack, decay);
            }
            return Normalize(d, 0.8f);
        }

        /// <summary>
        /// A tiny "state variable" band-pass filter: lets through a band of frequencies around a centre.
        /// Lower damping = narrower band that rings (resonates) like a struck object.
        /// </summary>
        struct BandPass
        {
            float low, band;
            public float Run(float x, float centre, float damping)
            {
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(centre, SR * 0.2f) / SR);
                low += f * band;
                float high = x - low - damping * band;
                band += f * high;
                return band;
            }
        }

        /// <summary>Low "thud": a quickly fading low sine plus a soft noise puff.</summary>
        static float[] Thump(float len, float freq, float decay)
        {
            var d = Buf(len);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                y += Coef(800f) * (Noise() - y);
                d[i] = Mathf.Sin(2f * Mathf.PI * freq * t * (1f - 0.3f * t)) * Env(t, 0.003f, decay) + y * Env(t, 0.001f, decay * 0.5f);
            }
            return Normalize(d, 0.9f);
        }

        /// <summary>Paper sounds: noise chopped into random crackles, fading out.</summary>
        static float[] Crackle(float len, float density, float cutoff, float decay)
        {
            var d = Buf(len);
            float y = 0f, gate = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                if (i % 160 == 0) gate = rng.NextDouble() < density ? 1f : 0.15f;   // re-roll every ~3.6 ms
                y += Coef(cutoff) * (Noise() - y);
                d[i] = y * gate * Env(t, 0.002f, decay);
            }
            return Normalize(d, 0.85f);
        }

        /// <summary>Staple "chk": a tiny noise click plus two high, fast-fading tones.</summary>
        static float[] StapleClick()
        {
            var d = Buf(0.12f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float click = t < 0.003f ? Noise() : 0f;
                float ping = (Mathf.Sin(2f * Mathf.PI * 2600f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 3900f * t)) * Env(t, 0.001f, 0.025f);
                d[i] = click + ping * 0.6f;
            }
            return Normalize(d, 0.7f);
        }

        /// <summary>Metal "clank": several unrelated (inharmonic) tones fading at different speeds.</summary>
        static float[] Clank(float len, float pitch)
        {
            float[] freqs = { 310f, 742f, 1187f, 1730f };
            float[] decays = { 0.3f, 0.2f, 0.15f, 0.1f };
            var d = Buf(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * freqs[k] * pitch * t) * Env(t, 0.001f, decays[k] * (len / 0.45f));
                if (t < 0.004f) s += Noise() * 1.5f;
                d[i] = s;
            }
            return Normalize(d, 0.85f);
        }

        static float[] Tone(float len, float freq, float attack, float decay, bool square)
        {
            var d = Buf(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float w = Mathf.Sin(2f * Mathf.PI * freq * t);
                if (square) w = Mathf.Clamp(w * 3f, -1f, 1f) * 0.6f;   // soft square = more "beepy"
                d[i] = w * Env(t, attack, decay);
            }
            return Normalize(d, 0.6f);
        }

        static float[] Sweep(float len, float f0, float f1, float attack, float decay, bool square = false)
        {
            var d = Buf(len);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                phase += Mathf.Lerp(f0, f1, t / len) / SR;
                float w = Mathf.Sin(phase * 2f * Mathf.PI);
                if (square) w = Mathf.Clamp(w * 3f, -1f, 1f) * 0.6f;
                d[i] = w * Env(t, attack, decay);
            }
            return Normalize(d, 0.6f);
        }

        static float[] Arpeggio(float[] notes, float step, float decay)
        {
            var d = Buf(step * notes.Length + decay * 3f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * SR);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)SR;
                    // Triangle-ish: fundamental plus a soft third harmonic.
                    float w = Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.2f * Mathf.Sin(6f * Mathf.PI * notes[n] * t);
                    d[i] += w * Env(t, 0.004f, decay);
                }
            }
            return Normalize(d, 0.6f);
        }

        static float[] Chord(float len, float[] notes, float delay, float decay)
        {
            var d = Buf(len);
            int start = (int)(delay * SR);
            for (int i = start; i < d.Length; i++)
            {
                float t = (i - start) / (float)SR, s = 0f;
                foreach (var f in notes) s += Mathf.Sin(2f * Mathf.PI * f * t);
                d[i] = s * Env(t, 0.02f, decay);
            }
            return Normalize(d, 0.6f);
        }

        // ------------------------------------------------------------------ loops

        static void GenWind(float[] d)
        {
            float y = 0f;
            for (int i = 0; i < d.Length; i++) { y += Coef(3000f) * (Noise() - y); d[i] = y; }
        }

        static void GenRumble(float[] d)
        {
            // Brown noise (a random walk) through a low filter: a deep, soft roar.
            float b = 0f, y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                b = (b + 0.02f * Noise()) / 1.02f;
                y += Coef(250f) * (b - y);
                d[i] = y;
            }
        }

        static void GenDraft(float[] d)
        {
            // Band of noise (a low-pass minus a lower low-pass) with a gentle 1 Hz swell.
            float lo = 0f, hi = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float n = Noise();
                hi += Coef(1500f) * (n - hi);
                lo += Coef(300f) * (n - lo);
                float t = i / (float)SR;
                d[i] = (hi - lo) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t));
            }
        }

        static void GenFlutter(float[] d)
        {
            // Paper flapping: noise switched on and off ~22 times a second.
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float flap = Mathf.Sin(2f * Mathf.PI * 22f * t) > 0f ? 1f : 0.25f;
                y += Coef(2500f) * (Noise() - y);
                d[i] = y * flap;
            }
        }

        static float[] GenLockTone()
        {
            // Steady high beep tone for "LOCKED" (exactly 900 cycles in 0.5 s, so it loops cleanly).
            var d = Buf(0.5f);
            for (int i = 0; i < d.Length; i++)
                d[i] = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * 1800f * i / SR) * 3f, -1f, 1f) * 0.3f;
            return d;
        }

        // ------------------------------------------------------------------ helpers

        static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(SR * seconds))];
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        /// <summary>One-pole low-pass coefficient for a cutoff frequency.</summary>
        static float Coef(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR);
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        static float[] Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            foreach (var v in d) max = Mathf.Max(max, Mathf.Abs(v));
            float g = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            return d;
        }

        static float[] Mix(float[] a, float[] b, float ga, float gb)
        {
            var d = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < d.Length; i++)
                d[i] = (i < a.Length ? a[i] * ga : 0f) + (i < b.Length ? b[i] * gb : 0f);
            return Normalize(d, 0.85f);
        }

        static float[] Concat(float[] a, float[] b)
        {
            var d = new float[a.Length + b.Length];
            a.CopyTo(d, 0);
            b.CopyTo(d, a.Length);
            return d;
        }

        /// <summary>Generates a loop and crossfades its end into its start so it repeats without a click.</summary>
        static AudioClip Loop(string name, float seconds, System.Action<float[]> gen)
        {
            int fade = SR / 10;
            var raw = Buf(seconds + 0.1f);
            gen(raw);
            var d = new float[raw.Length - fade];
            for (int i = 0; i < d.Length; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = raw[i] * k + raw[d.Length + i] * (1f - k);
            }
            return Make(name, Normalize(d, 0.7f));
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
