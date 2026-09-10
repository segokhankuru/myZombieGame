using System.Collections.Generic;
using Bunker.Systems.Net;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// <see cref="SessionSignals"/> lobi listesi (2026-09-11).
    ///
    /// <para><b>Neden var:</b> <c>ServerStartGame</c> listeyi kendisiyle güncelliyordu ve
    /// oyun başladığı anda liste sessizce boşalıyordu. Hata mesajı yoktu; tek belirti
    /// co-op oturumunun "tek oyuncu" sayılması olacaktı.</para>
    /// </summary>
    public sealed class SessionSignalsTests
    {
        [TearDown]
        public void TearDown() => SessionSignals.Clear();

        [Test]
        public void LobiListesi_KendisiyleGuncellenince_Bosalmaz()
        {
            SessionSignals.SetLobbyState(true, true, new List<string> { "a", "b", "c" });

            SessionSignals.SetLobbyState(false, true, SessionSignals.LobbyPlayers);

            Assert.AreEqual(3, SessionSignals.LobbyPlayers.Count);
            Assert.IsFalse(SessionSignals.IsInLobby);
        }

        [Test]
        public void LobiListesi_BaskaListeyle_Degisir()
        {
            SessionSignals.SetLobbyState(true, true, new List<string> { "a", "b", "c" });

            SessionSignals.SetLobbyState(true, true, new List<string> { "x" });

            Assert.AreEqual(1, SessionSignals.LobbyPlayers.Count);
            Assert.AreEqual("x", SessionSignals.LobbyPlayers[0]);
        }

        [Test]
        public void LobiListesi_NullIle_Temizlenir()
        {
            SessionSignals.SetLobbyState(true, true, new List<string> { "a", "b" });

            SessionSignals.SetLobbyState(false, false, null);

            Assert.AreEqual(0, SessionSignals.LobbyPlayers.Count);
        }
    }
}
