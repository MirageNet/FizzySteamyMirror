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
        ///     Connection request has failed to connect to user.
        /// </summary>
        /// <param name="result">The information back from steam.</param>
        protected override void ConnectionFailed(P2PSessionConnectFail_t result)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        ///     Accept new incoming connections.
        /// </summary>
        /// <param name="result">The connection information.</param>
        protected override void AcceptConnection(P2PSessionRequest_t result) =>
            SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);

        /// <summary>
        ///     Disconnect connection.
        /// </summary>
        public override void Disconnect()
        {
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