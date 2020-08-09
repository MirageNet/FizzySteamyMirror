#region Statements

using System;
using Mirror.FizzySteam;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public abstract class SteamCommon
    {
        internal Callback<P2PSessionRequest_t> ConnectionListener;
        internal Callback<P2PSessionConnectFail_t> ConnectionFailure = null;

        protected SteamCommon()
        {
            ConnectionListener = Callback<P2PSessionRequest_t>.Create(AcceptConnection);
            ConnectionFailure = Callback<P2PSessionConnectFail_t>.Create(ConnectionFailed);
        }

        public virtual void Disconnect()
        {
            ConnectionListener?.Dispose();
            ConnectionListener = null;

            ConnectionFailure.Dispose();
            ConnectionFailure = null;
        }

        /// <summary>
        ///     Connection request has failed to connect to user.
        /// </summary>
        /// <param name="result">The information back from steam.</param>
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

        /// <summary>
        ///     Accept connection request from steam user.
        /// </summary>
        /// <param name="result">The information coming back from steam.</param>
        protected abstract void AcceptConnection(P2PSessionRequest_t result);

        /// <summary>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="type"></param>
        internal abstract bool SteamSend(CSteamID target, InternalMessages type);

        /// <summary>
        ///     Check to see if we have received any data from steam users.
        /// </summary>
        /// <param name="clientSteamID">Returns back the steam id of users who sent message.</param>
        /// <param name="receiveBuffer">The data that was sent to use.</param>
        /// <param name="channel">The channel the data was sent on.</param>
        /// <returns></returns>
        internal bool DataReceivedCheck(out CSteamID clientSteamID, out byte[] receiveBuffer, int channel)
        {
            if (!SteamworksManager.Instance.Initialized)
            {
                receiveBuffer = null;
                clientSteamID = CSteamID.Nil;
                return false;
            }

            if (SteamNetworking.IsP2PPacketAvailable(out var packetSize, channel))
            {
                receiveBuffer = new byte[packetSize];
                return SteamNetworking.ReadP2PPacket(receiveBuffer, packetSize, out _, out clientSteamID, channel);
            }

            receiveBuffer = null;
            clientSteamID = CSteamID.Nil;
            return false;
        }
    }
}