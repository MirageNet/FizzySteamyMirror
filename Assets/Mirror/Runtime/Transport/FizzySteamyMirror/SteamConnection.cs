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

        protected override void AcceptConnection(P2PSessionRequest_t result)
        {
            if(result.m_steamIDRemote != _options.ConnectionAddress) return;

            SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);
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
        private bool Send(CSteamID host, byte[] msgBuffer, int channel)
        {
            return SteamNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, _options.Channels[channel], channel);
        }

        public SteamConnection(SteamOptions options, Transport transport)
        {
            _options = options;
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
                    if (!Connected)
                        return false;

                    for (var channel = 0; channel < _options.Channels.Length; channel++)
                        if(DataReceivedCheck(out var steamUserId, out _clientReceivePoolData, channel))
                        {
                            waitingForNewMsg = false;
                            break;
                        }

                    await Task.Delay(10);
                }

                buffer.SetLength(0);

                await buffer.WriteAsync(_clientReceivePoolData, 0, _clientReceivePoolData.Length);

                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public override void Disconnect()
        {
            SteamNetworking.CloseP2PSessionWithUser(_options.ConnectionAddress);

            _cancellationToken?.Cancel();
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