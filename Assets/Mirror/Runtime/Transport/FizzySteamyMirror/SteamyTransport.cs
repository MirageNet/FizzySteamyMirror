#region Statements

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public class SteamyTransport : Transport
    {
        static readonly ILogger Logger = LogFactory.GetLogger(typeof(SteamyTransport));

        #region Variables

        [Header("Steam Server Config")] [SerializeField]
        private bool _allowSteamRelay = true;

        [SerializeField] public EP2PSend[] Channels = new EP2PSend[2]
            {EP2PSend.k_EP2PSendReliable, EP2PSend.k_EP2PSendUnreliable};

        [SerializeField] private int _maxP2PConnections = 4;

        [Header("Steam Client Config")] [SerializeField, Range(1,10)]
        private int _clientConnectionTimeout = 10;

        private SteamServer _server;
        private AutoResetUniTaskCompletionSource _listenCompletionSource;

        public Action<ErrorCodes, string> Error;

        #endregion

        #region Unity Methods

        private void OnApplicationQuit()
        {
            _server?.Disconnect();
        }

        #endregion

        #region Overrides of Transport

        /// <summary>
        ///     Fires up our server and configs options for listening for connections.
        /// </summary>
        /// <returns></returns>
        public override UniTask ListenAsync()
        {
            var op = new SteamOptions
            {
                AllowSteamRelay = _allowSteamRelay, 
                MaxConnections = _maxP2PConnections, 
                Channels = Channels
            };

            _server = new SteamServer(op, this);

            _server.StartListening();

            _listenCompletionSource = AutoResetUniTaskCompletionSource.Create();

            return _listenCompletionSource.Task;
        }

        /// <summary>
        ///     Disconnect the server and client and shutdown.
        /// </summary>
        public override void Disconnect()
        {
            if(Logger.logEnabled)
                Logger.Log("MirrorNGSteamTransport shutting down.");

            _listenCompletionSource?.TrySetResult();

            _server?.Disconnect();
            _server = null;
        }

        /// <summary>
        ///     Connect clients async to mirror backend.
        /// </summary>
        /// <param name="uri">The address we want to connect to using steam ids.</param>
        /// <returns></returns>
        public override async UniTask<IConnection> ConnectAsync(Uri uri)
        {
            var op = new SteamOptions
            {
                AllowSteamRelay = _allowSteamRelay,
                ConnectionAddress = new CSteamID(ulong.Parse(uri.Host)),
                ConnectionTimeOut = _clientConnectionTimeout,
                Channels = Channels
            };

            var client = new SteamConnection(op);

            client.Error += (errorCode, message) => Error?.Invoke(errorCode, message);

            await client.ConnectAsync();

            return client;
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
