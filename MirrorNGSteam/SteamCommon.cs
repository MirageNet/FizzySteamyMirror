using System;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace OMN.Scripts.Networking.MirrorNGSteam
{
    public enum InternalMessages : byte
    {
        Connect,
        ConnectionAccepted,
        Disconnect
    }

    public abstract class SteamCommon
    {
        #region Variables

        public bool Connected;
        protected Callback<P2PSessionConnectFail_t> ConnectionFailure = null;
        private EP2PSend[] _channels;

        protected SteamCommon(MirrorNGSteamTransport transport)
        {
            _channels = transport.Channels;
        }

        #endregion

        #region Class Specific

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="clientSteamID"></param>
        protected abstract void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="clientSteamID"></param>
        /// <param name="channel"></param>
        protected abstract void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <param name="type"></param>
        protected void Send(CSteamID target, InternalMessages type) =>
            SteamNetworking.SendP2PPacket(target, new[] {(byte) type}, 1, EP2PSend.k_EP2PSendReliable, 0);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="msgBuffer"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        protected bool Send(CSteamID host, byte[] msgBuffer, int channel) =>
            SteamNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, _channels[channel], channel);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientSteamID"></param>
        /// <param name="receiveBuffer"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task ReceiveData()
        {
            try
            {
                while (Receive(out CSteamID clientSteamID, out byte[] internalMessage, 0))
                    if (internalMessage.Length == 1)
                        OnReceiveInternalData((InternalMessages)internalMessage[0], clientSteamID);
                    else
                        Debug.Log("Incorrect package length on internal channel.");

                for (int chNum = 0; chNum < _channels.Length; chNum++)
                    while (Receive(out var clientSteamID, out var receiveBuffer, chNum))
                        OnReceiveData(receiveBuffer, clientSteamID, chNum);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async void Update()
        {
            await ReceiveData();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        protected virtual void ConnectionFailed(P2PSessionConnectFail_t result)
        {
            //TODO Add messages back to clients in ui.
#if UNITY_EDITOR
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
#endif
        }

        #endregion
    }
}
