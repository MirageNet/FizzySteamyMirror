#region Statements

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public enum InternalMessages : byte
    {
        Connect,
        Accepted,
    }

    public class SteamConnection : SteamCommon, IChannelConnection
    {
        #region Variables

        private SteamOptions _options;
        private byte[] _clientSendPoolData, _clientReceivePoolData;
        protected Callback<P2PSessionConnectFail_t> ConnectionFailure = null;
        private CancellationTokenSource _cancellationToken;
        private event Action OnConnected;
        public bool Connected = false;

        #endregion

        #region Class Specific

        /// <summary>
        ///     Connect to server.
        /// </summary>
        /// <returns></returns>
        public async Task<IConnection> ConnectAsync()
        {
#if UNITY_EDITOR
            Debug.Log("Starting client.");
#endif
            _cancellationToken = new CancellationTokenSource();

            if(SteamNetworking.GetP2PSessionState(_options.ConnectionAddress, out var connectionState))
                if (bool.Parse(connectionState.m_bConnectionActive.ToString()))
                    SteamNetworking.CloseP2PSessionWithUser(_options.ConnectionAddress);

            try
            {
                // Send a message to server to intiate handshake connection
                SteamSend(_options.ConnectionAddress, InternalMessages.Connect);

                var connectedComplete = new TaskCompletionSource<Task>();
                Task connectedCompleteTask = connectedComplete.Task;

                while (await Task.WhenAny(connectedCompleteTask, Task.Delay(1000, _cancellationToken.Token)) != connectedCompleteTask)
                {
                    if (_options.ConnectionTimeOut < 30)
                    {
#if UNITY_EDITOR
                        Debug.LogError($"Connection to {_options.ConnectionAddress.m_SteamID.ToString()} timed out.");
#endif
                        return null;
                    }

                    DataReceivedCheck(out var id, out var buffer, _options.Channels.Length);

                    if (buffer == null || buffer.Length != 1 ||
                        (InternalMessages) buffer[0] != InternalMessages.Accepted) continue;

                    connectedComplete.SetResult(connectedComplete.Task);
                    _cancellationToken.Cancel();
                    OnConnected?.Invoke();
                    Connected = true;
                }
                
                return this;

            }
            catch (FormatException)
            {
#if UNITY_EDITOR
                Debug.LogError("Connection string was not in the right format. Did you enter a SteamId?");
#endif
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR
                Debug.LogError(ex.Message);
#endif
            }

            return null;
        }

        /// <summary>
        ///     A connection attempt has failed to connect to us.
        /// </summary>
        /// <param name="result">The results of why the client failed to connect.</param>
        private void ConnectionFailed(P2PSessionConnectFail_t result)
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
        /// </summary>
        /// <param name="target"></param>
        /// <param name="type"></param>
        internal override bool SteamSend(CSteamID target, InternalMessages type)
        {
            return SteamNetworking.SendP2PPacket(target, new[] {(byte) type}, 1, EP2PSend.k_EP2PSendReliable,
                _options.Channels.Length);
        }


        /// <summary>
        /// </summary>
        /// <param name="host"></param>
        /// <param name="msgBuffer"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        protected bool Send(CSteamID host, byte[] msgBuffer, int channel)
        {
            return SteamNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, _options.Channels[channel], channel);
        }

        public SteamConnection(SteamOptions options, Transport transport)
        {
            _options = options;
            ConnectionFailure = Callback<P2PSessionConnectFail_t>.Create(ConnectionFailed);
            SteamNetworking.AllowP2PPacketRelay(_options.AllowSteamRelay);
        }

        #endregion

        #region Implementation of IConnection

        /// <summary>
        ///     Send data on default reliable channel.
        /// </summary>
        /// <param name="data">The data we want to send.</param>
        /// <returns>Whether or not we sent our data.</returns>
        public Task SendAsync(ArraySegment<byte> data)
        {
            // Default send to reliable channel;
            return SendAsync(data, 0);
        }

        /// <summary>
        ///     Check if we have data in the pipe line that we need to process
        /// </summary>
        /// <param name="buffer">The buffer we need to write data too.</param>
        /// <returns></returns>
        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            try
            {
                bool waitingForNewMsg = true;

                while (waitingForNewMsg)
                {
                    for (var channel = 0; channel < _options.Channels.Length; channel++)
                        if(DataReceivedCheck(out var steamUserId, out _clientReceivePoolData, channel))
                        {
                            waitingForNewMsg = false;
                            break;
                        }

                    await Task.Delay(10);
                }

                buffer.Write(_clientReceivePoolData, 0, _clientReceivePoolData.Length);

                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Disconnect()
        {
            SteamNetworking.CloseP2PSessionWithUser(_options.ConnectionAddress);

            _cancellationToken.Cancel();

            ConnectionFailure?.Dispose();
            ConnectionFailure = null;
        }

        /// <summary>
        ///     Get the network address using steams id.
        /// </summary>
        /// <returns></returns>
        public EndPoint GetEndPointAddress()
        {
            return new DnsEndPoint(_options.ConnectionAddress.m_SteamID.ToString(), 0);
        }

        /// <summary>
        ///     Send data on a specific channel.
        /// </summary>
        /// <param name="data">The data we want to send.</param>
        /// <param name="channel">The channel we want to send it on.</param>
        /// <returns></returns>
        public Task SendAsync(ArraySegment<byte> data, int channel)
        {
            _clientSendPoolData = new byte[data.Count];

            Array.Copy(data.Array, data.Offset, _clientSendPoolData, 0, _clientSendPoolData.Length);

            return Send(_options.ConnectionAddress, _clientSendPoolData, channel) ? Task.CompletedTask : null;
        }

        #endregion
    }
}