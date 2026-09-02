using System;
using System.Globalization;
using System.Threading;
using Bunker.Systems.Config;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Config ayrıştırıcısı. Bu sınıf yanlış okursa <b>oyunun bütün denge sayıları
    /// yanlış olur ve hiçbir test bunu söylemez</b> — o yüzden burada sıkı test edilir.
    /// </summary>
    public sealed class JsonTests
    {
        [Test]
        public void NesneVeAlanlar_Okunur()
        {
            JsonValue v = JsonValue.Parse("{\"a\": 1, \"b\": {\"c\": 2.5}}");

            Assert.AreEqual(1d, v["a"].AsNumber, 0.0001d);
            Assert.AreEqual(2.5d, v["b"]["c"].AsNumber, 0.0001d);
        }

        [Test]
        public void OlmayanAnahtar_NullDoner_Cokmez()
        {
            JsonValue v = JsonValue.Parse("{\"a\": 1}");

            Assert.AreEqual(JsonKind.Null, v["yok"].Kind);
            Assert.AreEqual(JsonKind.Null, v["yok"]["daha_da_yok"].Kind);
        }

        [Test]
        public void AnahtarSirasi_DosyadakiSirayiKorur()
        {
            JsonValue v = JsonValue.Parse("{\"z\": 1, \"a\": 2, \"m\": 3}");

            CollectionAssert.AreEqual(new[] { "z", "a", "m" }, v.Keys);
        }

        /// <summary>
        /// Bu testin sebebi somut: geliştirme makinesi tr-TR. Ondalık ayırıcı virgül
        /// olduğu için kültüre duyarlı bir ayrıştırıcı <c>"1.4"</c> değerini sessizce
        /// <b>14</b> okur. On kat hızlı bir zombi, hiçbir hata mesajı vermeden.
        /// </summary>
        [Test]
        public void OndalikSayi_MakineninDilindenEtkilenmez()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");

                JsonValue v = JsonValue.Parse("{\"speed\": 1.4}");

                Assert.AreEqual(1.4d, v["speed"].AsNumber, 0.0001d);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void BilimselGosterim_VeNegatif_Okunur()
        {
            JsonValue v = JsonValue.Parse("{\"a\": -2.5, \"b\": 1e3, \"c\": 2.5E-2}");

            Assert.AreEqual(-2.5d, v["a"].AsNumber, 0.0001d);
            Assert.AreEqual(1000d, v["b"].AsNumber, 0.0001d);
            Assert.AreEqual(0.025d, v["c"].AsNumber, 0.0001d);
        }

        [Test]
        public void TamSayiAyrimi_KorunurTamSayiOlmayanTespitEdilir()
        {
            JsonValue v = JsonValue.Parse("{\"a\": 40, \"b\": 40.5}");

            Assert.IsTrue(v["a"].IsIntegral, "40 tam sayidir");
            Assert.IsFalse(v["b"].IsIntegral, "40.5 tam sayi degildir - sema 'integer' " +
                                              "isterse hata verilmeli");
        }

        [Test]
        public void Dizi_Okunur()
        {
            JsonValue v = JsonValue.Parse("{\"required\": [\"a\", \"b\"]}");

            Assert.AreEqual(2, v["required"].Items.Count);
            Assert.AreEqual("a", v["required"].Items[0].AsString);
        }

        [Test]
        public void MetinKacislari_Cozulur()
        {
            JsonValue v = JsonValue.Parse("{\"s\": \"a\\\"b\\\\c\\nd\"}");

            Assert.AreEqual("a\"b\\c\nd", v["s"].AsString);
        }

        [Test]
        public void TurkceKarakterler_Bozulmaz()
        {
            JsonValue v = JsonValue.Parse("{\"s\": \"ığüşöçİĞÜŞÖÇ\"}");

            Assert.AreEqual("ığüşöçİĞÜŞÖÇ", v["s"].AsString);
        }

        [Test]
        public void BosNesneVeDizi_Kabul()
        {
            Assert.AreEqual(JsonKind.Object, JsonValue.Parse("{}").Kind);
            Assert.AreEqual(0, JsonValue.Parse("[]").Items.Count);
        }

        [Test]
        public void MantiksalVeNull_Okunur()
        {
            JsonValue v = JsonValue.Parse("{\"t\": true, \"f\": false, \"n\": null}");

            Assert.IsTrue(v["t"].AsBool);
            Assert.IsFalse(v["f"].AsBool);
            Assert.AreEqual(JsonKind.Null, v["n"].Kind);
        }

        // ---------------------------------------------------------------- bozuk girdi

        [Test]
        public void KapanmamisNesne_HataVerir_SessizceYutulmaz()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\": 1"));
        }

        [Test]
        public void SondaFazladanIcerik_HataVerir()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\": 1} fazlalik"));
        }

        [Test]
        public void EksikIkiNokta_HataVerir()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\" 1}"));
        }

        [Test]
        public void YanlisTipOkumasi_HataVerir()
        {
            JsonValue v = JsonValue.Parse("{\"a\": \"metin\"}");

            Assert.Throws<InvalidOperationException>(() => { double _ = v["a"].AsNumber; });
        }
    }
}
