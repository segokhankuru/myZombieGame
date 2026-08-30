using System;
using Bunker.Systems.Economy;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// `PlayerWallet` için EditMode testleri (M1-02).
    ///
    /// En önemlisi <see cref="Harcama_SkoruAzaltmaz"/> — ekonominin tasarım kararının
    /// koruyucusu. Biri iki sayıyı tek sayıya indirmeye kalkarsa o test kırılır ve
    /// sebebi orada yazılıdır.
    /// </summary>
    public sealed class PlayerWalletTests
    {
        private static PlayerWallet NewWallet(int starting = 0)
            => new PlayerWallet(new EconomyConfig(), starting);

        // ---------------------------------------------------------------- kazanma

        [Test]
        public void Odul_HemBakiyeyiHemToplamiArtirir()
        {
            var wallet = NewWallet();

            int gained = wallet.Award(PointEvent.BodyKill);

            Assert.AreEqual(60, gained);
            Assert.AreEqual(60, wallet.SpendablePoints);
            Assert.AreEqual(60, wallet.TotalEarned);
        }

        [Test]
        public void OlayTurleri_YapilandirilanDegerleriVerir()
        {
            var config = new EconomyConfig(
                hit: 10, bodyKill: 60, headshotKill: 100,
                meleeKill: 130, barricadeBoardRepair: 10);
            var wallet = new PlayerWallet(config);

            Assert.AreEqual(10, wallet.Award(PointEvent.Hit));
            Assert.AreEqual(60, wallet.Award(PointEvent.BodyKill));
            Assert.AreEqual(100, wallet.Award(PointEvent.HeadshotKill));
            Assert.AreEqual(130, wallet.Award(PointEvent.MeleeKill));
            Assert.AreEqual(10, wallet.Award(PointEvent.BarricadeBoardRepair));
            Assert.AreEqual(310, wallet.TotalEarned);
        }

        [Test]
        public void BicakOldurmesi_EnYuksekOdulOlmali()
        {
            // Bu bir denge iddiasi degil, tasarim niyetinin korumasi: bicak en riskli
            // ve mermi harcamayan yontem. Ucuzlarsa erken tur ekonomisi coker.
            var config = new EconomyConfig();

            Assert.Greater(config.MeleeKill, config.HeadshotKill);
            Assert.Greater(config.HeadshotKill, config.BodyKill);
            Assert.Greater(config.BodyKill, config.Hit);
        }

        [Test]
        public void SifirVeNegatifTekrar_HicbirSeyYapmaz()
        {
            var wallet = NewWallet(100);

            Assert.AreEqual(0, wallet.Award(PointEvent.BodyKill, 0));
            Assert.AreEqual(0, wallet.Award(PointEvent.BodyKill, -3));
            Assert.AreEqual(100, wallet.SpendablePoints);
            Assert.AreEqual(100, wallet.TotalEarned);
        }

        [Test]
        public void CokluTekrar_Carpilir()
        {
            var wallet = NewWallet();

            int gained = wallet.Award(PointEvent.BarricadeBoardRepair, 6);

            Assert.AreEqual(60, gained);
            Assert.AreEqual(60, wallet.SpendablePoints);
        }

        // ---------------------------------------------------------------- harcama

        [Test]
        public void Harcama_SkoruAzaltmaz()
        {
            // EKONOMININ TASARIM KARARI.
            // Tek sayi kullanilsaydi kapi acan oyuncu skor kaybederdi ve hicbir sey
            // satin almayan oyuncu tabloda birinci olurdu. Bu SYS-01'i ve PILLAR-02'yi
            // cigner. Bu test o karari korur.
            var wallet = NewWallet();
            wallet.Award(PointEvent.BodyKill, 20);   // 1200 puan

            wallet.TryPurchase(750);

            Assert.AreEqual(450, wallet.SpendablePoints, "Bakiye harcamayla azalir.");
            Assert.AreEqual(1200, wallet.TotalEarned, "Skor harcamayla AZALMAZ.");
        }

        [Test]
        public void YetersizPuan_DurumuDegistirmez()
        {
            var wallet = NewWallet(100);

            PurchaseResult result = wallet.TryPurchase(750);

            Assert.AreEqual(PurchaseResult.InsufficientPoints, result);
            Assert.AreEqual(100, wallet.SpendablePoints, "Basarisiz alim kismi harcama yapmaz.");
        }

        [Test]
        public void TamPuanlaAlim_Basarili()
        {
            var wallet = NewWallet(750);

            Assert.AreEqual(PurchaseResult.Success, wallet.TryPurchase(750));
            Assert.AreEqual(0, wallet.SpendablePoints);
        }

        [Test]
        public void SifirVeNegatifFiyat_GecersizSayilir()
        {
            var wallet = NewWallet(1000);

            Assert.AreEqual(PurchaseResult.InvalidCost, wallet.TryPurchase(0));
            Assert.AreEqual(PurchaseResult.InvalidCost, wallet.TryPurchase(-500));
            Assert.AreEqual(1000, wallet.SpendablePoints);
        }

        // ---------------------------------------------------------------- dusme cezasi

        [Test]
        public void DusmeCezasi_BakiyeyiYariyaIndirir_SkoruBirakmaz()
        {
            var wallet = new PlayerWallet(new EconomyConfig(downedSpendableFraction: 0.5f));
            wallet.Award(PointEvent.BodyKill, 10);   // 600

            int lost = wallet.ApplyDownedPenalty();

            Assert.AreEqual(300, lost);
            Assert.AreEqual(300, wallet.SpendablePoints);
            Assert.AreEqual(600, wallet.TotalEarned, "Dusmek skoru silmez.");
        }

        [Test]
        public void DusmeCezasi_SifirBakiyede_HicbirSeyYapmaz()
        {
            var wallet = NewWallet();

            Assert.AreEqual(0, wallet.ApplyDownedPenalty());
            Assert.AreEqual(0, wallet.SpendablePoints);
        }

        [Test]
        public void DusmeCezasi_AsagiYuvarlar()
        {
            var wallet = new PlayerWallet(new EconomyConfig(downedSpendableFraction: 0.5f), 101);

            int lost = wallet.ApplyDownedPenalty();

            Assert.AreEqual(50, lost, "Yuvarlama oyuncunun lehine olmali.");
            Assert.AreEqual(51, wallet.SpendablePoints);
        }

        [Test]
        public void DusmeCezasi_SifirOran_HicbirSeyAlmaz()
        {
            var wallet = new PlayerWallet(new EconomyConfig(downedSpendableFraction: 0f), 500);

            Assert.AreEqual(0, wallet.ApplyDownedPenalty());
            Assert.AreEqual(500, wallet.SpendablePoints);
        }

        [Test]
        public void DusmeCezasi_TekrarUygulanabilir()
        {
            var wallet = new PlayerWallet(new EconomyConfig(downedSpendableFraction: 0.5f), 800);

            wallet.ApplyDownedPenalty();   // 400
            wallet.ApplyDownedPenalty();   // 200

            Assert.AreEqual(200, wallet.SpendablePoints);
            Assert.AreEqual(800, wallet.TotalEarned);
        }

        // ---------------------------------------------------------------- kenar durumlar

        [Test]
        public void NegatifBaslangicPuani_Reddedilir()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerWallet(new EconomyConfig(), -1));
        }

        [Test]
        public void NullConfig_Reddedilir()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerWallet(null));
        }

        [Test]
        public void ToplamKazanc_TasmazNegatifeDonmez()
        {
            // Uzun bir run'da sessizce negatife donen bir skor, bulunmasi en zor
            // hata turudur. Tavan davranisi acikca test ediliyor.
            var wallet = new PlayerWallet(new EconomyConfig(bodyKill: int.MaxValue / 2));

            wallet.Award(PointEvent.BodyKill);
            wallet.Award(PointEvent.BodyKill);
            wallet.Award(PointEvent.BodyKill);

            Assert.AreEqual(int.MaxValue, wallet.TotalEarned);
            Assert.Greater(wallet.SpendablePoints, 0, "Bakiye negatife donmemeli.");
        }

        [Test]
        public void YeniRun_HerSeyiSifirlar()
        {
            var wallet = NewWallet();
            wallet.Award(PointEvent.BodyKill, 10);
            wallet.TryPurchase(300);

            wallet.ResetForNewRun();

            Assert.AreEqual(0, wallet.SpendablePoints);
            Assert.AreEqual(0, wallet.TotalEarned);
        }
    }
}
