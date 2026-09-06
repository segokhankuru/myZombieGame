using Bunker.Systems.Cards;
using Bunker.Systems.Rounds;
using UnityEngine;

namespace Bunker.Audio
{
    /// <summary>
    /// Oyunun <b>tek</b> ses servisi. Oyun kodu olay bildirir; <c>AudioSource</c>'a
    /// buranın dışında hiçbir yer dokunmaz (audio-code.md).
    ///
    /// <para><b>Neden statik bir cephe:</b> ses üreten taraflar dört ayrı assembly'de
    /// yaşıyor (<c>Gameplay</c> silah, <c>AI</c> zombi, <c>UI</c> menü) ve bu assembly'ler
    /// birbirini görmüyor. <see cref="RoundSignals"/> ile aynı desen: bağımlılık ters
    /// çevrilir, kimse kimseye uzanmaz.</para>
    ///
    /// <para><b>Kaynaklar havuzlanır.</b> Atış başına bir <c>AudioSource</c> yaratmak
    /// takılma üretir; havuz sabit boyutludur ve dolduğunda <b>en düşük öncelikli</b>
    /// ses kurban edilir. Kırk eşzamanlı vuruş, kırpma değil seyrelme üretmeli.</para>
    ///
    /// <para><b>Ses varlıkları geldiğinde değişecek olan yer <see cref="SfxBank"/>'tır</b>,
    /// burası değil. Çağıran kod hiçbir zaman bir dosya adı bilmez.</para>
    /// </summary>
    public static class GameAudio
    {
        /// <summary>
        /// Eşzamanlı ses sayısı. Bir tavan olmak zorunda: sınırsız kaynak, kalabalıkta
        /// hem kırpma hem kare düşüşü demektir.
        /// </summary>
        private const int VoiceCount = 24;

        private const float MaxDistanceMeters = 40f;
        private const float MinDistanceMeters = 3f;

        private static Runtime _runtime;

        /// <summary>
        /// Oyuncu ayarı. <b>Sesi kısabilmek erişilebilirliktir, tercih değil</b>
        /// (audio-code.md). Şimdilik tek bir ana seviye; bus haritası ses yönü
        /// kilitlenince gelir.
        /// </summary>
        public static float MasterVolume { get; set; } = 1f;

        /// <summary>Dünyada bir noktadan gelen ses. Yönü ve mesafesi duyulur.</summary>
        public static void PlayAt(SfxId id, Vector3 position, float volumeScale = 1f)
        {
            Runtime r = EnsureRuntime();
            if (r != null) r.Play(id, position, true, volumeScale);
        }

        /// <summary>
        /// Oyuncunun kendi eylemi (silah, bıçak, isabet işareti). 2B çalar: kendi
        /// silahının sesi kafanın içindedir, uzayda bir yerde değil.
        /// </summary>
        public static void Play(SfxId id, float volumeScale = 1f)
        {
            Runtime r = EnsureRuntime();
            if (r != null) r.Play(id, Vector3.zero, false, volumeScale);
        }

        private static Runtime EnsureRuntime()
        {
            if (_runtime != null) return _runtime;
            if (!Application.isPlaying) return null;

            var host = new GameObject("_GameAudio");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.DontSave;

            _runtime = host.AddComponent<Runtime>();
            return _runtime;
        }

        /// <summary>
        /// Play oturumları arasında sızan statik referansı temizler.
        ///
        /// <para><b>Domain Reload kapalıyken statikler yaşar</b> ve ikinci oturumda
        /// <c>_runtime</c> yok edilmiş bir nesneye bakar — Unity'de bu, "null değil ama
        /// ölü" denen sinsi durumdur (<c>RoundSignalsBootstrap</c> ile aynı ders).</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _runtime = null;
            MasterVolume = 1f;
        }

        /// <summary>
        /// Havuzu ve bankayı taşıyan sahne nesnesi. <b>Oyun kodu bu tipi hiç görmez</b> —
        /// dışarıya açık olan tek şey <see cref="GameAudio"/>'nun iki metodudur.
        /// </summary>
        private sealed class Runtime : MonoBehaviour
        {
            private readonly SfxBank _bank = new SfxBank();

            private AudioSource[] _voices;
            private SfxId[] _playing;
            private int[] _priorities;
            private float[] _startedAt;

            private readonly float[] _lastPlayed = new float[64];
            private readonly int[] _lastVariant = new int[64];

            private void Awake()
            {
                _voices = new AudioSource[VoiceCount];
                _playing = new SfxId[VoiceCount];
                _priorities = new int[VoiceCount];
                _startedAt = new float[VoiceCount];

                for (int i = 0; i < VoiceCount; i++)
                {
                    var go = new GameObject($"Voice_{i:D2}");
                    go.transform.SetParent(transform, false);

                    AudioSource source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;

                    // Yuvarlanma egrisi ACIKCA kuruluyor: Unity varsayilani (logaritmik,
                    // 500 m) neredeyse her oyun icin yanlistir ve uzaktaki zombiyi
                    // yanindaymis gibi duyurur (audio-code.md).
                    source.rolloffMode = AudioRolloffMode.Linear;
                    source.minDistance = MinDistanceMeters;
                    source.maxDistance = MaxDistanceMeters;
                    source.dopplerLevel = 0f;

                    _voices[i] = source;
                    _startedAt[i] = float.NegativeInfinity;
                }

                for (int i = 0; i < _lastPlayed.Length; i++) _lastPlayed[i] = float.NegativeInfinity;
            }

            public void Play(SfxId id, Vector3 position, bool spatial, float volumeScale)
            {
                int index = (int)id;
                if (index <= 0 || index >= _lastPlayed.Length) return;

                SfxSpec spec = SfxBank.Spec(id);
                if (spec.Volume <= 0f || MasterVolume <= 0f) return;

                float now = Time.unscaledTime;

                // Ayni sesin ust uste yigilmasi: bir karede on zombi olurse on olum
                // sesi tek bir gurultuye doner. Kisa bir bosluk, sesi ayirir.
                if (now - _lastPlayed[index] < spec.MinIntervalSeconds) return;

                if (CountPlaying(id) >= spec.VoiceLimit) return;

                AudioClip clip = _bank.Clip(id, NextVariant(index));
                if (clip == null) return;

                int voice = ClaimVoice(spec.Priority);
                if (voice < 0) return;

                AudioSource source = _voices[voice];
                source.transform.position = position;
                source.spatialBlend = spatial && spec.Spatial ? 1f : 0f;
                source.clip = clip;
                source.volume = Mathf.Clamp01(spec.Volume * volumeScale * MasterVolume);
                source.pitch = spec.PitchJitter <= 0f
                    ? 1f
                    : 1f + Random.Range(-spec.PitchJitter, spec.PitchJitter);

                _playing[voice] = id;
                _priorities[voice] = spec.Priority;
                _startedAt[voice] = now;
                _lastPlayed[index] = now;

                source.Play();
            }

            /// <summary>
            /// Sıradaki varyant. <b>Aynı varyant art arda çalmaz</b> — tek varyantlı bir
            /// ses gibi okunmasının en hızlı yolu, dördünden aynısını iki kez seçmektir.
            /// </summary>
            private int NextVariant(int index)
            {
                int count = _bank.VariantCount;
                int next = (_lastVariant[index] + 1 + Random.Range(0, count - 1)) % count;
                _lastVariant[index] = next;
                return next;
            }

            private int CountPlaying(SfxId id)
            {
                int count = 0;

                for (int i = 0; i < _voices.Length; i++)
                {
                    if (_playing[i] == id && _voices[i].isPlaying) count++;
                }

                return count;
            }

            /// <summary>
            /// Boş bir kanal bulur; yoksa <b>en düşük öncelikli ve en eski</b> sesi keser.
            /// Yeni ses daha önemsizse hiç çalınmaz — bir patlamanın üstüne ayak sesi
            /// bindirmek, ikisini birden okunmaz kılar.
            /// </summary>
            private int ClaimVoice(int priority)
            {
                int weakest = -1;
                int weakestPriority = int.MaxValue;
                float oldest = float.MaxValue;

                for (int i = 0; i < _voices.Length; i++)
                {
                    if (!_voices[i].isPlaying) return i;

                    if (_priorities[i] < weakestPriority ||
                        (_priorities[i] == weakestPriority && _startedAt[i] < oldest))
                    {
                        weakestPriority = _priorities[i];
                        oldest = _startedAt[i];
                        weakest = i;
                    }
                }

                if (weakest < 0 || weakestPriority > priority) return -1;

                _voices[weakest].Stop();
                return weakest;
            }
        }
    }

    /// <summary>
    /// Tur ve run olaylarını sese bağlar.
    ///
    /// <para><b>Neden ayrı bir sınıf:</b> turu yürüten <c>ZombieDirector</c>'ün ses
    /// çalması gerekmiyor — onun işi tur akışı. Sinyali dinleyip sese çeviren taraf
    /// burada durursa, ses yönü değiştiğinde tek bir dosya değişir.</para>
    /// </summary>
    public static class AudioDirector
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            // Statik olaylar Play oturumlari arasinda yasar: once birak, sonra abone ol.
            // Bu iki satirin yoklugu ikinci oturumda her tur baslangicini iki kez
            // caldirir - sessiz ve sasirtici (RoundSignals'in ogrettigi ders).
            RoundSignals.RoundStarted -= OnRoundStarted;
            RoundSignals.RoundCleared -= OnRoundCleared;
            RunSignals.RunEnded -= OnRunEnded;
            CardSignals.DraftOpened -= OnDraftOpened;

            RoundSignals.RoundStarted += OnRoundStarted;
            RoundSignals.RoundCleared += OnRoundCleared;
            RunSignals.RunEnded += OnRunEnded;
            CardSignals.DraftOpened += OnDraftOpened;
        }

        private static void OnRoundStarted(int round) => GameAudio.Play(SfxId.RoundStart);

        private static void OnRoundCleared(int round) => GameAudio.Play(SfxId.RoundCleared);

        private static void OnRunEnded(RunSummary summary) => GameAudio.Play(SfxId.RunOver);

        private static void OnDraftOpened(CardDraft draft) => GameAudio.Play(SfxId.Purchase);
    }
}
