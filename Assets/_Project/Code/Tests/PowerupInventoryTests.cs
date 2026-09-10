using Bunker.Systems.Pickups;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Eşya cebinin kuralları (2026-09-09).
    ///
    /// <para><b>EditMode</b>: <see cref="PowerupInventory"/> saf C#, sahne gerektirmez.
    /// Yığılma, tavan ve tüketim kurallarının Unity açmadan doğrulanabilmesi, cebin
    /// ayrı bir sınıf olmasının başlıca sebebi.</para>
    /// </summary>
    public sealed class PowerupInventoryTests
    {
        [Test]
        public void BosCepteHicbirSeyYok()
        {
            var inventory = new PowerupInventory(5);

            Assert.IsTrue(inventory.IsEmpty);
            Assert.IsFalse(inventory.Has(PowerupKind.Health));
            Assert.AreEqual(0, inventory.Count(PowerupKind.Nuke));
        }

        [Test]
        public void ToplananEsyaCepteBirikir()
        {
            var inventory = new PowerupInventory(5);

            Assert.IsTrue(inventory.TryStore(PowerupKind.Health));
            Assert.IsTrue(inventory.TryStore(PowerupKind.Health));

            Assert.AreEqual(2, inventory.Count(PowerupKind.Health));
            Assert.IsFalse(inventory.IsEmpty);
        }

        [Test]
        public void TurlerAyriSlotlardaBirikir()
        {
            var inventory = new PowerupInventory(5);

            inventory.TryStore(PowerupKind.Health);
            inventory.TryStore(PowerupKind.Nuke);
            inventory.TryStore(PowerupKind.Nuke);

            Assert.AreEqual(1, inventory.Count(PowerupKind.Health));
            Assert.AreEqual(2, inventory.Count(PowerupKind.Nuke));
            Assert.AreEqual(0, inventory.Count(PowerupKind.Ammo));
        }

        /// <summary>
        /// <b>Tavan dolduğunda toplama BASARISIZ olur.</b> Sessizce yutmak, oyuncuya
        /// "topladım" deyip hiçbir şey vermemek olurdu — çağıran taraf eşyayı yerde
        /// bırakabilmek için bunu bilmek zorunda.
        /// </summary>
        [Test]
        public void TavanDolunca_ToplamaBasarisizOlur()
        {
            var inventory = new PowerupInventory(2);

            Assert.IsTrue(inventory.TryStore(PowerupKind.Freeze));
            Assert.IsTrue(inventory.TryStore(PowerupKind.Freeze));
            Assert.IsFalse(inventory.TryStore(PowerupKind.Freeze), "Tavan asilmamali.");

            Assert.AreEqual(2, inventory.Count(PowerupKind.Freeze));
            Assert.IsTrue(inventory.IsFull(PowerupKind.Freeze));
        }

        [Test]
        public void DolanSlot_DigerSlotuEtkilemez()
        {
            var inventory = new PowerupInventory(1);

            inventory.TryStore(PowerupKind.Nuke);

            Assert.IsFalse(inventory.TryStore(PowerupKind.Nuke));
            Assert.IsTrue(inventory.TryStore(PowerupKind.Health),
                          "Tavan slot BASINA, cep genelinde degil.");
        }

        [Test]
        public void KullanilanEsyaDusulur()
        {
            var inventory = new PowerupInventory(5);
            inventory.TryStore(PowerupKind.Ammo);
            inventory.TryStore(PowerupKind.Ammo);

            Assert.IsTrue(inventory.TryConsume(PowerupKind.Ammo));

            Assert.AreEqual(1, inventory.Count(PowerupKind.Ammo));
        }

        /// <summary>
        /// Boş slota basmak <c>false</c> döner — çağıran taraf hiçbir etki
        /// uygulamamalı. Aksi hâlde nuke sonsuz olurdu.
        /// </summary>
        [Test]
        public void BosSlotKullanilamaz()
        {
            var inventory = new PowerupInventory(5);

            Assert.IsFalse(inventory.TryConsume(PowerupKind.Slow));
            Assert.AreEqual(0, inventory.Count(PowerupKind.Slow));
        }

        [Test]
        public void TukettiktenSonraTekrarToplanabilir()
        {
            var inventory = new PowerupInventory(1);

            inventory.TryStore(PowerupKind.Health);
            Assert.IsFalse(inventory.TryStore(PowerupKind.Health));

            inventory.TryConsume(PowerupKind.Health);

            Assert.IsTrue(inventory.TryStore(PowerupKind.Health),
                          "Tuketilen slot yeniden dolabilmeli.");
        }

        /// <summary>
        /// Sıfır ya da negatif tavan sessizce "hiçbir şey toplanamaz" demek olurdu ve
        /// oyun testinde "droplar çalışmıyor" diye okunurdu — teşhisi en zor hata türü.
        /// </summary>
        [Test]
        public void GecersizTavan_EnAzBirEsyayaDuser()
        {
            var inventory = new PowerupInventory(0);

            Assert.AreEqual(1, inventory.CapacityPerSlot);
            Assert.IsTrue(inventory.TryStore(PowerupKind.Health));
        }

        [Test]
        public void YeniRun_CebiBosaltir()
        {
            var inventory = new PowerupInventory(5);
            inventory.TryStore(PowerupKind.Nuke);
            inventory.TryStore(PowerupKind.Health);

            inventory.Clear();

            Assert.IsTrue(inventory.IsEmpty,
                          "Ikinci run, birincinin nuke'uyla baslamamali.");
        }

        /// <summary>
        /// Slot sirasi <see cref="PowerupKind"/> sirasidir ve tuslar <b>4-5-6-7-8</b>
        /// (2026-09-09: atesli silah slotu ikiye inince 6'dan 4'e kaydi).
        ///
        /// <para><b>Bu test bir HIZALAMA testi:</b> silah slotu sayisi degistiginde
        /// esya tuslari da kaymali, yoksa bir tus hem silah degistirir hem esya
        /// harcar. Buradaki sabitler kirildiginda dusunulmesi gereken sey budur.</para>
        /// </summary>
        [Test]
        public void TusNumaralari_DortdenBaslar()
        {
            Assert.AreEqual(4, PowerupInventory.KeyNumberFor(0));
            Assert.AreEqual(5, PowerupInventory.KeyNumberFor(1));
            Assert.AreEqual(6, PowerupInventory.KeyNumberFor(2));
            Assert.AreEqual(7, PowerupInventory.KeyNumberFor(3));
            Assert.AreEqual(8, PowerupInventory.KeyNumberFor(4));
        }

        [Test]
        public void SlotSirasi_EsyaTuruSirasiylaAyni()
        {
            Assert.AreEqual(PowerupKind.Health, PowerupInventory.KindAt(0));
            Assert.AreEqual(PowerupKind.Ammo, PowerupInventory.KindAt(1));
            Assert.AreEqual(PowerupKind.Slow, PowerupInventory.KindAt(2));
            Assert.AreEqual(PowerupKind.Freeze, PowerupInventory.KindAt(3));
            Assert.AreEqual(PowerupKind.Nuke, PowerupInventory.KindAt(4));
        }

        /// <summary>
        /// Slot sayısı eşya türü sayısına <b>eşit olmak zorunda</b>: altıncı bir eşya
        /// eklendiği gün bu test, arayüzde ona yer olmadığını söyler.
        /// </summary>
        [Test]
        public void SlotSayisi_EsyaTuruSayisiylaAyni()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(PowerupKind)).Length,
                            PowerupInventory.SlotCount);
        }
    }
}
