using UnityEngine;

namespace Bunker.Audio
{
    /// <summary>
    /// Bir sesin oyun tarafındaki sözleşmesi: ne kadar yüksek, 3B mi, kaç tanesi aynı
    /// anda çalabilir, ne kadar önemli.
    ///
    /// <para><b>Öncelik ve ses limiti burada, çağıran tarafta değil.</b> Kırk eşzamanlı
    /// çarpma sesi kırpma üretmemeli, zarifçe seyrelmelidir (audio-code.md). Bunu her
    /// çağıran yerin ayrı ayrı bilmesi imkânsızdır; sözleşme sesin kendisine aittir.</para>
    /// </summary>
    internal readonly struct SfxSpec
    {
        public readonly float Volume;
        public readonly bool Spatial;

        /// <summary>Aynı anda bu sesten en fazla kaç tane. Aşılırsa yenisi düşer.</summary>
        public readonly int VoiceLimit;

        /// <summary>Öncelik: sesler tükendiğinde düşük öncelikli olan kurban edilir.</summary>
        public readonly int Priority;

        /// <summary>Perde rastgeleliği (± oran). Tekrarın makineleşmesini engeller.</summary>
        public readonly float PitchJitter;

        /// <summary>Aynı sesin iki tetiklenmesi arasındaki en kısa süre.</summary>
        public readonly float MinIntervalSeconds;

        public SfxSpec(float volume, bool spatial, int voiceLimit, int priority,
                       float pitchJitter, float minIntervalSeconds)
        {
            Volume = volume;
            Spatial = spatial;
            VoiceLimit = voiceLimit;
            Priority = priority;
            PitchJitter = pitchJitter;
            MinIntervalSeconds = minIntervalSeconds;
        }
    }

    /// <summary>
    /// Sesleri <b>çalışma anında sentezler</b>. M-01'de tek bir ses dosyası yok ve
    /// olmaması bir eksiklik değil, bir sıra meselesi: ses yönü henüz kilitlenmedi,
    /// kilitlenmeden alınan her dosya sonra atılacak.
    ///
    /// <para><b>Yine de sessiz kalınamaz.</b> Oyun testinde arkadan gelen zombinin
    /// duyulmaması bir ses eksikliği değil, bir <i>oynanış</i> eksikliğidir — oyuncunun
    /// arkasını dönme sebebi ortadan kalkar. Bu banka o boşluğu, hiçbir varlığa
    /// bağlanmadan doldurur: gri kutunun sesli karşılığı.</para>
    ///
    /// <para><b>Her ses dört varyant üretir</b> (audio-code.md): tek varyantlı, dakikada
    /// onlarca kez çalan bir ses oyuncuya sesi kapattırır. Varyantlar sabit tohumla
    /// üretilir, yani her makinede aynıdır ve bir hata tekrarlanabilir.</para>
    ///
    /// <para><b>Üretim tembeldir:</b> bir ses ilk kez çalınana kadar hiçbir örnek
    /// hesaplanmaz. Açılışta yirmi sesin hepsini üretmek, oyunun ilk karesini
    /// geciktirmekten başka bir işe yaramaz.</para>
    /// </summary>
    internal sealed class SfxBank
    {
        private const int SampleRate = 22050;
        private const int VariantsPerSound = 4;

        private readonly AudioClip[][] _clips = new AudioClip[64][];

        public int VariantCount => VariantsPerSound;

        /// <summary>Sesin oyun sözleşmesi. Bilinmeyen bir ses sessizdir, çökmez.</summary>
        public static SfxSpec Spec(SfxId id) => id switch
        {
            //                                    ses    3B     limit oncelik jitter  min aralik
            SfxId.GunShot         => new SfxSpec(0.55f, false, 4,  200, 0.05f, 0f),
            SfxId.GunDryFire      => new SfxSpec(0.35f, false, 1,  120, 0.04f, 0.10f),
            SfxId.ReloadOut       => new SfxSpec(0.40f, false, 2,  140, 0.06f, 0f),
            SfxId.ReloadIn        => new SfxSpec(0.45f, false, 2,  140, 0.06f, 0f),
            SfxId.HitMarker       => new SfxSpec(0.30f, false, 3,  160, 0.03f, 0.02f),
            SfxId.HeadshotMarker  => new SfxSpec(0.38f, false, 3,  170, 0.03f, 0.02f),

            SfxId.KnifeSwing      => new SfxSpec(0.45f, false, 2,  150, 0.10f, 0f),
            SfxId.KnifeHit        => new SfxSpec(0.55f, false, 2,  180, 0.10f, 0f),

            // Zombi sesleri 3B ve bilerek yuksek: arkadan gelen zombiyi duymak,
            // oyuncunun arkasini donme sebebidir. Duyulmayan zombi, tasarim olarak
            // var olmayan zombidir.
            SfxId.ZombieGroan     => new SfxSpec(0.60f, true,  6,  100, 0.14f, 0f),
            SfxId.ZombieAlert     => new SfxSpec(0.75f, true,  5,  150, 0.12f, 0f),
            SfxId.ZombieAttack    => new SfxSpec(0.85f, true,  6,  190, 0.10f, 0f),
            SfxId.ZombieHurt      => new SfxSpec(0.50f, true,  6,  110, 0.15f, 0.05f),
            SfxId.ZombieDeath     => new SfxSpec(0.70f, true,  6,  160, 0.12f, 0f),
            SfxId.ZombieVault     => new SfxSpec(0.55f, true,  4,  120, 0.12f, 0f),
            SfxId.ZombieLegBreak  => new SfxSpec(0.80f, true,  3,  200, 0.08f, 0f),

            SfxId.BarricadeTear   => new SfxSpec(0.70f, true,  4,  170, 0.10f, 0.05f),
            SfxId.BarricadeRepair => new SfxSpec(0.60f, true,  3,  150, 0.08f, 0f),
            SfxId.PlayerHurt      => new SfxSpec(0.80f, false, 2,  240, 0.06f, 0.05f),

            SfxId.Purchase        => new SfxSpec(0.55f, false, 1,  200, 0f,    0.05f),
            SfxId.Denied          => new SfxSpec(0.45f, false, 1,  180, 0f,    0.15f),
            SfxId.RoundStart      => new SfxSpec(0.70f, false, 1,  250, 0f,    0f),
            SfxId.RoundCleared    => new SfxSpec(0.60f, false, 1,  250, 0f,    0f),
            SfxId.RunOver         => new SfxSpec(0.80f, false, 1,  255, 0f,    0f),

            _ => new SfxSpec(0f, false, 1, 0, 0f, 0f)
        };

        /// <summary>
        /// Bir varyantı verir; ilk istekte üretir. <b>Aynı tohum, aynı dalga biçimi</b> —
        /// bir sesin "bugün başka geliyor" olması bir hata değil, imkânsız olmalı.
        /// </summary>
        public AudioClip Clip(SfxId id, int variant)
        {
            int index = (int)id;
            if (index <= 0 || index >= _clips.Length) return null;

            AudioClip[] set = _clips[index];

            if (set == null)
            {
                set = new AudioClip[VariantsPerSound];

                for (int v = 0; v < VariantsPerSound; v++)
                {
                    // Tohum: ses kimligi ve varyant numarasi. Sabit, yani tekrarlanabilir.
                    var rng = new Rng((uint)(index * 7919 + v * 104729 + 1));
                    float[] samples = Render(id, ref rng);

                    var clip = AudioClip.Create($"sfx_{id}_{v:D2}", samples.Length, 1, SampleRate, false);
                    clip.SetData(samples, 0);
                    set[v] = clip;
                }

                _clips[index] = set;
            }

            return set[variant % VariantsPerSound];
        }

        // ---------------------------------------------------------------- tarifler

        private static float[] Render(SfxId id, ref Rng rng) => id switch
        {
            SfxId.GunShot         => GunShot(ref rng),
            SfxId.GunDryFire      => Click(ref rng, 0.05f, 2600f, 0.5f),
            SfxId.ReloadOut       => Click(ref rng, 0.10f, 1400f, 0.7f),
            SfxId.ReloadIn        => Click(ref rng, 0.14f, 900f, 0.9f),
            SfxId.HitMarker       => Blip(1250f, 0.07f),
            SfxId.HeadshotMarker  => Blip(1900f, 0.11f),
            SfxId.KnifeSwing      => Swoosh(ref rng),
            SfxId.KnifeHit        => Thud(ref rng, 0.28f, 140f, 0.9f),
            SfxId.ZombieGroan     => Growl(ref rng, 1.20f, 78f, -0.10f, 0.55f),
            SfxId.ZombieAlert     => Growl(ref rng, 0.70f, 95f, 0.35f, 0.85f),
            SfxId.ZombieAttack    => Growl(ref rng, 0.45f, 130f, 0.55f, 1.00f),
            SfxId.ZombieHurt      => Growl(ref rng, 0.30f, 150f, -0.30f, 0.80f),
            SfxId.ZombieDeath     => Growl(ref rng, 1.05f, 120f, -0.55f, 0.90f),
            SfxId.ZombieVault     => Scrape(ref rng),
            SfxId.ZombieLegBreak  => Crunch(ref rng),
            SfxId.BarricadeTear   => Crack(ref rng),
            SfxId.BarricadeRepair => Thud(ref rng, 0.22f, 220f, 0.7f),
            SfxId.PlayerHurt      => Thud(ref rng, 0.45f, 90f, 1.0f),
            SfxId.Purchase        => TwoTone(620f, 930f, 0.22f),
            SfxId.Denied          => TwoTone(420f, 260f, 0.24f),
            SfxId.RoundStart      => Horn(62f, 1.5f),
            SfxId.RoundCleared    => TwoTone(520f, 780f, 0.45f),
            SfxId.RunOver         => Horn(44f, 2.2f),
            _ => new float[1]
        };

        /// <summary>
        /// Silah sesi: keskin gürültü patlaması + alçalan gövde. <b>Atağın ilk 20 ms'si
        /// sesin tamamıdır</b> — oyuncu tetiğe bastığını oradan anlar (audio-code.md:
        /// zamanlama örnek kalitesini yener).
        /// </summary>
        private static float[] GunShot(ref Rng rng)
        {
            float[] b = New(0.38f);
            float lp = 0f;

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float crack = Mathf.Exp(-t * 34f);
                float tail = Mathf.Exp(-t * 8f) * 0.30f;

                // Tek kutuplu alcak geciren: baslangicta acik, sonra kapaniyor.
                // Parlaklik zamanla dusunce "patlama, sonra yankisi" okunur.
                float cut = Mathf.Lerp(0.12f, 0.85f, crack);
                lp += (rng.Bipolar() - lp) * cut;

                float body = Mathf.Sin(2f * Mathf.PI * (110f - 55f * Mathf.Min(t * 6f, 1f)) * t)
                             * Mathf.Exp(-t * 16f) * 0.55f;

                b[i] = Clamp(lp * (crack + tail) * 1.6f + body);
            }

            return b;
        }

        /// <summary>
        /// Zombi sesleri: testere dalgası + gürültü, boğaz benzeri bir süzgeçten.
        /// <paramref name="glide"/> pozitifse ses yükselir (uyarı, saldırı), negatifse
        /// alçalır (acı, ölüm) — <b>perdenin yönü olayın kendisini söyler</b>.
        /// </summary>
        private static float[] Growl(ref Rng rng, float seconds, float baseHz, float glide, float grit)
        {
            float[] b = New(seconds);
            float lp = 0f;
            float phase = 0f;
            float vibratoHz = 4.5f + rng.Float() * 3f;

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float u = t / seconds;

                float hz = baseHz * (1f + glide * u) *
                           (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * vibratoHz * t));

                phase += hz / SampleRate;
                if (phase > 1f) phase -= 1f;

                // Testere: harmonik yigin. Saf sinus "elektronik", testere "canli" okunur.
                float saw = phase * 2f - 1f;
                float noise = rng.Bipolar() * grit * 0.45f;

                lp += (saw + noise - lp) * 0.22f;

                // Yavas atak (nefes), uzun kuyruk.
                float env = Mathf.Min(1f, u * 6f) * Mathf.Pow(1f - u, 1.6f);

                b[i] = Clamp(lp * env * 1.5f);
            }

            return b;
        }

        private static float[] Swoosh(ref Rng rng)
        {
            const float seconds = 0.30f;
            float[] b = New(seconds);
            float lp = 0f, hp = 0f, prev = 0f;

            for (int i = 0; i < b.Length; i++)
            {
                float u = T(i) / seconds;

                lp += (rng.Bipolar() - lp) * Mathf.Lerp(0.10f, 0.60f, u);

                // Yuksek geciren: savurusun "hava" karakteri alcak frekanslarda degil.
                float x = lp;
                hp = 0.90f * (hp + x - prev);
                prev = x;

                b[i] = Clamp(hp * Mathf.Sin(Mathf.PI * u) * 2.2f);
            }

            return b;
        }

        private static float[] Thud(ref Rng rng, float seconds, float hz, float punch)
        {
            float[] b = New(seconds);
            float lp = 0f;

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float env = Mathf.Exp(-t * (3.2f / seconds));

                float body = Mathf.Sin(2f * Mathf.PI * hz * t * (1f - 0.35f * t)) * punch;
                lp += (rng.Bipolar() - lp) * 0.25f;

                b[i] = Clamp((body + lp * 0.55f * Mathf.Exp(-t * 26f)) * env);
            }

            return b;
        }

        /// <summary>
        /// Kemik kırılması. <b>Düz gürültü "hışırtı" okunur, çatırtı "kırılma" okunur</b> —
        /// fark, rastgele aralıklarla açılan mikro patlamalarda.
        /// </summary>
        private static float[] Crunch(ref Rng rng)
        {
            float[] b = New(0.40f);
            float lp = 0f;

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float env = Mathf.Exp(-t * 9f);

                float grain = rng.Float() < 0.06f ? rng.Bipolar() * 1.4f : rng.Bipolar() * 0.25f;
                lp += (grain - lp) * 0.55f;

                float snap = Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 22f) * 0.5f;
                b[i] = Clamp((lp * 1.5f + snap) * env);
            }

            return b;
        }

        private static float[] Crack(ref Rng rng)
        {
            float[] b = New(0.30f);

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float env = Mathf.Exp(-t * 14f);
                float grain = rng.Float() < 0.10f ? rng.Bipolar() * 1.6f : rng.Bipolar() * 0.3f;
                float wood = Mathf.Sin(2f * Mathf.PI * 320f * t) * Mathf.Exp(-t * 30f) * 0.6f;

                b[i] = Clamp((grain + wood) * env);
            }

            return b;
        }

        private static float[] Scrape(ref Rng rng)
        {
            const float seconds = 0.55f;
            float[] b = New(seconds);
            float lp = 0f;

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float u = t / seconds;

                lp += (rng.Bipolar() - lp) * 0.35f;

                // Genlik modulasyonu: duz gurultu "ruzgar", modulasyonlu gurultu
                // "surtunme" okunur.
                float rub = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 14f * t);

                b[i] = Clamp(lp * rub * Mathf.Sin(Mathf.PI * u) * 1.4f);
            }

            return b;
        }

        private static float[] Click(ref Rng rng, float seconds, float hz, float noiseMix)
        {
            float[] b = New(seconds);

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float env = Mathf.Exp(-t * (5f / seconds));
                float tone = Mathf.Sin(2f * Mathf.PI * hz * t);

                b[i] = Clamp((tone * (1f - noiseMix) + rng.Bipolar() * noiseMix) * env);
            }

            return b;
        }

        private static float[] Blip(float hz, float seconds)
        {
            float[] b = New(seconds);

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float u = t / seconds;

                // Kisa atak, kisa dusus: "tik" degil "onaylandi" okunmali.
                float env = Mathf.Min(1f, u * 20f) * Mathf.Pow(1f - u, 2f);
                b[i] = Clamp(Mathf.Sin(2f * Mathf.PI * hz * t) * env * 0.9f);
            }

            return b;
        }

        private static float[] TwoTone(float fromHz, float toHz, float seconds)
        {
            float[] b = New(seconds);
            int half = b.Length / 2;
            float halfSeconds = seconds * 0.5f;

            for (int i = 0; i < b.Length; i++)
            {
                bool second = i >= half;
                float local = second ? T(i - half) : T(i);
                float hz = second ? toHz : fromHz;

                float u = halfSeconds <= 0f ? 1f : local / halfSeconds;
                float env = Mathf.Min(1f, u * 12f) * Mathf.Pow(Mathf.Max(0f, 1f - u), 1.5f);

                b[i] = Clamp(Mathf.Sin(2f * Mathf.PI * hz * local) * env * 0.8f);
            }

            return b;
        }

        private static float[] Horn(float hz, float seconds)
        {
            float[] b = New(seconds);
            float phase = 0f;

            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float u = t / seconds;

                phase += hz * (1f + 0.02f * Mathf.Sin(2f * Mathf.PI * 3f * t)) / SampleRate;
                if (phase > 1f) phase -= 1f;

                float saw = phase * 2f - 1f;
                float env = Mathf.Min(1f, u * 5f) * Mathf.Pow(1f - u, 1.2f);

                b[i] = Clamp(saw * env * 0.8f);
            }

            return b;
        }

        // ---------------------------------------------------------------- yardimcilar

        private static float[] New(float seconds) =>
            new float[Mathf.Max(1, (int)(SampleRate * seconds))];

        private static float T(int sample) => (float)sample / SampleRate;

        private static float Clamp(float v) => v > 1f ? 1f : (v < -1f ? -1f : v);

        /// <summary>
        /// Sabit tohumlu xorshift. <c>UnityEngine.Random</c> <b>kullanılmaz</b>: o küresel
        /// bir durumdur ve başka sistemlerin çektiği sayılar bu dalga biçimlerini
        /// değiştirirdi (csharp-code.md, determinizm).
        /// </summary>
        private struct Rng
        {
            private uint _state;

            public Rng(uint seed) { _state = seed == 0u ? 1u : seed; }

            public float Float()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / (float)0x1000000;
            }

            public float Bipolar() => Float() * 2f - 1f;
        }
    }
}
