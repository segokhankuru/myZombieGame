using Steamworks;
using System;
using System.Collections;
using UnityEngine;

namespace Mirror.FizzySteam
{
    public abstract class LegacyCommon
    {
        private P2PSend[] channels;
        private int internal_ch => channels.Length;

        protected enum InternalMessages : byte
        {
            CONNECT,
            ACCEPT_CONNECT,
            DISCONNECT
        }

        protected readonly FizzyFacepunch transport;

        protected LegacyCommon(FizzyFacepunch transport)
        {
            channels = transport.Channels;

            SteamNetworking.OnP2PSessionRequest += OnNewConnection;
            SteamNetworking.OnP2PConnectionFailed += OnConnectFail;

            this.transport = transport;
        }

        protected void WaitForClose(SteamId cSteamID) => transport.StartCoroutine(DelayedClose(cSteamID));
        private IEnumerator DelayedClose(SteamId cSteamID)
        {
            yield return null;
            CloseP2PSessionWithUser(cSteamID);
        }

        protected void Dispose()
        {
            SteamNetworking.OnP2PSessionRequest -= OnNewConnection;
            SteamNetworking.OnP2PConnectionFailed -= OnConnectFail;
        }

        protected abstract void OnNewConnection(SteamId steamID);

        private void OnConnectFail(SteamId id, P2PSessionError err)
        {
            OnConnectionFailed(id);
            CloseP2PSessionWithUser(id);

            // BUNKER YAMASI (2026-09-05, ADR-0007): Facepunch 2.5.2'de iki uye SILINDI.
            // Valve, P2PSessionError.NotRunningApp ve DestinationNotLoggedIn'i
            // kullanimdan kaldirdi; Facepunch onlari '..._DELETED' diye yeniden
            // adlandirdi. Silinmis bir uyeyi adiyla yakalamak, paketin bir sonraki
            // surumunde tekrar kirilacak bir satir birakirdi - o yuzden ikisi de
            // 'default' dalina birakildi ve mesaji orasi veriyor.
            switch (err)
            {
                case P2PSessionError.NoRightsToApp:
                    throw new Exception("Connection failed: The local user doesn't own the app that is running.");
                case P2PSessionError.Timeout:
                    throw new Exception("Connection failed: The connection timed out because the target user didn't respond.");
                default:
                    // Mesaj ZENGINLESTIRILDI (BUNKER YAMASI): silinen iki uye en sik
                    // gorulen iki sebepti. "Unknown error" demek, teshisi tamamen
                    // kaybetmek olurdu.
                    throw new Exception(
                        $"Connection failed ({err}). Muhtemel sebepler: karsi taraf ayni " +
                        "oyunu calistirmiyor, Steam'e giris yapmamis, ya da farkli bir " +
                        "App ID ile calisiyor.");
            }
        }

        protected bool SendInternal(SteamId target, InternalMessages type) => SteamNetworking.SendP2PPacket(target, new byte[] { (byte)type }, 1, internal_ch);
        protected void Send(SteamId host, byte[] msgBuffer, int channel) => SteamNetworking.SendP2PPacket(host, msgBuffer, msgBuffer.Length, channel, channels[Mathf.Min(channel, channels.Length - 1)]);
        private bool Receive(out SteamId clientSteamID, out byte[] receiveBuffer, int channel)
        {
            if (SteamNetworking.IsP2PPacketAvailable(channel))
            {
                var data = SteamNetworking.ReadP2PPacket(channel);

                if (data != null)
                {
                    receiveBuffer = data.Value.Data;
                    clientSteamID = data.Value.SteamId;
                    return true;
                }
            }

            receiveBuffer = null;
            clientSteamID = 0;
            return false;
        }

        protected void CloseP2PSessionWithUser(SteamId clientSteamID) => SteamNetworking.CloseP2PSessionWithUser(clientSteamID);

        public void ReceiveData()
        {
            try
            {
                while (transport.enabled && Receive(out SteamId clientSteamID, out byte[] internalMessage, internal_ch))
                {
                    if (internalMessage.Length == 1)
                    {
                        OnReceiveInternalData((InternalMessages)internalMessage[0], clientSteamID);
                        return;
                    }
                    else
                    {
                        Debug.Log("Incorrect package length on internal channel.");
                    }
                }

                for (int chNum = 0; chNum < channels.Length; chNum++)
                {
                    while (transport.enabled && Receive(out SteamId clientSteamID, out byte[] receiveBuffer, chNum))
                    {
                        OnReceiveData(receiveBuffer, clientSteamID, chNum);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected abstract void OnReceiveInternalData(InternalMessages type, SteamId clientSteamID);
        protected abstract void OnReceiveData(byte[] data, SteamId clientSteamID, int channel);
        protected abstract void OnConnectionFailed(SteamId remoteId);
    }
}