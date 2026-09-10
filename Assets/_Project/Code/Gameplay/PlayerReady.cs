using System.Collections.Generic;
using Bunker.Audio;
using Bunker.Systems.Cards;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Tur arasında <b>hazır</b> işareti (F). Herkes hazır verirse mola biter
    /// (2026-09-09).
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"tur araları direkt 30 sn olsun ve
    /// herkes ready (F tuşu) verirse zaman direkt bitsin ve sonraki tur
    /// başlasın."</i> Sabit bir mola tek başına yanlış bir çözümdü: erken turlarda
    /// molada yapılacak iş yok ve 30 saniye boş bekleme olarak geçiyor; geç turlarda
    /// ise 30 saniye zar zor yetiyor. Hazır işareti bu ikisini <b>aynı sayıyla</b>
    /// çözüyor — 30 saniye bir <i>tavan</i> oluyor, bir süre değil.</para>
    ///
    /// <para><b>Herkes, bir kişi değil.</b> Tek oyuncunun molayı bitirebilmesi, co-op'u
    /// en sabırsız oyuncunun hızında oynatmak olurdu — hâlâ tezgâhta olan arkadaşı
    /// hazırlıksız bırakır. Bekleme süresi bir ceza değil, hazırlık için ayrılmış
    /// zaman.</para>
    ///
    /// <para><b>Yere düşmüş ve ölü oyuncular sayılmaz</b>: hazır veremeyecek durumda
    /// olan birini beklemek, molayı sonsuza kadar uzatırdı. Solo'da bu kural görünmez
    /// (tek oyuncu vardır ve ayaktadır), ama iki ayrı kural yazmıyoruz — "ayakta olan
    /// herkes" solo'yu da kapsıyor.</para>
    ///
    /// <para><b>Otorite sunucuda</b> (ADR-0004): tur akışını başlatan karar otoritenin.
    /// İstemci yalnızca "ben hazırım" der.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Ready")]
    public sealed class PlayerReady : NetworkBehaviour
    {
        private static readonly List<PlayerReady> All = new List<PlayerReady>(4);

        /// <summary>Oturumdaki bütün oyuncular. HUD sayacı bunu okur.</summary>
        public static IReadOnlyList<PlayerReady> Players => All;

        [SyncVar] private bool _ready;

        private PlayerDownState _down;

        /// <summary>Bu oyuncu hazır mı.</summary>
        public bool IsReady => _ready;

        /// <summary>Hazır verebilecek durumda mı (ayakta ve run sürüyor).</summary>
        public bool CanReady => _down == null || _down.IsAlive;

        private void Awake() => _down = GetComponent<PlayerDownState>();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Tur BASLAYINCA sifirlanir: bir sonraki molaya hazir girmek, molayi
            // hic yasamadan gecmek olurdu.
            RoundSignals.RoundStarted += OnRoundStarted;
            RunSignals.RunRestarted += OnRunRestarted;
        }

        public override void OnStopServer()
        {
            // OnStartServer'in kurdugunu OnStopServer bozar (csharp-code.md).
            RoundSignals.RoundStarted -= OnRoundStarted;
            RunSignals.RunRestarted -= OnRunRestarted;

            base.OnStopServer();
        }

        private void OnRoundStarted(int round) => _ready = false;

        private void OnRunRestarted() => _ready = false;

        private void Update()
        {
            if (!isLocalPlayer) return;
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen) return;

            // YALNIZCA MOLADA. Tur icinde F'ye basmak hicbir sey yapmamali - islevsiz
            // bir tus, oyuncuya "bozuk" diye okunur; o yuzden ipucu da yalnizca molada
            // gorunuyor (CombatHud).
            if (!RoundSignals.IsBreather) return;
            if (!CanReady) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.fKey.wasPressedThisFrame) CmdToggleReady();
        }

        /// <summary>
        /// Hazır işaretini <b>açar/kapatır</b>. Geri alınabilir olması şart: yanlışlıkla
        /// basan oyuncu, üç saniye sonra başlayan bir tura mahkûm olmamalı.
        /// </summary>
        [Command]
        private void CmdToggleReady()
        {
            if (!RoundSignals.IsBreather) return;
            if (!CanReady) return;

            _ready = !_ready;

            TargetFeedback(connectionToClient);
            EvaluateAll();
        }

        [TargetRpc]
        private void TargetFeedback(NetworkConnection target)
        {
            // Basmanin karsiligi AYNI KAREDE duyulur: sunucunun kararini beklemek,
            // tusun calismadigi gibi okunur (audio-code.md).
            GameAudio.Play(SfxId.Purchase, 0.6f);
        }

        /// <summary>
        /// <b>Ayakta olan herkes hazır mı</b> — öyleyse mola biter.
        ///
        /// <para><b>Neden statik ve her hazır işaretinde çalışıyor:</b> kare başına
        /// yoklamak, dört elemanlı bir listeyi saniyede altmış kez taramak olurdu.
        /// Cevabın değişebileceği tek an, birinin işaretini değiştirdiği andır.</para>
        /// </summary>
        [Server]
        private static void EvaluateAll()
        {
            int eligible = 0;

            for (int i = 0; i < All.Count; i++)
            {
                PlayerReady player = All[i];
                if (player == null || !player.CanReady) continue;

                eligible++;
                if (!player._ready) return;
            }

            // Ayakta kimse yoksa mola bitirilmez: herkesin yerde oldugu bir molada
            // turu baslatmak, kimsenin oynayamadigi bir tur acardi.
            if (eligible == 0) return;

            RoundSignals.RaiseBreatherSkip();
        }
    }
}
