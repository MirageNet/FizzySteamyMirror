#region Statements

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public class SteamServer : SteamCommon
    {
        static readonly ILogger Logger = LogFactory.GetLogger(typeof(SteamServer));

        #region Variables

        public bool Connected = false;
        private readonly IDictionary<CSteamID, SteamConnection> ConnectedSteamUsers;
        private Callback<P2PSessionRequest_t> _connectionRequest = null;
        private Message msgBuffer;

        #endregion

        #region Class Specific

        /// <summary>
        ///     Initialize new <see cref="SteamServer"/> server connection.
        /// </summary>
        /// <param name="options">The options we want our server to run.</param>
        public SteamServer(SteamOptions options) : base(options)
        {
            Options = options;
            ConnectedSteamUsers = new Dictionary<CSteamID, SteamConnection>(Options.MaxConnections);

            SteamNetworking.AllowP2PPacketRelay(Options.AllowSteamRelay);

            _connectionRequest = Callback<P2PSessionRequest_t>.Create(OnConnectionRequest);
        }

        /// <summary>
        ///     Connection request from a steam user.
        /// </summary>
        /// <param name="result">The information coming back from steam.</param>
        private void OnConnectionRequest(P2PSessionRequest_t result)
        {
            if (ConnectedSteamUsers.ContainsKey(result.m_steamIDRemote))
                if (Logger.logEnabled)
                {
                    Logger.LogWarning(
                        $"SteamServer client {result.m_steamIDRemote} has already been added to connection list. Disconnecting old user.");

                    ConnectedSteamUsers[result.m_steamIDRemote].Disconnect();
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
            if (QueuedData.Count > 0)
            {
                QueuedData.TryDequeue(out msgBuffer);

                if (ConnectedSteamUsers.Count >= Options.MaxConnections)
                {
                    SteamSend(msgBuffer.steamId, InternalMessages.TooManyUsers);

                    return null;
                }

                Options.ConnectionAddress = msgBuffer.steamId;

                var client = new SteamConnection(Options) {Connected = true};

                if (Logger.logEnabled)
                    Logger.Log($"SteamServer connecting with {msgBuffer.steamId} and accepting handshake.");

                ConnectedSteamUsers.Add(msgBuffer.steamId, client);

                SteamSend(msgBuffer.steamId, InternalMessages.Accept);

                return await Task.FromResult(msgBuffer.steamId == CSteamID.Nil ? null : client);
            }

            return null;
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
        ///     Update method to be called by the transport.
        /// </summary>
        protected internal override void Update()
        {
            // Need server to call update on the clients to process
            // steam messages in the steam connection.
            foreach (var user in ConnectedSteamUsers)
                user.Value.Update();

            // Look for internal messages only.
            for (var channel = 0; channel < Options.Channels.Length + 1; channel++)
                if (DataAvailable(out var steamId, out var receiveBuffer, channel))
                {
                    if (receiveBuffer.Length != 1 ||
                        (InternalMessages) receiveBuffer[0] != InternalMessages.Connect) return;

                    msgBuffer = new Message(steamId, InternalMessages.Connect, receiveBuffer);

                    QueuedData.Enqueue(msgBuffer);
                }
        }

        #endregion
    }
}