#region Statements

using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using Cysharp.Threading.Tasks;

#endregion

namespace Mirage.FizzySteam
{
    public class SteamServer : SteamCommon
    {
        private static readonly ILogger Logger = LogFactory.GetLogger(typeof(SteamServer));

        #region Variables

        private readonly Transport _transport;
        private readonly IDictionary<CSteamID, SteamConnection> _connectedSteamUsers;
        private Callback<P2PSessionRequest_t> _connectionRequest;

        #endregion

        #region Class Specific

        /// <summary>
        ///     Initialize new <see cref="SteamServer"/> server connection.
        /// </summary>
        /// <param name="options">The options we want our server to run.</param>
        /// <param name="transport">Transport to attach to.</param>
        public SteamServer(SteamOptions options, Transport transport) : base(options)
        {
            Options = options;
            _connectedSteamUsers = new Dictionary<CSteamID, SteamConnection>(Options.MaxConnections);

            _transport = transport;

            UniTask.Run(ProcessIncomingMessages).Forget();
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

        public void StartListening()
        {
            if (Logger.logEnabled) Logger.Log("SteamServer listening for incoming connections....");

            SteamNetworking.AllowP2PPacketRelay(Options.AllowSteamRelay);

            _connectionRequest = Callback<P2PSessionRequest_t>.Create(OnConnectionRequest);

            _transport.Started.Invoke();
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

            _connectionRequest?.Dispose();
            _connectionRequest = null;

            _connectedSteamUsers.Clear();
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

                    if (_connectedSteamUsers.TryGetValue(clientSteamId, out SteamConnection connection))
                    {
                        connection.Disconnect();

                        _connectedSteamUsers.Remove(clientSteamId);

                        if (Logger.logEnabled)
                            Logger.Log($"Client with SteamID {clientSteamId} disconnected.");
                    }

                    break;
                case InternalMessages.Connect:

                    if (_connectedSteamUsers.Count >= Options.MaxConnections)
                    {
                        SteamSend(clientSteamId, InternalMessages.TooManyUsers);

                        return;
                    }

                    if (_connectedSteamUsers.ContainsKey(clientSteamId)) return;

                    Options.ConnectionAddress = clientSteamId;

                    var client = new SteamConnection(Options, true);

                    _transport.Connected.Invoke(client);

                    if (Logger.logEnabled)
                        Logger.Log($"SteamServer connecting with {clientSteamId} and accepting handshake.");

                    _connectedSteamUsers.Add(clientSteamId, client);

                    SteamSend(clientSteamId, InternalMessages.Accept);
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
        internal override void OnReceiveData(byte[] data, CSteamID clientSteamId, int channel)
        {
            if (_connectedSteamUsers.TryGetValue(clientSteamId, out SteamConnection client))
            {
                client.OnReceiveData(data, clientSteamId, channel);
            }

            if (Logger.logEnabled)
                Logger.Log(
                    $"SteamConnection: Queue up message Event Type: {InternalMessages.Data} data: {BitConverter.ToString(data)}");
        }

        #endregion
    }
}
