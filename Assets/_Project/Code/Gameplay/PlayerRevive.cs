using Bunker.Systems.Cards;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Yere düşen takım arkadaşını <b>E'yi basılı tutarak kaldırma</b>. 2026-09-07.
    ///
    /// <para><b>Neden ışın değil, yakınlık:</b> barikat tamiri ve satın alma bir
    /// <i>ışına</i> bakıyor, çünkü ikisi de duvarda duran şeyler ve hangisine
    /// baktığın belirsiz olabilir. Yerde yatan bir oyuncu ise yerde: sürünün ortasında
    /// ona tam nişan almak zorunda kalmak, kaldırmayı bir nişan alma problemine
    /// çevirirdi. Yakınlık ölçüsü doğru cevap — <b>yanına git ve tut</b>.</para>
    ///
    /// <para><b>Mesafeyi SUNUCU ölçer</b> (netcode.md: her RPC bir güven sınırıdır).
    /// İstemci yalnızca "kaldırmak istiyorum" der; kimi, ne kadar yakından ve gerçekten
    /// ayakta mıyken sorularının hepsini <see cref="PlayerDownState.ServerTickRevive"/>
    /// cevaplıyor. İstemcinin söylediği mesafeye güvenmek, haritanın öbür ucundan
    /// diriltmek demek olurdu.</para>
    ///
    /// <para><b>Aynı tuş, tanımlı çakışma:</b> E zaten satın alma ve tamir tuşu.
    /// Sıralama <i>en yakın olan kazanır</i> değil, <b>kaldırma önce gelir</b>:
    /// arkadaşın yerde yatarken tezgâhtan alışveriş yapmak hiçbir durumda doğru
    /// niyet değil.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Revive")]
    public sealed class PlayerRevive : NetworkBehaviour
    {
        private PlayerDownState _self;

        /// <summary>Şu an kaldırılabilecek biri var mı (HUD ipucu bunu okur).</summary>
        public bool HasTarget { get; private set; }

        /// <summary>Hedefin kaldırılma ilerlemesi (0..1). HUD çubuğu bunu çizer.</summary>
        public float TargetProgress01 { get; private set; }

        private void Awake() => _self = GetComponent<PlayerDownState>();

        private void Update()
        {
            if (!isLocalPlayer)
            {
                HasTarget = false;
                return;
            }

            // Kendisi dusmusken kimseyi kaldiramaz; run bittiyse girdi kesilir.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen
                || _self == null || !_self.IsAlive)
            {
                HasTarget = false;
                return;
            }

            PlayerDownState target = PlayerDownState.NearestDowned(
                transform.position, PlayerDownState.ReviveRangeMeters);

            HasTarget = target != null;
            TargetProgress01 = target != null ? target.ReviveProgress01 : 0f;

            if (target == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.isPressed) return;

            CmdRevive(target.netIdentity, Time.deltaTime);
        }

        /// <summary>
        /// Kaldırma isteği. <b>Süreyi de istemci gönderiyor</b> — ve sunucu bunu
        /// sınırlıyor: sınırsız bir <c>deltaTime</c>, tek karede diriltme demek olurdu
        /// (netcode.md: bir istemcinin döngüde çağırabileceği her şey sınırlanır).
        /// </summary>
        [Command]
        private void CmdRevive(NetworkIdentity targetIdentity, float deltaTime)
        {
            if (targetIdentity == null) return;

            var target = targetIdentity.GetComponent<PlayerDownState>();
            if (target == null) return;

            // Bir karenin makul ust siniri. Daha buyugu ya lag ya hile;
            // ikisinde de dogru cevap kirpmak.
            float step = Mathf.Clamp(deltaTime, 0f, 0.1f);

            target.ServerTickRevive(_self, step);
        }
    }
}
