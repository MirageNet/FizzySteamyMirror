#region Statements

using System;
using System.Collections.Concurrent;
using System.Threading;
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

        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        private Callback<P2PSessionConnectFail_t> _connectionFailure;
        internal readonly ConcurrentQueue<Message> QueuedData = new ConcurrentQueue<Message>();
        protected SteamOptions Options;
        public Action<ErrorCodes, string> Error;

        #endregion

        #region Class Specific

        public bool Connected => _cancellationToken.IsCancellationRequested != true;

        protected SteamCommon(SteamOptions options)
        {
            Options = options;
            _connectionFailure = Callback<P2PSessionConnectFail_t>.Create(OnConnectionFailed);
            _= Task.Run(Update);
        }

        public virtual void Disconnect()
        {
            _cancellationToken?.Cancel();
            _connectionFailure?.Dispose();
            _connectionFailure = null;
        }

        /// <summary>
        ///     Connection request has failed to connect to user.
        /// </summary>
        /// <param name="result">The information back from steam.</param>
        protected virtual void OnConnectionFailed(P2PSessionConnectFail_t result)
        {
            string errorMessage;

            switch (result.m_eP2PSessionError)
            {
                case 1:

                    errorMessage = "Connection failed: The target user is not running the same game.";

                    Error?.Invoke((ErrorCodes) result.m_eP2PSessionError, errorMessage);

                    if (Logger.logEnabled)
                        Logger.LogError(new Exception(errorMessage));
                    break;
                case 2:

                    errorMessage = "Connection failed: The local user doesn't own the app that is running.";

                    Error?.Invoke((ErrorCodes)result.m_eP2PSessionError, errorMessage);

                    if (Logger.logEnabled)
                        Logger.LogError(
                        new Exception(errorMessage));
                    break;
                case 3:

                    errorMessage = "Connection failed: The target user is not running the same game.";

                    Error?.Invoke((ErrorCodes)result.m_eP2PSessionError, errorMessage);

                    if (Logger.logEnabled)
                        Logger.LogError(new Exception(errorMessage));
                    break;
                case 4:

                    errorMessage = "Connection failed: The connection timed out because the target user didn't respond.";

                    Error?.Invoke((ErrorCodes)result.m_eP2PSessionError, errorMessage);

                    if (Logger.logEnabled)
                        Logger.LogError(new Exception(errorMessage));
                    break;
                default:

                    errorMessage = $"Connection failed: Unknown: {(EP2PSessionError) result.m_eP2PSessionError}";

                    Error?.Invoke((ErrorCodes)result.m_eP2PSessionError, errorMessage);

                    if (Logger.logEnabled)
                        Logger.LogError(new Exception(errorMessage));
                    break;
            }

            _cancellationToken.Cancel();
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
        private void Update()
        {
            while (Connected)
            {
                while (DataAvailable(out CSteamID clientSteamId, out byte[] internalMessage, Options.Channels.Length))
                {
                    if (internalMessage.Length != 1) continue;

                    OnReceiveInternalData((InternalMessages) internalMessage[0], clientSteamId);

                    break;
                }

                for (int chNum = 0; chNum < Options.Channels.Length; chNum++)
                {
                    while (DataAvailable(out CSteamID clientSteamId, out byte[] receiveBuffer, chNum))
                    {
                        OnReceiveData(receiveBuffer, clientSteamId, chNum);
                    }
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