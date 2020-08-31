#region Statements

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public abstract class SteamCommon
    {
        #region Variables

        private static readonly ILogger Logger = LogFactory.GetLogger(typeof(SteamCommon));

        private Callback<P2PSessionConnectFail_t> _connectionFailure;
        internal readonly ConcurrentQueue<Message> QueuedData = new ConcurrentQueue<Message>();
        protected SteamOptions Options;

        #endregion

        #region Class Specific

        protected SteamCommon(SteamOptions options)
        {
            Options = options;
            _connectionFailure = Callback<P2PSessionConnectFail_t>.Create(OnConnectionFailed);
        }

        public virtual void Disconnect()
        {
            _connectionFailure.Dispose();
            _connectionFailure = null;
        }

        /// <summary>
        ///     Connection request has failed to connect to user.
        /// </summary>
        /// <param name="result">The information back from steam.</param>
        protected virtual void OnConnectionFailed(P2PSessionConnectFail_t result)
        {
            //TODO Add messages back to clients in ui.

            switch (result.m_eP2PSessionError)
            {
                case 1:
                    if (Logger.logEnabled)
                        Logger.LogError(new Exception("SteamCommon connection failed: The target user is not running the same game."));
                    break;
                case 2:
                    if (Logger.logEnabled)
                        Logger.LogError(
                        new Exception("SteamCommon connection failed: The local user doesn't own the app that is running."));
                    break;
                case 3:
                    if (Logger.logEnabled)
                        Logger.LogError(new Exception("SteamCommon connection failed: Target user isn't connected to Steam."));
                    break;
                case 4:
                    if (Logger.logEnabled)
                        Logger.LogError(new Exception(
                        "SteamCommon connection failed: The connection timed out because the target user didn't respond."));
                    break;
                default:
                    if (Logger.logEnabled)
                        Logger.LogError(new Exception("SteamCommon connection failed: Unknown error."));
                    break;
            }
        }

        /// <summary>
        ///     Send an internal message through system.
        /// </summary>
        /// <param name="target">The steam person we are sending internal message to.</param>
        /// <param name="type">The type of <see cref="InternalMessages"/> we want to send.</param>
        internal abstract bool SteamSend(CSteamID target, InternalMessages type);

        /// <summary>
        ///     Process our internal messages away from mirror.
        /// </summary>
        /// <param name="type">The <see cref="InternalMessages"/> type message we received.</param>
        /// <param name="clientSteamId">The client id which the internal message came from.</param>
        protected abstract void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamId);

        /// <summary>
        ///     Process data incoming from steam backend.
        /// </summary>
        /// <param name="data">The data that has come in.</param>
        /// <param name="clientSteamId">The client the data came from.</param>
        /// <param name="channel">The channel the data was received on.</param>
        protected abstract void OnReceiveData(byte[] data, CSteamID clientSteamId, int channel);

        /// <summary>
        ///     Update method to be called by the transport.
        /// </summary>
        protected internal void Update()
        {
            while (DataAvailable(out CSteamID clientSteamId, out byte[] internalMessage, Options.Channels.Length))
            {
                if (internalMessage.Length != 1) continue;

                OnReceiveInternalData((InternalMessages)internalMessage[0], clientSteamId);

                return;
            }

            for (int chNum = 0; chNum < Options.Channels.Length; chNum++)
            {
                while (DataAvailable(out CSteamID clientSteamId, out byte[] receiveBuffer, chNum))
                {
                    OnReceiveData(receiveBuffer, clientSteamId, chNum);
                }
            }
        }

        /// <summary>
        ///     Check to see if we have received any data from steam users.
        /// </summary>
        /// <param name="clientSteamId">Returns back the steam id of users who sent message.</param>
        /// <param name="receiveBuffer">The data that was sent to use.</param>
        /// <param name="channel">The channel the data was sent on.</param>
        /// <returns></returns>
        private bool DataAvailable(out CSteamID clientSteamId, out byte[] receiveBuffer, int channel)
        {
            if (!SteamworksManager.Instance.Initialized)
            {
                receiveBuffer = null;
                clientSteamId = CSteamID.Nil;
                return false;
            }

            if (SteamNetworking.IsP2PPacketAvailable(out var packetSize, channel))
            {
                receiveBuffer = new byte[packetSize];
                return SteamNetworking.ReadP2PPacket(receiveBuffer, packetSize, out _, out clientSteamId, channel);
            }

            receiveBuffer = null;
            clientSteamId = CSteamID.Nil;
            return false;
        }

        #endregion
    }
}