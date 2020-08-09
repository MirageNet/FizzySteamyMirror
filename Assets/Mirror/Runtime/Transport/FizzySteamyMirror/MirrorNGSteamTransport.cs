#region Statements

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public class MirrorNGSteamTransport : Transport
    {
        #region Variables

        [Header("Steam Server Config")] [SerializeField]
        private bool _allowSteamRelay = true;

        [SerializeField] public EP2PSend[] Channels = new EP2PSend[2]
            {EP2PSend.k_EP2PSendReliable, EP2PSend.k_EP2PSendUnreliable};
        [SerializeField] private int _maxP2PConnections = 4;

        [Header("Steam Client Config")] [SerializeField]
        private int _clientConnectionTimeout = 30;

        private SteamServer _server;
        private SteamConnection _client;

        #endregion

        #region Class Specific

        /// <summary>
        ///     Steam transport way of scanning for connections as steam itself
        ///     uses events to trigger connections versus a real listening connection.
        /// </summary>
        /// <returns></returns>
        private async Task<SteamConnection> QueuedConnectionsAsync()
        {
            _server.DataReceivedCheck(out var steamId, out var buffer, Channels.Length);

            if (buffer != null && buffer.Length == 1 && (InternalMessages) buffer[0] == InternalMessages.Connect &&
                !_server._queuedConnections.Contains(steamId))
            {
                _server._queuedConnections.Enqueue(steamId);
                //SteamNetworking.AcceptP2PSessionWithUser(steamId);
            }

            if (_server._queuedConnections.Count <= 0) return await Task.FromResult<SteamConnection>(null);

            var id = _server._queuedConnections.Dequeue();

            var op = new SteamOptions
            {
                AllowSteamRelay = _allowSteamRelay,
                ConnectionAddress = id,
                ConnectionTimeOut = _clientConnectionTimeout,
                Channels = Channels
            };

            var client = new SteamConnection(op, this) {Connected = true};
            _server.SteamSend(id, InternalMessages.Accepted);

            return await Task.FromResult(id == CSteamID.Nil ? null : client);
        }

        #endregion

        #region Overrides of Transport

        /// <summary>
        ///     Fires up our server and configs options for listening for connections.
        /// </summary>
        /// <returns></returns>
        public override Task ListenAsync()
        {
            var op = new SteamOptions
            {
                AllowSteamRelay = _allowSteamRelay, 
                MaxConnections = _maxP2PConnections, 
                Channels = Channels
            };

            _server = new SteamServer(op, this);

            _server.StartListening();

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Disconnect the server and client and shutdown.
        /// </summary>
        public override void Disconnect()
        {
            _client?.Disconnect();
            _client = null;

            _server?.Disconnect();
            _server = null;
        }

        /// <summary>
        ///     Connect clients async to mirror backend.
        /// </summary>
        /// <param name="uri">The address we want to connect to using steam ids.</param>
        /// <returns></returns>
        public override Task<IConnection> ConnectAsync(Uri uri)
        {
            var op = new SteamOptions
            {
                AllowSteamRelay = _allowSteamRelay,
                ConnectionAddress = new CSteamID(ulong.Parse(uri.Host)),
                ConnectionTimeOut = _clientConnectionTimeout,
                Channels = Channels
            };

            _client = new SteamConnection(op, this);

            return _client.ConnectAsync();
        }

        /// <summary>
        ///     Start listening on the server for client connections.
        ///     Due to how steam works we must create our own endless loop to fake how sockets
        ///     would work.
        /// </summary>
        /// <returns>Sends back a <see cref="SteamClient"/> connection back to mirror.</returns>
        public override async Task<IConnection> AcceptAsync()
        {
            // Steam has no way to do async accepting of connections
            // so we create a fake loop to keep server running.
            try
            {
                while (_server.Connected)
                {
                    var client = await QueuedConnectionsAsync();

                    if (client != null)
                    {
                        return client;
                    }

                    await Task.Delay(100);
                }

                return null;
            }
            catch (ObjectDisposedException)
            {
                // expected,  the connection was closed
                return null;
            }
        }

        /// <summary>
        ///     Server's different connection scheme's
        /// </summary>
        /// <returns>Returns back a array of supported scheme's</returns>
        public override IEnumerable<Uri> ServerUri()
        {
            var steamBuilder = new UriBuilder
            {
                Scheme = "steam",
                Host = SteamUser.GetSteamID().m_SteamID.ToString()
            };

            return new[] {steamBuilder.Uri};
        }

        /// <summary>
        ///     Type of connection scheme transport supports.
        /// </summary>

        public override IEnumerable<string> Scheme => new[] {"steam"};

        /// <summary>
        ///     Does this transport support this specific platform.
        /// </summary>
        public override bool Supported => SteamworksManager.Instance.Initialized;

        #endregion
    }
}