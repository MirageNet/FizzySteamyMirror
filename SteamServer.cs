#region Statements

using System;
using System.Collections.Generic;
using System.IO;
using Mirror;
using Steamworks;
using UnityEngine;

#endregion

namespace OMN.Scripts.Networking.MirrorNGSteam
{
    public class SteamServer : SteamCommon
    {
        #region Variables

        private readonly SteamServerOptions _options;
        internal readonly Queue<CSteamID> _queuedConnections = new Queue<CSteamID>();
        private Callback<P2PSessionRequest_t> _connectionListener;

        protected internal readonly BidirectionalDictionary<CSteamID, SteamClient> ClientConnections =
            new BidirectionalDictionary<CSteamID, SteamClient>();

        #endregion

        #region Class Specific

        public SteamServer(SteamServerOptions options, MirrorNGSteamTransport transport) : base(transport)
        {
            _options = options;
            var t = transport.GetComponent<NetworkServer>();
        }

        public void StartListening()
        {
#if UNITY_EDITOR
            Debug.Log("Starting server.");
#endif
            SteamNetworking.AllowP2PPacketRelay(_options.AllowSteamRelay);

            _connectionListener = Callback<P2PSessionRequest_t>.Create(AcceptConnection);
            ConnectionFailure = Callback<P2PSessionConnectFail_t>.Create(ConnectionFailed);

            Connected = true;
        }

        /// <summary>
        ///     Accept new incoming connections.
        /// </summary>
        /// <param name="result">The connection information.</param>
        private void AcceptConnection(P2PSessionRequest_t result)
        {
            SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);

            Send(result.m_steamIDRemote, InternalMessages.ConnectionAccepted);

            if (_queuedConnections.Contains(result.m_steamIDRemote)) return;

            _queuedConnections.Enqueue(result.m_steamIDRemote);
        }

        #endregion

        #region Overrides of SteamCommon

        public override void Update()
        {
            foreach (var clientConnection in ClientConnections)
            {
                var client = clientConnection.Value;

                client?.Update();
            }

            base.Update();
        }

        /// <summary>
        ///     Disconnect connection.
        /// </summary>
        public override void Disconnect()
        {
            _connectionListener.Dispose();
            _connectionListener = null;
            _queuedConnections.Clear();
        }

        /// <summary>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="clientSteamID"></param>
        protected override void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID)
        {
            switch (type)
            {
                case InternalMessages.Connect:

                    if (ClientConnections.Count >= _options.MaxConnections)
                    {
                        Send(clientSteamID, InternalMessages.Disconnect);

                        return;
                    }

                    if (_queuedConnections.Contains(clientSteamID) || ClientConnections.Contains(clientSteamID)) return;

                    _queuedConnections.Enqueue(clientSteamID);

                    Send(clientSteamID, InternalMessages.ConnectionAccepted);

#if UNITY_EDITOR
                    Debug.Log($"Client with SteamID {clientSteamID} connected.");
#endif

                    break;
                case InternalMessages.Disconnect:

                    if (ClientConnections.Contains(clientSteamID))
                    {
                        ClientConnections[clientSteamID].Disconnect();

                        ClientConnections.Remove(clientSteamID);

                        SteamNetworking.CloseP2PSessionWithUser(clientSteamID);

#if UNITY_EDITOR
                        Debug.Log($"Client with SteamID {clientSteamID} disconnected.");

#endif
                    }

                    break;
                default:
                    Debug.Log("Received unknown message type");
                    break;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="data"></param>
        /// <param name="clientSteamID"></param>
        /// <param name="channel"></param>
        protected override void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel)
        {
            if (ClientConnections.Contains(clientSteamID))
            {
                var connectionId = ClientConnections[clientSteamID];

                connectionId.OnReceivedData(data, channel);
            }
            else
            {
                SteamNetworking.CloseP2PSessionWithUser(clientSteamID);

                Debug.LogError("Data received from steam client thats not known " + clientSteamID);

                //OnReceivedError?.Invoke(-1, new Exception("ERROR Unknown SteamID"));
            }
        }

        /// <summary>
        ///     A connection attempt has failed to connect to us.
        /// </summary>
        /// <param name="result">The results of why the client failed to connect.</param>
        protected override void ConnectionFailed(P2PSessionConnectFail_t result)
        {
            base.ConnectionFailed(result);
        }

        #endregion
    }
}