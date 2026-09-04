using System.Collections.Generic;
using Bunker.Systems.Cards;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Kart sisteminin çekirdeği (SYS-02). Buradaki asıl işler:
    /// <b>istiflenen kartların dengeyi patlatmaması</b> (§3.1, §3.2) ve
    /// <b>draft'ın üç ayrı seçenek göstermesi</b> — aynı kartı iki yuvada göstermek,
    /// üç seçeneği sessizce ikiye düşürür.
    /// </summary>
    public sealed class CardSystemTests
    {
        private static CardDefinition Card(string id, CardTag tag = CardTag.Ballistics,
                                           CardStat stat = CardStat.WeaponDamage,
                                           float value = 0.2f,
                                           bool solo = true, bool coop = true) =>
            new CardDefinition(id, id, string.Empty, tag, stat, value, solo, coop);

        private static List<CardDefinition> Pool(int count, CardTag tag = CardTag.Ballistics)
        {
            var list = new List<CardDefinition>(count);
            for (int i = 0; i < count; i++) list.Add(Card($"card.{tag}.{i}", tag));
            return list;
        }

        // ---------------------------------------------------------------- loadout

        [Test]
        public void AyniKart_IkiKezAlinamaz()
        {
            var loadout = new CardLoadout();

            Assert.IsTrue(loadout.Add(Card("card.a")));
            Assert.IsFalse(loadout.Add(Card("card.a")));
            Assert.AreEqual(1, loadout.Count);
        }

        [Test]
        public void Kartlar_TOPLANIR_carpilmaz()
        {
            var loadout = new CardLoadout();

            for (int i = 0; i < 5; i++)
            {
                loadout.Add(Card($"card.{i}", CardTag.Ballistics, CardStat.WeaponDamage, 0.20f));
            }

            // SYS-02 §3.1: carpimsal olsaydi 1.2^5 = 2.49x ederdi ve 20. turda denge
            // yok olurdu. Toplamsal 2.00x eder.
            Assert.AreEqual(2.00f, loadout.Multiplier(CardStat.WeaponDamage), 0.001f);
        }

        [Test]
        public void HasarAzaltma_SifiraUlasmaz()
        {
            var loadout = new CardLoadout();

            // §3.2: yuzdeyle dususte 5 kart x %20 = %100 = olumsuzluk. Efektif can
            // ile hicbir zaman sifira ulasmaz.
            for (int i = 0; i < 10; i++)
            {
                loadout.Add(Card($"card.{i}", CardTag.Blood, CardStat.EffectiveHealth, 0.25f));
            }

            float multiplier = loadout.DamageTakenMultiplier;

            Assert.Greater(multiplier, 0f, "Alinan hasar carpani asla sifir olamaz.");
            Assert.Less(multiplier, 0.4f, "On kart hasari belirgin dusurmeli.");
        }

        [Test]
        public void HasarAzaltma_DortKart_HasariYariyaIndirir()
        {
            var loadout = new CardLoadout();

            for (int i = 0; i < 4; i++)
            {
                loadout.Add(Card($"card.{i}", CardTag.Blood, CardStat.EffectiveHealth, 0.25f));
            }

            // 1 / (1 + 4*0.25) = 0.5
            Assert.AreEqual(0.5f, loadout.DamageTakenMultiplier, 0.001f);
        }

        [Test]
        public void EtiketBonusu_UcuncuKarttaAcilir()
        {
            var loadout = new CardLoadout();

            loadout.Add(Card("a", CardTag.Tempo));
            loadout.Add(Card("b", CardTag.Tempo));
            Assert.IsFalse(loadout.HasTagBonus(CardTag.Tempo));

            loadout.Add(Card("c", CardTag.Tempo));
            Assert.IsTrue(loadout.HasTagBonus(CardTag.Tempo));
        }

        [Test]
        public void TakimKartlari_EtiketBonusuSaymaz()
        {
            var loadout = new CardLoadout();

            for (int i = 0; i < 5; i++) loadout.Add(Card($"t{i}", CardTag.Team));

            Assert.IsFalse(loadout.HasTagBonus(CardTag.Team),
                           "Takim kartlari bir build kimligi degil, bir takim etkisi.");
        }

        [Test]
        public void Reset_YiginiVeSayaclariTemizler()
        {
            var loadout = new CardLoadout();
            loadout.Add(Card("a", CardTag.Tempo, CardStat.MoveSpeed, 0.2f));
            loadout.Add(Card("b", CardTag.Tempo, CardStat.MoveSpeed, 0.2f));

            loadout.Reset();

            Assert.AreEqual(0, loadout.Count);
            Assert.AreEqual(0, loadout.TagCount(CardTag.Tempo));
            Assert.AreEqual(1f, loadout.Multiplier(CardStat.MoveSpeed), 0.001f);
        }

        // ---------------------------------------------------------------- draft

        [Test]
        public void Draft_UcFARKLIKartGosterir()
        {
            var draft = new CardDraft(new CardPool(Pool(20), seed: 1));
            draft.Open(new CardLoadout(), solo: true);

            Assert.AreEqual(3, draft.SlotCount);
            Assert.AreNotEqual(draft.Slot(0).Id, draft.Slot(1).Id);
            Assert.AreNotEqual(draft.Slot(1).Id, draft.Slot(2).Id);
            Assert.AreNotEqual(draft.Slot(0).Id, draft.Slot(2).Id);
        }

        [Test]
        public void Draft_AyniTohumAyniUcluyuUretir()
        {
            // draft-ekrani.md: sonucu HOST belirler, animasyon sonuca oynar. Ayni
            // tohum ayni sonucu vermezse dort istemci farkli kart gorur.
            var a = new CardDraft(new CardPool(Pool(20), seed: 42));
            var b = new CardDraft(new CardPool(Pool(20), seed: 42));

            a.Open(new CardLoadout(), solo: true);
            b.Open(new CardLoadout(), solo: true);

            for (int i = 0; i < a.SlotCount; i++)
            {
                Assert.AreEqual(a.Slot(i).Id, b.Slot(i).Id);
            }
        }

        [Test]
        public void Draft_AlinmisKartTekrarCikmaz()
        {
            var loadout = new CardLoadout();
            var draft = new CardDraft(new CardPool(Pool(6), seed: 3));

            // Havuzu neredeyse tuket.
            for (int round = 0; round < 3; round++)
            {
                draft.Open(loadout, solo: true);
                draft.Pick(0, loadout);
            }

            draft.Open(loadout, solo: true);

            for (int i = 0; i < draft.SlotCount; i++)
            {
                if (!draft.Slot(i).IsValid) continue;
                Assert.IsFalse(loadout.Has(draft.Slot(i).Id) && draft.Slot(i).Id != draft.Slot(i).Id);
            }

            // Elde uc kart var; kalan uc karttan hicbiri elde olmamali.
            Assert.AreEqual(3, loadout.Count);
        }

        [Test]
        public void Draft_HavuzTukenirse_GecersizYuva()
        {
            var loadout = new CardLoadout();
            var draft = new CardDraft(new CardPool(Pool(2), seed: 5));

            draft.Open(loadout, solo: true);

            // Iki kartlik havuzda ucuncu yuva DOLDURULAMAZ. Sessizce tekrar eden bir
            // kart vermek, uc secenek varmis gibi gosterip ikisini ayni yapardi.
            Assert.IsFalse(draft.Slot(2).IsValid);
        }

        [Test]
        public void SoloModda_CoopKartiCikmaz()
        {
            var cards = new List<CardDefinition>
            {
                Card("solo.only", CardTag.Blood, CardStat.MaxHealth, 0.1f, solo: true, coop: true),
                Card("coop.only", CardTag.Team, CardStat.MaxHealth, 0.1f, solo: false, coop: true)
            };

            var draft = new CardDraft(new CardPool(cards, seed: 7));
            draft.Open(new CardLoadout(), solo: true);

            for (int i = 0; i < draft.SlotCount; i++)
            {
                Assert.AreNotEqual("coop.only", draft.Slot(i).Id,
                                   "Solo'da takim karti cikmamali (SYS-02 §6).");
            }
        }

        // ---------------------------------------------------------------- yenileme

        [Test]
        public void Yenileme_YuvaBasinaBirUcretsizBirPuanli()
        {
            var loadout = new CardLoadout();
            var draft = new CardDraft(new CardPool(Pool(30), seed: 11));
            draft.Open(loadout, solo: true);

            Assert.AreEqual(RerollState.FreeAvailable, draft.Reroll(0));
            Assert.IsFalse(draft.RerollCostsPoints(0));

            Assert.IsTrue(draft.RerollSlot(0, loadout, true));
            Assert.AreEqual(RerollState.PaidAvailable, draft.Reroll(0));
            Assert.IsTrue(draft.RerollCostsPoints(0), "Ikinci yenileme puana mal olur.");

            Assert.IsTrue(draft.RerollSlot(0, loadout, true));
            Assert.AreEqual(RerollState.Exhausted, draft.Reroll(0));

            Assert.IsFalse(draft.RerollSlot(0, loadout, true), "Ucuncu yenileme yok.");
        }

        [Test]
        public void Yenileme_YuvalarBirbirindenBAGIMSIZ()
        {
            var loadout = new CardLoadout();
            var draft = new CardDraft(new CardPool(Pool(30), seed: 13));
            draft.Open(loadout, solo: true);

            draft.RerollSlot(1, loadout, true);

            // "Ucu de kotu" ile "ikisi iyi biri kotu" farkli durumlar. Hepsini birden
            // yenilemek, begendigin kartlari da atmaya zorlardi.
            Assert.AreEqual(RerollState.FreeAvailable, draft.Reroll(0));
            Assert.AreEqual(RerollState.PaidAvailable, draft.Reroll(1));
            Assert.AreEqual(RerollState.FreeAvailable, draft.Reroll(2));
        }

        [Test]
        public void Yenileme_HerDraftBasindaSIFIRLANIR()
        {
            var loadout = new CardLoadout();
            var draft = new CardDraft(new CardPool(Pool(30), seed: 17));

            draft.Open(loadout, solo: true);
            draft.RerollSlot(0, loadout, true);
            draft.RerollSlot(0, loadout, true);
            Assert.AreEqual(RerollState.Exhausted, draft.Reroll(0));

            draft.Pick(1, loadout);
            draft.Open(loadout, solo: true);

            // Gelistirici karari (2026-09-04): "her tur bu ozellik sifirlanir".
            Assert.AreEqual(RerollState.FreeAvailable, draft.Reroll(0));
        }

        [Test]
        public void Secim_DrafiKapatir_VeIkinciSecimiReddeder()
        {
            var loadout = new CardLoadout();
            var draft = new CardDraft(new CardPool(Pool(30), seed: 19));
            draft.Open(loadout, solo: true);

            Assert.IsTrue(draft.Pick(0, loadout));
            Assert.IsFalse(draft.IsOpen);
            Assert.IsFalse(draft.Pick(1, loadout), "Kapali draft'tan ikinci kart alinamaz.");
            Assert.AreEqual(1, loadout.Count);
        }

        [Test]
        public void EtiketAgirligi_AyniEtiketiDahaSikCikarir()
        {
            // GOAL-02'nin araci: oyuncular birbirinden uzaklasmali. Elde 2 Tempo
            // varken Tempo daha sik gelmeli.
            var cards = new List<CardDefinition>();
            for (int i = 0; i < 10; i++) cards.Add(Card($"tempo.{i}", CardTag.Tempo));
            for (int i = 0; i < 10; i++) cards.Add(Card($"ball.{i}", CardTag.Ballistics));

            var loadout = new CardLoadout();
            loadout.Add(Card("seed.a", CardTag.Tempo));
            loadout.Add(Card("seed.b", CardTag.Tempo));

            var pool = new CardPool(cards, seed: 23);
            int tempo = 0;

            for (int i = 0; i < 400; i++)
            {
                CardDefinition drawn = pool.Draw(loadout, solo: true, taken: null, excludeUpTo: 0);
                if (drawn.Tag == CardTag.Tempo) tempo++;
            }

            // Agirliksiz beklenti %50; iki Tempo karti agirligi 2x yapar -> ~%67.
            Assert.Greater(tempo, 220, $"Tempo agirligi calismiyor: 400 cekiliste {tempo}.");
        }
    }
}
