#region Statements

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public class SteamClient : SteamCommon, IChannelConnection
    {
        #region Class Specific

        public SteamClient(ClientOptions options, MirrorNGSteamTransport transport) : base(transport)
        {
            _options = options;

            var nc = transport.GetComponent<NetworkClient>();

            OnConnected += () => nc.Connected.Invoke(new NetworkConnection(this));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="channel"></param>
        public void OnReceivedData(byte[] data, int channel)
        {
            _receivedMessages.Enqueue(new MemoryStream(data));

            OnDataReceived?.Invoke(data, channel);
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnClientConnected()
        {
            Connected = true;
            OnConnected?.Invoke();
        }

        /// <summary>
        ///     Connect to server.
        /// </summary>
        /// <returns></returns>
        public async Task<IConnection> ConnectAsync()
        {
#if UNITY_EDITOR
            Debug.Log("Starting client.");
#endif
            SteamNetworking.AllowP2PPacketRelay(_options.AllowSteamRelay);

            _cancellationToken = new CancellationTokenSource();

            try
            {
                var connectedComplete = new TaskCompletionSource<Task>();

                OnConnected += () => connectedComplete.SetResult(connectedComplete.Task);

                SteamNetworking.CloseP2PSessionWithUser(_options.ConnectionAddress);

                Send(_options.ConnectionAddress, InternalMessages.Connect);

                Task connectedCompleteTask = connectedComplete.Task;

                if (await Task.WhenAny(connectedCompleteTask,
                    Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.ConnectionTimeOut)), _cancellationToken.Token)) != connectedCompleteTask)
                {
#if UNITY_EDITOR
                    Debug.LogError($"Connection to {_options.ConnectionAddress.m_SteamID.ToString()} timed out.");
#endif
                    OnConnected -= () => connectedComplete.SetResult(connectedComplete.Task);

                    return null;
                }

                OnConnected -= () => connectedComplete.SetResult(connectedComplete.Task);


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

        #endregion

        #region Variables

        private ClientOptions _options;
        private CancellationTokenSource _cancellationToken;
        private event Action OnConnected;
        public event Action<byte[], int> OnDataReceived;
        private byte[] _clientPoolData;
        private readonly ConcurrentQueue<MemoryStream> _receivedMessages = new ConcurrentQueue<MemoryStream>();

        #endregion

        #region Implementation of IConnection

        /// <summary>
        ///     Send async message using default channel.
        /// </summary>
        /// <param name="data">The data we want to send over the wire.</param>
        /// <returns>Return back the task results.</returns>
        public Task SendAsync(ArraySegment<byte> data)
        {
            return SendAsync(data, 0);
        }

        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            try
            {
#if UNITY_EDITOR
                
#endif
                // We have no way to keep connection alive here due to steam backend
                // so we must create our own small retry system to know when connection finally
                // has established. This should be fast and no more then connection timeout.
                // We should be receiving ping pong messages from mirror so if retries fail
                // and we get pass this then the queue will be empty and should disconnect user.

                MemoryStream tempBuffer = new MemoryStream();
                bool waitingForNewMsg = true;
                while(waitingForNewMsg)
                {
                    if(_receivedMessages.TryDequeue(out tempBuffer))
                    {
                        waitingForNewMsg = false;
                    }

                    await Task.Delay(1);
                }
                string str = BitConverter.ToString(tempBuffer.ToArray());
                Debug.Log($"Client Receiving Data: {str} ");
                buffer.Write(tempBuffer.ToArray(), 0, tempBuffer.ToArray().Length);

                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Disconnect()
        {
            ConnectionFailure?.Dispose();
            ConnectionFailure = null;

            _cancellationToken?.Cancel();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public EndPoint GetEndPointAddress()
        {
            return new DnsEndPoint(_options.ConnectionAddress.m_SteamID.ToString(), 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        public Task SendAsync(ArraySegment<byte> data, int channel)
        {
            Debug.Log($"Client Sending Data: {data.Array}");

            _clientPoolData = new byte[data.Count];

            Array.Copy(data.Array, data.Offset, _clientPoolData, 0, _clientPoolData.Length);

            Send(_options.ConnectionAddress, _clientPoolData, channel);

            string str = BitConverter.ToString(_clientPoolData);
            Debug.Log($"Client Sending Data: {str}");

            string str2 = BitConverter.ToString(data.Array);
            Debug.Log($"Client Param Data: {str2}");

            return Task.CompletedTask;
        }

        #endregion

        #region Overrides of SteamCommon

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="clientSteamID"></param>
        protected override void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID)
        {
            switch (type)
            {
                case InternalMessages.ConnectionAccepted:

                    Connected = true;
                    OnConnected?.Invoke();

#if UNITY_EDITOR
                    Debug.Log("Connection established.");
#endif

                    break;
                case InternalMessages.Disconnect:

                    Connected = false;
#if UNITY_EDITOR
                    Debug.Log("Disconnected.");
#endif
                    //OnDisconnected?.Invoke();
                    break;
                default:
                    Debug.Log("Received unknown message type");
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="clientSteamID"></param>
        /// <param name="channel"></param>
        protected override void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel)
        {
            if (clientSteamID != _options.ConnectionAddress)
            {
#if UNITY_EDITOR
                Debug.LogError("Received a message from an unknown");
#endif
                return;
            }

            _receivedMessages.Enqueue(new MemoryStream(data));

             //OnReceivedData?.Invoke(data, channel);
        }

        /// <summary>
        ///    Connection to server has failed to connect.
        /// </summary>
        /// <param name="result">The results of why we failed.</param>
        protected override void ConnectionFailed(P2PSessionConnectFail_t result)
        {
            base.ConnectionFailed(result);
        }

        #endregion
    }
}
