#region Using Statements

using System;
using System.Collections;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public abstract class Common
    {
        protected readonly FizzySteamyMirror transport;
        private Callback<P2PSessionConnectFail_t> callback_OnConnectFail = null;
        private Callback<P2PSessionRequest_t> callback_OnNewConnection = null;
        private EP2PSend[] channels;
        private int internal_ch => channels.Length;

        protected Common(FizzySteamyMirror transport)
        {
            channels = transport.Channels;

            callback_OnNewConnection = Callback<P2PSessionRequest_t>.Create(OnNewConnection);
            callback_OnConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnConnectFail);

            this.transport = transport;
        }

        protected IEnumerator WaitDisconnect(CSteamID steamID)
        {
            yield return new WaitForSeconds(0.1f);
            CloseP2PSessionWithUser(steamID);
        }

        protected void Dispose()
        {
            if (callback_OnNewConnection != null)
            {
                callback_OnNewConnection.Dispose();
                callback_OnNewConnection = null;
            }

            if (callback_OnConnectFail != null)
            {
                callback_OnConnectFail.Dispose();
                callback_OnConnectFail = null;
            }
        }
        protected abstract void OnNewConnection(P2PSessionRequest_t result);

        private void OnConnectFail(P2PSessionConnectFail_t result)
        {
            switch (result.m_eP2PSessionError)
            {
                case 1:
                    Debug.LogError(new Exception("Connection failed: The target user is not running the same game."));
                    break;
                case 2:
                    Debug.LogError(
                        new Exception("Connection failed: The local user doesn't own the app that is running."));
                    break;
                case 3:
                    Debug.LogError(new Exception("Connection failed: Target user isn't connected to Steam."));
                    break;
                case 4:
                    Debug.LogError(new Exception(
                        "Connection failed: The connection timed out because the target user didn't respond."));
                    break;
                default:
                    Debug.LogError(new Exception("Connection failed: Unknown error."));
                    break;
            }
        }

        protected void SendInternal(CSteamID target, InternalMessages type) =>
            SteamNetworking.SendP2PPacket(target, new[] {(byte) type}, 1, EP2PSend.k_EP2PSendReliable, internal_ch);

        protected bool Send(CSteamID host, byte[] msgBuffer, int channel) =>
            SteamNetworking.SendP2PPacket(host, msgBuffer, (uint) msgBuffer.Length, channels[channel], channel);

        private bool Receive(out CSteamID clientSteamID, out byte[] receiveBuffer, int channel)
        {
            if (SteamNetworking.IsP2PPacketAvailable(out uint packetSize, channel))
            {
                receiveBuffer = new byte[packetSize];
                return SteamNetworking.ReadP2PPacket(receiveBuffer, packetSize, out _, out clientSteamID, channel);
            }

            receiveBuffer = null;
            clientSteamID = CSteamID.Nil;
            return false;
        }

        protected void CloseP2PSessionWithUser(CSteamID clientSteamID) =>
            SteamNetworking.CloseP2PSessionWithUser(clientSteamID);

        public void ReceiveData()
        {
            try
            {
                while (Receive(out CSteamID clientSteamID, out byte[] internalMessage, internal_ch))
                    if (internalMessage.Length == 1)
                        OnReceiveInternalData((InternalMessages)internalMessage[0], clientSteamID);
                    else
                        Debug.Log("Incorrect package length on internal channel.");

                for (int chNum = 0; chNum < channels.Length; chNum++)
                    while (Receive(out CSteamID clientSteamID, out byte[] receiveBuffer, chNum))
                        OnReceiveData(receiveBuffer, clientSteamID, chNum);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected abstract void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID);
        protected abstract void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel);
        protected abstract void OnConnectionFailed(CSteamID remoteId);

        protected enum InternalMessages : byte
        {
            CONNECT,
            ACCEPT_CONNECT,
            DISCONNECT
        }
    }
}
        {
            if (callback_OnNewConnection != null)
            {
                callback_OnNewConnection.Dispose();
                callback_OnNewConnection = null;
            }

            if (callback_OnConnectFail != null)
            {
                callback_OnConnectFail.Dispose();
                callback_OnConnectFail = null;
            }
        }

        protected abstract void OnNewConnection(P2PSessionRequest_t result);

        protected virtual void OnConnectFail(P2PSessionConnectFail_t result) => throw new Exception("Connection failed.");

        protected void SendInternal(CSteamID host, byte[] msgBuffer) => SteamNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, EP2PSend.k_EP2PSendReliable, SEND_INTERNAL);

        private bool ReceiveInternal(out uint readPacketSize, out CSteamID clientSteamID) => SteamNetworking.ReadP2PPacket(receiveBufferInternal, 1, out readPacketSize, out clientSteamID, SEND_INTERNAL);

        protected bool Send(CSteamID host, byte[] msgBuffer, int channel) => SteamNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, channels[channel], channel);

        private bool Receive(out uint readPacketSize, out CSteamID clientSteamID, out byte[] receiveBuffer, int channel)
        {
            uint packetSize;
            if (SteamNetworking.IsP2PPacketAvailable(out packetSize, channel) && packetSize > 0)
            {
                receiveBuffer = new byte[packetSize];
                return SteamNetworking.ReadP2PPacket(receiveBuffer, packetSize, out readPacketSize, out clientSteamID, channel);
            }

            receiveBuffer = null;
            readPacketSize = 0;
            clientSteamID = CSteamID.Nil;
            return false;
        }

        protected void CloseP2PSessionWithUser(CSteamID clientSteamID) => SteamNetworking.CloseP2PSessionWithUser(clientSteamID);

        public void ReceiveInternal()
        {
            try
            {
                while (ReceiveInternal(out uint readPacketSize, out CSteamID clientSteamID))
                {
                    if (readPacketSize == 1)
                    {
                        OnReceiveInternalData((InternalMessages)receiveBufferInternal[0], clientSteamID);
                    }
                    else
                    {
                        Debug.Log("Incorrect package length on internal channel.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void ReceiveData()
        {
            try
            {
                byte[] receiveBuffer;
                for (int chNum = 0; chNum < channels.Length; chNum++)
                {
                    while (Receive(out uint readPacketSize, out CSteamID clientSteamID, out receiveBuffer, chNum))
                    {
                        if (readPacketSize > 0)
                        {
                            OnReceiveData(receiveBuffer, clientSteamID, chNum);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected abstract void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID);
        protected abstract void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel);
    }
}
