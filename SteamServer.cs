#region Statements

using System.Collections.Generic;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public class SteamServer : SteamCommon
    {
        #region Variables

        private readonly SteamOptions _options;
        internal readonly Queue<CSteamID> _queuedConnections = new Queue<CSteamID>();
        private Callback<P2PSessionRequest_t> _connectionListener;
        public bool Connected = false;

        #endregion

        #region Class Specific

        /// <summary>
        ///     Initialize new <see cref="SteamServer"/> server connection.
        /// </summary>
        /// <param name="options">The options we want our server to run.</param>
        /// <param name="transport">The transport to make connections with.</param>
        public SteamServer(SteamOptions options, MirrorNGSteamTransport transport)
        {
            _options = options;
            _connectionListener = Callback<P2PSessionRequest_t>.Create(AcceptConnection);
            SteamNetworking.AllowP2PPacketRelay(_options.AllowSteamRelay);
        }

        public void StartListening()
        {
#if UNITY_EDITOR
            Debug.Log("Starting server.");
#endif
            Connected = true;
        }

        /// <summary>
        ///     Accept new incoming connections.
        /// </summary>
        /// <param name="result">The connection information.</param>
        private void AcceptConnection(P2PSessionRequest_t result)
        {
            SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);

            if (_queuedConnections.Contains(result.m_steamIDRemote)) return;

            _queuedConnections.Enqueue(result.m_steamIDRemote);
        }

        /// <summary>
        ///     Disconnect connection.
        /// </summary>
        public void Disconnect()
        {
            _connectionListener?.Dispose();
            _connectionListener = null;
            _queuedConnections.Clear();
        }


        #endregion

        #region Overrides of SteamCommon

        /// <summary>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="type"></param>
        internal override bool SteamSend(CSteamID target, InternalMessages type)
        {
            return SteamNetworking.SendP2PPacket(target, new[] { (byte)type }, 1, EP2PSend.k_EP2PSendReliable,
                _options.Channels.Length);
        }

        #endregion
    }
}