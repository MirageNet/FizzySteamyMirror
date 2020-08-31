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
    public class SteamServer : SteamCommon
    {
        private static readonly ILogger Logger = LogFactory.GetLogger(typeof(SteamServer));

        #region Variables

        public bool Connected;
        private readonly IDictionary<CSteamID, SteamConnection> _connectedSteamUsers;
        private readonly ConcurrentQueue<Message> _connectionQueue = new ConcurrentQueue<Message>();
        private Callback<P2PSessionRequest_t> _connectionRequest;
        private Message _msgBuffer;

        #endregion

        #region Class Specific

        /// <summary>
        ///     Initialize new <see cref="SteamServer"/> server connection.
        /// </summary>
        /// <param name="options">The options we want our server to run.</param>
        public SteamServer(SteamOptions options) : base(options)
        {
            Options = options;
            _connectedSteamUsers = new Dictionary<CSteamID, SteamConnection>(Options.MaxConnections);

            SteamNetworking.AllowP2PPacketRelay(Options.AllowSteamRelay);

            _connectionRequest = Callback<P2PSessionRequest_t>.Create(OnConnectionRequest);
        }

        /// <summary>
        ///     Connection request from a steam user.
        /// </summary>
        /// <param name="result">The information coming back from steam.</param>
        private void OnConnectionRequest(P2PSessionRequest_t result)
        {
            if (_connectedSteamUsers.ContainsKey(result.m_steamIDRemote))
                if (Logger.logEnabled)
                {
                    Logger.LogWarning(
                        $"SteamServer client {result.m_steamIDRemote} has already been added to connection list. Disconnecting old user.");

                    _connectedSteamUsers[result.m_steamIDRemote].Disconnect();
                }

            if (Logger.logEnabled)
                Logger.Log($"SteamServer request from {result.m_steamIDRemote}. Server accepting.");

            SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);
        }

        /// <summary>
        ///     Steam transport way of scanning for connections as steam itself
        ///     uses events to trigger connections versus a real listening connection.
        /// </summary>
        /// <returns></returns>
        public async Task<SteamConnection> QueuedConnectionsAsync()
        {
            // Check to see if we received a connection message.
            if (_connectionQueue.Count <= 0) return null;

            // It was data connection let's pull data out.
            _connectionQueue.TryDequeue(out _msgBuffer);

            if (_connectedSteamUsers.Count >= Options.MaxConnections)
            {
                SteamSend(_msgBuffer.steamId, InternalMessages.TooManyUsers);

                return null;
            }

            if (_connectedSteamUsers.ContainsKey(_msgBuffer.steamId)) return null;

            Options.ConnectionAddress = _msgBuffer.steamId;

            var client = new SteamConnection(Options) {Connected = true};

            if (Logger.logEnabled)
                Logger.Log($"SteamServer connecting with {_msgBuffer.steamId} and accepting handshake.");

            _connectedSteamUsers.Add(_msgBuffer.steamId, client);

            SteamSend(_msgBuffer.steamId, InternalMessages.Accept);

            return await Task.FromResult(_msgBuffer.steamId == CSteamID.Nil ? null : client);

        }

        public void StartListening()
        {
            if (Logger.logEnabled) Logger.Log("SteamServer listening for incoming connections....");

            Connected = true;
        }

        #endregion

        #region Overrides of SteamCommon

        /// <summary>
        ///     Disconnect connection.
        /// </summary>
        public override void Disconnect()
        {
            if (Logger.logEnabled) Logger.Log("SteamServer shutting down.");

            base.Disconnect();

            _connectionRequest.Dispose();
            _connectionRequest = null;
        }

        /// <summary>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="type"></param>
        internal override bool SteamSend(CSteamID target, InternalMessages type)
        {
            return SteamNetworking.SendP2PPacket(target, new[] {(byte) type}, 1, EP2PSend.k_EP2PSendReliable,
                Options.Channels.Length);
        }

        /// <summary>
        ///     Process our internal messages away from mirror.
        /// </summary>
        /// <param name="type">The <see cref="InternalMessages"/> type message we received.</param>
        /// <param name="clientSteamId">The client id which the internal message came from.</param>
        protected override void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamId)
        {
            switch (type)
            {
                case InternalMessages.Disconnect:
                    if (Logger.logEnabled)
                        Logger.Log("Received internal message to disconnect steam user.");

                    if (_connectedSteamUsers.TryGetValue(clientSteamId, out var connection))
                    {
                        connection.Disconnect();
                        SteamNetworking.CloseP2PSessionWithUser(clientSteamId);
                        _connectedSteamUsers.Remove(clientSteamId);

                        if (Logger.logEnabled)
                            Logger.Log($"Client with SteamID {clientSteamId} disconnected.");
                    }

                    break;
                case InternalMessages.Connect:
                    _msgBuffer = new Message(clientSteamId, InternalMessages.Connect, new[] {(byte) type});

                    _connectionQueue.Enqueue(_msgBuffer);
                    break;
                default:
                    if (Logger.logEnabled)
                        Logger.Log(
                            $"SteamConnection cannot process internal message {type}. If this is anything other then {InternalMessages.Data} something has gone wrong.");
                    break;
            }
        }

        /// <summary>
        ///     Process data incoming from steam backend.
        /// </summary>
        /// <param name="data">The data that has come in.</param>
        /// <param name="clientSteamId">The client the data came from.</param>
        /// <param name="channel">The channel the data was received on.</param>
        protected override void OnReceiveData(byte[] data, CSteamID clientSteamId, int channel)
        {
            var dataMsg = new Message(clientSteamId, InternalMessages.Data, data);

            if (Logger.logEnabled)
                Logger.Log(
                    $"SteamConnection: Queue up message Event Type: {dataMsg.eventType} data: {BitConverter.ToString(dataMsg.data)}");
            
            // We need to check if data is from a user and pass it to the correct queue system
            // due to how mirrorng works we cant just event listener it.
            if (_connectedSteamUsers.ContainsKey(clientSteamId))
                _connectedSteamUsers[clientSteamId].QueuedData.Enqueue(dataMsg);
        }

        #endregion
    }
}