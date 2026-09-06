using System.Collections.Generic;
using Bunker.Systems.Settings;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// M-04 ayarları. Test edilen şey <b>oyuncunun kendi oyununu bozamaması</b>:
    /// sınırlar ayar sınıfında duruyor, ayar ekranında değil.
    ///
    /// <para>Bu dosyanın var olabilmesi, <c>GameSettings</c>'in Unity'ye bağlı
    /// olmamasının karşılığı: depo bir arayüz, testte sahtesi bağlanıyor.</para>
    /// </summary>
    public sealed class GameSettingsTests
    {
        /// <summary>Diski taklit eden depo. Yazılanı hatırlar, başka bir şey yapmaz.</summary>
        private sealed class FakeStore : ISettingsStore
        {
            private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
            private readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();

            public int FlushCount { get; private set; }

            public float GetFloat(string key, float fallback) =>
                _floats.TryGetValue(key, out float v) ? v : fallback;

            public void SetFloat(string key, float value) => _floats[key] = value;

            public int GetInt(string key, int fallback) =>
                _ints.TryGetValue(key, out int v) ? v : fallback;

            public void SetInt(string key, int value) => _ints[key] = value;

            public bool GetBool(string key, bool fallback) =>
                _bools.TryGetValue(key, out bool v) ? v : fallback;

            public void SetBool(string key, bool value) => _bools[key] = value;

            public void Flush() => FlushCount++;
        }

        [SetUp]
        public void Setup() => GameSettings.Clear();

        [TearDown]
        public void Teardown() => GameSettings.Clear();

        [Test]
        public void Hassasiyet_AltSinirinAltinaINMEZ()
        {
            // Sifir hassasiyet, oyuncunun etrafina bakamadigi bir oyun demektir ve
            // sebebini bulmasi dakikalar surer.
            GameSettings.MouseSensitivity = 0f;

            Assert.AreEqual(GameSettings.MinSensitivity, GameSettings.MouseSensitivity, 0.0001f);
        }

        [Test]
        public void Hassasiyet_UstSinirinUstuneCIKMAZ()
        {
            GameSettings.MouseSensitivity = 99f;

            Assert.AreEqual(GameSettings.MaxSensitivity, GameSettings.MouseSensitivity, 0.0001f);
        }

        [Test]
        public void Ses_SifirBirArasindaKalir()
        {
            GameSettings.MasterVolume = -1f;
            Assert.AreEqual(0f, GameSettings.MasterVolume, 0.0001f);

            GameSettings.MasterVolume = 5f;
            Assert.AreEqual(1f, GameSettings.MasterVolume, 0.0001f);
        }

        [Test]
        public void DepoBagliDegilse_AyarlarYineCalisir()
        {
            // Testler ve ilk acilis: kalici olmamasi calismamasi demek degil.
            GameSettings.MouseSensitivity = 0.10f;

            Assert.AreEqual(0.10f, GameSettings.MouseSensitivity, 0.0001f);
        }

        [Test]
        public void Depo_YazilaniHatirlar()
        {
            var store = new FakeStore();
            GameSettings.AttachStore(store);

            GameSettings.MouseSensitivity = 0.12f;
            GameSettings.InvertY = true;

            // Yeniden baglamak "oyunu kapatip acmak"la ayni: degerler diskten gelmeli.
            GameSettings.Clear();
            GameSettings.AttachStore(store);

            Assert.AreEqual(0.12f, GameSettings.MouseSensitivity, 0.0001f);
            Assert.IsTrue(GameSettings.InvertY);
        }

        [Test]
        public void Degisiklik_OlayYayinlar()
        {
            int changes = 0;
            GameSettings.Changed += () => changes++;

            GameSettings.MouseSensitivity = 0.11f;

            Assert.AreEqual(1, changes);
        }

        [Test]
        public void AyniDegerYazmak_OlayYayinlamaz()
        {
            // Kare basina ayni degeri yazan bir kaydiraci dinleyen ses servisi,
            // saniyede altmis kez AudioSource ayari yapardi (ui-code.md).
            GameSettings.MouseSensitivity = 0.11f;

            int changes = 0;
            GameSettings.Changed += () => changes++;

            GameSettings.MouseSensitivity = 0.11f;

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void FabrikaAyarlari_HerSeyiGeriAlir()
        {
            GameSettings.MouseSensitivity = 0.25f;
            GameSettings.MasterVolume = 0.1f;
            GameSettings.InvertY = true;

            GameSettings.ResetToDefaults();

            Assert.AreEqual(GameSettings.DefaultSensitivity, GameSettings.MouseSensitivity, 0.0001f);
            Assert.AreEqual(1f, GameSettings.MasterVolume, 0.0001f);
            Assert.IsFalse(GameSettings.InvertY);
        }

        [Test]
        public void Kaydetme_YalnizcaIstenincePlatformaYazar()
        {
            var store = new FakeStore();
            GameSettings.AttachStore(store);

            GameSettings.MouseSensitivity = 0.13f;
            GameSettings.MouseSensitivity = 0.14f;

            Assert.AreEqual(0, store.FlushCount, "kaydirma sirasinda diske yazilmaz");

            GameSettings.Save();

            Assert.AreEqual(1, store.FlushCount);
        }
    }
}
