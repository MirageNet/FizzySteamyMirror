#region Statements

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Mirror;
using Steamworks;
using UnityEngine;

#endregion

namespace OMN.Scripts.Networking.MirrorNGSteam
{
    public class SteamServer : SteamCommon, IChannelConnection
    {
        #region Variables
        
        private readonly SteamServerOptions _options;
        internal readonly Queue<CSteamID> _queuedConnections = new Queue<CSteamID>();
        private Callback<P2PSessionRequest_t> _connectionListener = null;

        #endregion

        #region Class Specific

        public SteamServer(SteamServerOptions options, MirrorNGSteamTransport transport) : base(transport)
        {
            _options = options;
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

        #region Implementation of IConnection

        public Task SendAsync(ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            throw new NotImplementedException();
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public EndPoint GetEndPointAddress()
        {
            throw new NotImplementedException();
        }

        public Task SendAsync(ArraySegment<byte> data, int channel)
        {
            throw new NotImplementedException();
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
                case InternalMessages.Connect:

                    //if (steamToMirrorIds.Count >= maxConnections)
                    //{
                    //    SendInternal(clientSteamID, InternalMessages.DISCONNECT);
                    //    return;
                    //}

                    Send(clientSteamID, InternalMessages.ConnectionAccepted);

                    //steamToMirrorIds.Add(clientSteamID, connectionId);

                    //OnConnected?.Invoke(connectionId);

#if UNITY_EDITOR
                    Debug.Log($"Client with SteamID {clientSteamID} connected.");
#endif

                    break;
                case InternalMessages.Disconnect:

                    //if (steamToMirrorIds.Contains(clientSteamID))
                    //{
                        //OnDisconnected?.Invoke(steamToMirrorIds[clientSteamID]);
                        
                        //steamToMirrorIds.Remove(clientSteamID);

                        SteamNetworking.CloseP2PSessionWithUser(clientSteamID);

#if UNITY_EDITOR
                        Debug.Log($"Client with SteamID {clientSteamID} disconnected.");

#endif
                    //}
                    //else
                    //{
                    //OnReceivedError?.Invoke(-1, new Exception("ERROR Unknown SteamID"));
                    //}

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
            //if (steamToMirrorIds.Contains(clientSteamID))
            //{
            //    int connectionId = steamToMirrorIds[clientSteamID];
            //    OnReceivedData?.Invoke(connectionId, data, channel);
            //}
            //else
            //{
            //    CloseP2PSessionWithUser(clientSteamID);
            //    Debug.LogError("Data received from steam client thats not known " + clientSteamID);
            //    OnReceivedError?.Invoke(-1, new Exception("ERROR Unknown SteamID"));
            //}
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