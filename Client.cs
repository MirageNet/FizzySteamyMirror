#region Using Statements

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OMN.Scripts.Managers;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public class Client : Common, IChannelConnection
    {
        public bool Connected { get; private set; }
        public bool Error { get; private set; }

        private byte[] clientPoolData;

        private event Action<byte[], int> OnReceivedData;
        private event Action OnConnected;
        private event Action OnDisconnected;

        private TimeSpan ConnectionTimeout;

        private CSteamID hostSteamID = CSteamID.Nil;
        private TaskCompletionSource<Task> connectedComplete;
        private CancellationTokenSource cancelToken;

        private Client(FizzySteamyMirror transport) : base(transport)
        {
            ConnectionTimeout = TimeSpan.FromSeconds(Math.Max(1, transport.Timeout));
        }

#if MirrorNG
        public static Client CreateClient(FizzySteamyMirror transport, CSteamID connectionId)
        {
            var c = new Client(transport);
            
            c.hostSteamID = connectionId;

            while (c.Connected)
            {
                Task.Run(() => c.ReceiveData());

                Task.Delay(1000);
            }
            return c;
        }
#endif

        public static Client CreateClient(FizzySteamyMirror transport, string host)
        {
            Client c = new Client(transport);

#if !MirrorNG
            c.OnConnected += () => transport.OnClientConnected.Invoke();
            c.OnDisconnected += () => transport.OnClientDisconnected.Invoke();
            c.OnReceivedData += (data, channel) => transport.OnClientDataReceived.Invoke(new ArraySegment<byte>(data), channel);
#else
            var nc = transport.GetComponent<NetworkClient>();

            c.OnConnected += () => nc.Connected.Invoke(new NetworkConnection(c));
            c.OnDisconnected += () => nc.Disconnected.Invoke();
#endif

            if (SteamworksManager.Instance.Initialized)
            {
                c.Connect(host);
            }
            else
            {
                Debug.LogError("SteamWorks not initialized");
                c.OnConnectionFailed(CSteamID.Nil);
            }

            return c;
        }

        private async void Connect(string host)
        {
            cancelToken = new CancellationTokenSource();

            try
            {
                hostSteamID = new CSteamID(ulong.Parse(host));
                connectedComplete = new TaskCompletionSource<Task>();

                OnConnected += SetConnectedComplete;
                CloseP2PSessionWithUser(hostSteamID);

                SendInternal(hostSteamID, InternalMessages.CONNECT);

                Task connectedCompleteTask = connectedComplete.Task;

                if (await Task.WhenAny(connectedCompleteTask, Task.Delay(ConnectionTimeout, cancelToken.Token)) != connectedCompleteTask)
                {
                    Debug.LogError($"Connection to {host} timed out.");
                    OnConnected -= SetConnectedComplete;
                    OnConnectionFailed(hostSteamID);
                }

                OnConnected -= SetConnectedComplete;
            }
            catch (FormatException)
            {
                Debug.LogError($"Connection string was not in the right format. Did you enter a SteamId?");
                Error = true;
                OnConnectionFailed(hostSteamID);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                Error = true;
                OnConnectionFailed(hostSteamID);
            }
            finally
            {
                if (Error)
                {
                    OnConnectionFailed(CSteamID.Nil);
                }
            }
        }

        public void Disconnect()
        {
            SendInternal(hostSteamID, InternalMessages.DISCONNECT);
            Dispose();
            cancelToken.Cancel();
            transport.StartCoroutine(WaitDisconnect(hostSteamID));
        }

        private void SetConnectedComplete() => connectedComplete.SetResult(connectedComplete.Task);

        protected override void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel)
        {
            if (clientSteamID != hostSteamID)
            {
                Debug.LogError("Received a message from an unknown");
                return;
            }

            OnReceivedData?.Invoke(data, channel);
        }
        protected override void OnNewConnection(P2PSessionRequest_t result)
        {
            if (hostSteamID == result.m_steamIDRemote)
                SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);
            else
                Debug.LogError("P2P Acceptance Request from unknown host ID.");
        }

        protected override void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID)
        {
            switch (type)
            {
                case InternalMessages.ACCEPT_CONNECT:
                    Connected = true;
                    OnConnected?.Invoke();
                    Debug.Log("Connection established.");
                    break;
                case InternalMessages.DISCONNECT:
                    Connected = false;
                    OnDisconnected?.Invoke();
                    Debug.Log("Disconnected.");
                    break;
                default:
                    Debug.Log("Received unknown message type");
                    break;
            }
        }

        public bool Send(byte[] data, int channelId) => Send(hostSteamID, data, channelId);

        protected override void OnConnectionFailed(CSteamID remoteId) => OnDisconnected.Invoke();

#region Implementation of IConnection

        public Task SendAsync(ArraySegment<byte> data, int channel)
        {
            Debug.Log($"Client Sending Data: {data}");

            clientPoolData = new byte[data.Count];

            Array.Copy(data.Array, data.Offset, clientPoolData, 0, data.Count);

            Send(clientPoolData, channel);

            return Task.CompletedTask;
        }

        public Task SendAsync(ArraySegment<byte> data)
        {
            return SendAsync(data, 0);
        }

        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            buffer.ToArray();

            if (buffer.Length > 0)
            {
                Debug.Log($"Client Receiving Data: {buffer}");
                return true;
            }

            return false;
        }

        public EndPoint GetEndPointAddress()
        {
            return new IPEndPoint(IPAddress.None, 0);
        }

        #endregion
    }
}
        }

        private async void Connect(string host)
        {
            cancelToken = new CancellationTokenSource();

            try
            {
                hostSteamID = new CSteamID(Convert.ToUInt64(host));
                connectedComplete = new TaskCompletionSource<Task>();

                OnConnected += SetConnectedComplete;
                CloseP2PSessionWithUser(hostSteamID);

                //Send a connect message to the steam client - this requests a connection with them
                SendInternal(hostSteamID, connectMsgBuffer);

                Task connectedCompleteTask = connectedComplete.Task;

                if (await Task.WhenAny(connectedCompleteTask, Task.Delay(ConnectionTimeout, cancelToken.Token)) != connectedCompleteTask)
                {
                    OnConnected -= SetConnectedComplete;
                    throw new Exception("Timed out while connecting");
                }

                OnConnected -= SetConnectedComplete;
            }
            catch (FormatException)
            {
                OnReceivedError?.Invoke(new Exception("ERROR passing steam ID address"));
            }
            catch (Exception ex)
            {
                OnReceivedError?.Invoke(ex);
            }
        }

        public void Disconnect()
        {
            SendInternal(hostSteamID, disconnectMsgBuffer);
            Dispose();
            cancelToken.Cancel();

            transport.StartCoroutine(WaitDisconnect(hostSteamID));
        }

        private void SetConnectedComplete() => connectedComplete.SetResult(connectedComplete.Task);

        protected override void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel)
        {
            if (clientSteamID != hostSteamID)
            {
                Debug.LogError("Received a message from an unknown");
                return;
            }

            OnReceivedData?.Invoke(data, channel);
        }

        protected override void OnNewConnection(P2PSessionRequest_t result)
        {
            if (hostSteamID == result.m_steamIDRemote)
            {
                SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);
            }
            else
            {
                Debug.LogError("P2P Acceptance Request from unknown host ID.");
            }
        }

        protected override void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID)
        {
            switch (type)
            {
                case InternalMessages.ACCEPT_CONNECT:
                    Connected = true;
                    Debug.Log("Connection established.");
                    OnConnected?.Invoke();
                    break;
                case InternalMessages.DISCONNECT:
                    Connected = false;
                    Debug.Log("Disconnected.");
                    OnDisconnected?.Invoke();
                    break;
                default:
                    Debug.Log("Received unknown message type");
                    break;
            }
        }


        public bool Send(byte[] data, int channelId) => Send(hostSteamID, data, channelId);
    }
}
