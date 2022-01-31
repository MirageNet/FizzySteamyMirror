#if !DISABLESTEAMWORKS

#region Statements

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Mirage.SocketLayer;
using Steamworks;
using UnityEngine;
using Debug = UnityEngine.Debug;

#endregion

namespace Mirage.Sockets.FizzySteam
{
    #region Endpoint Wrappers

    public class SteamEndpoint : IEndPoint, IEquatable<SteamEndpoint>
    {
        public readonly CSteamID Address;

        public SteamEndpoint(CSteamID address)
        {
            Address = address;
        }

        /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.</returns>
        public bool Equals(SteamEndpoint other)
        {
            return Address == other.Address;
        }

        public override bool Equals(object obj)
        {
            if (obj is SteamEndpoint endPoint)
            {
                return Address == endPoint.Address;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }

        public override string ToString()
        {
            return Address.ToString();
        }

        /// <summary>
        /// Creates a new instance of <see cref="IEndPoint"/> with same connection data
        /// <para>this is called when a new connection is created by <see cref="Peer"/></para>
        /// </summary>
        /// <returns></returns>
        public IEndPoint CreateCopy()
        {
            return new SteamEndpoint(Address);
        }
    }

    #endregion

    internal sealed class SteamSocketManager : IDisposable
    {
        #region Fields

        public HSteamListenSocket Socket;
        public HSteamNetConnection HoHSteamNetConnection;
        public readonly Dictionary<IEndPoint, HSteamNetConnection> SteamConnections;
        private readonly HSteamNetPollGroup _pollGroup = SteamNetworkingSockets.CreatePollGroup();
        private Callback<SteamNetConnectionStatusChangedCallback_t> _onConnectionChange = null;
        private readonly bool _isServer;
        public readonly ConcurrentQueue<Message> BufferQueue = new ConcurrentQueue<Message>();
        private readonly SteamOptions _steamOptions;
        private bool _steamInitialized;

        #endregion

        #region Class Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="logType"></param>
        [Conditional("UNITY_EDITOR")]
        internal void LogDebug(string message, LogType logType = LogType.Log)
        {
            switch (logType)
            {
                case LogType.Log:
                    Debug.Log($"<color=green> {message} </color>");
                    break;
                case LogType.Warning:
                    Debug.LogWarning($"<color=orange> {message} </color>");
                    break;
                case LogType.Error:
                    Debug.LogError($"<color=red> {message} </color>");
                    break;
                default:
                    Debug.LogException(new Exception($"<color=red> {message} </color>"));
                    break;

            }
        }

        public bool Update()
        {
            if (_steamInitialized)
                SteamAPI.RunCallbacks();

            var receivedMessages = new IntPtr[_steamOptions.MaxMessagesPolling];
            int receivedCount;

            if ((receivedCount = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, receivedMessages,
                _steamOptions.MaxMessagesPolling)) > 0)
            {
                for (int i = 0; i < receivedCount; i++)
                {
                    SteamNetworkingMessage_t steamMessage = SteamNetworkingMessage_t.FromIntPtr(receivedMessages[i]);

                    var message = new Message
                    {
                        Data = new byte[steamMessage.m_cbSize],
                        Endpoint = FindKeyByValue(SteamConnections, steamMessage.m_conn)
                    };

                    Marshal.Copy(steamMessage.m_pData, message.Data, 0, steamMessage.m_cbSize);

                    BufferQueue.Enqueue(message);

                    if (_steamOptions.EnableDebug)
                        LogDebug(
                            $"Steam back-end queuing up messages to buffer. Current Message queue: {BufferQueue.Count}");

                    SteamNetworkingMessage_t.Release(receivedMessages[i]);
                }
            }

            return BufferQueue.Count > 0;
        }

        private TK FindKeyByValue<TK, TV>(Dictionary<TK, TV> dict, TV value)
        {
            foreach (KeyValuePair<TK, TV> pair in dict)
            {
                if (EqualityComparer<TV>.Default.Equals(pair.Value, value))
                {
                    return pair.Key;
                }
            }

            return default;
        }

        /// <summary>
        ///     Manager class to control various ways of steam sockets. Different modes are P2P,UDP
        /// </summary>
        /// <param name="options"></param>
        /// <param name="isServer">Is this socket manager running on server or client.</param>
        public SteamSocketManager(SteamOptions options, bool isServer)
        {
            _steamOptions = options;
            _isServer = isServer;

            _onConnectionChange =
                    Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);

            SteamConnections = new Dictionary<IEndPoint, HSteamNetConnection>();

            _steamInitialized = options.InitSteam;
        }

        /// <summary>
        ///     Steam sockets callback for connection status changed.
        /// </summary>
        /// <param name="param"></param>
        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t param)
        {
            ulong clientSteamId = param.m_info.m_identityRemote.GetSteamID64();

            switch (_isServer)
            {
                case true:
                    switch (param.m_info.m_eState)
                    {
                        case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:

                            EResult reason;

                            if ((reason = SteamNetworkingSockets.AcceptConnection(param.m_hConn)) ==
                                EResult.k_EResultOK)
                            {
                                if (_steamOptions.EnableDebug)
                                    LogDebug($"Accepted connection from {clientSteamId}");
                            }
                            else
                            {
                                if (_steamOptions.EnableDebug)
                                    LogDebug($"Connection {clientSteamId} could not be accepted: {reason}");
                            }

                            break;
                        case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:

                            SteamNetworkingSockets.SetConnectionPollGroup(param.m_hConn, _pollGroup);

                            var steamEndpoint = new SteamEndpoint(param.m_info.m_identityRemote.GetSteamID());

                            SteamConnections.Add(steamEndpoint, param.m_hConn);

                            if (_steamOptions.EnableDebug)
                                LogDebug(
                                    $"Client with SteamID {clientSteamId} and connection {param.m_hConn} connected.");

                            break;
                        case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:

                            if (SteamConnections.ContainsValue(param.m_hConn))
                                SteamConnections.Remove(FindKeyByValue(SteamConnections, param.m_hConn));

                            if (_steamOptions.EnableDebug)
                                LogDebug($"Connection closed by peer: {clientSteamId}");

                            break;
                        default:

                            if (_steamOptions.EnableDebug)
                                LogDebug($"Connection {clientSteamId} state changed: {param.m_info.m_eState}");

                            break;
                    }

                    break;
                default:
                    switch (param.m_info.m_eState)
                    {
                        case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:

                            SteamNetworkingSockets.SetConnectionPollGroup(param.m_hConn, _pollGroup);

                            if (_steamOptions.EnableDebug)
                                LogDebug("Connection established.");

                            break;
                        case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:

                            if (_steamOptions.EnableDebug)
                                LogDebug($"Connection was closed by peer: {param.m_hConn}");

                            if (SteamConnections.ContainsValue(param.m_hConn))
                            {
                                if (_steamOptions.EnableDebug)
                                    LogDebug("Found connection in dictionary. Removing connection.");

                                SteamConnections.Remove(FindKeyByValue(SteamConnections, param.m_hConn));
                            }

                            break;
                        default:
                            if (_steamOptions.EnableDebug)
                                LogDebug(
                                    $"Connection state changed: {param.m_info.m_eState} for peer: {param.m_hConn}");
                            break;
                    }

                    break;
            }
        }

        #endregion

        #region Implementation of IDisposable

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            if (_steamOptions.EnableDebug)
                LogDebug("Shutting down socket manager.");

            _onConnectionChange = null;
            SteamNetworkingSockets.DestroyPollGroup(_pollGroup);
            _steamInitialized = false;
            
            if(options.InitSteam)
                SteamAPI.Shutdown();
        }

        #endregion
    }

    internal sealed class SteamSocket : ISocket
    {
        #region Fields

        private readonly bool _isServer;
        private readonly SteamOptions _steamOptions;
        private readonly SteamSocketManager _steamSocketManager;
        private SteamSocketFactory.SteamEndPointWrapper _endPoint;

        #endregion

        #region Class Specific

        public SteamSocket(SteamOptions options, bool isServer)
        {
            if (options.InitSteam)
            {
                if (SteamAPI.RestartAppIfNecessary((AppId_t)480))
                {
                    Application.Quit();
                    return;
                }

                bool initialized = SteamAPI.Init();

                if (!initialized)
                {
                    Debug.LogError(
                        "[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.");

                    return;
                }
            }

            Debug.Log("Starting up FizzySteam Socket...");

            if(options.useSteamRelay)
                SteamNetworkingUtils.InitRelayNetworkAccess();

            _steamOptions = options;
            _isServer = isServer;
            _steamSocketManager = new SteamSocketManager(options, isServer);
        }

        #endregion

        #region Implementation of ISocket

        /// <summary>
        /// Starts listens for data on an endpoint
        /// <para>Used by Server to allow clients to connect</para>
        /// </summary>
        /// <param name="endPoint">the endpoint to listen on</param>
        public void Bind(IEndPoint endPoint)
        {
            switch (_steamOptions.SteamMode)
            {
                case SteamModes.P2P:
                    _steamSocketManager.Socket =
                        SteamNetworkingSockets.CreateListenSocketP2P(0, 0, Array.Empty<SteamNetworkingConfigValue_t>());
                    break;
                case SteamModes.UDP:

                    _endPoint = (SteamSocketFactory.SteamEndPointWrapper)endPoint;

                    var address = new SteamNetworkingIPAddr();
                    address.SetIPv6(((IPEndPoint)_endPoint.inner).Address.GetAddressBytes(), (ushort)((IPEndPoint)_endPoint.inner).Port);

                    _steamSocketManager.Socket =
                        SteamNetworkingSockets.CreateListenSocketIP(ref address, 0,
                            Array.Empty<SteamNetworkingConfigValue_t>());
                    break;
                default:
                    _steamSocketManager.LogDebug("Unknown steam mode. Please check if mode has been supported.", LogType.Warning);

                    throw new NotImplementedException("Unknown steam mode. This mode must not be implemented fully yet.");
            }
        }

        /// <summary>
        /// Sets up Socket ready to send data to endpoint as a client
        /// </summary>
        /// <param name="endPoint"></param>
        public void Connect(IEndPoint endPoint)
        {
            switch (_steamOptions.SteamMode)
            {
                case SteamModes.P2P:

                    var steamEndPoint = (SteamEndpoint)endPoint;
                    var steamIdentity = new SteamNetworkingIdentity();
                    steamIdentity.SetSteamID(steamEndPoint.Address);

                    _steamSocketManager.HoHSteamNetConnection = SteamNetworkingSockets.ConnectP2P(ref steamIdentity, 0, 0, Array.Empty<SteamNetworkingConfigValue_t>());

                    _steamSocketManager.SteamConnections.Add(steamEndPoint, _steamSocketManager.HoHSteamNetConnection);
                    break;
                case SteamModes.UDP:

                    _endPoint = (SteamSocketFactory.SteamEndPointWrapper)endPoint;

                    var address = new SteamNetworkingIPAddr();
                    address.SetIPv6(((IPEndPoint)_endPoint.inner).Address.GetAddressBytes(), (ushort)((IPEndPoint)_endPoint.inner).Port);

                    _steamSocketManager.HoHSteamNetConnection =
                        SteamNetworkingSockets.ConnectByIPAddress(ref address, 0,
                            Array.Empty<SteamNetworkingConfigValue_t>());

                    _steamSocketManager.SteamConnections.Add(_endPoint, _steamSocketManager.HoHSteamNetConnection);
                    break;
                default:
                    Debug.LogWarning("Unknown steam mode. Please check if mode has been supported.");
                    break;
            }
        }

        /// <summary>
        /// Closes the socket, stops receiving messages from other peers
        /// </summary>
        public void Close()
        {
            switch (_isServer)
            {
                case true:
                    SteamNetworkingSockets.CloseListenSocket(_steamSocketManager.Socket);
                    break;
                case false:
                    SteamNetworkingSockets.CloseConnection(_steamSocketManager.HoHSteamNetConnection,
                        (int)ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_App_Generic, null, true);
                    SteamNetworkingSockets.FlushMessagesOnConnection(_steamSocketManager.HoHSteamNetConnection);
                    break;
            }

            _steamSocketManager.Dispose();

            Debug.Log("Shutting down FizzySteam Sockets");
        }

        /// <summary>
        /// Checks if a packet is available 
        /// </summary>
        /// <returns>true if there is atleast 1 packet to read</returns>
        public bool Poll()
        {
            return _steamSocketManager.Update();
        }

        /// <summary>
        /// Gets next packet
        /// <para>Should be called after Poll</para>
        /// <para>
        ///     Implementation should check that incoming packet is within the size of <paramref name="buffer"/>,
        ///     and make sure not to return <paramref name="bytesReceived"/> above that size
        /// </para>
        /// </summary>
        /// <param name="buffer">buffer to write recevived packet into</param>
        /// <param name="endPoint">where packet came from</param>
        /// <returns>length of packet, should not be above <paramref name="buffer"/> length</returns>
        public int Receive(byte[] buffer, out IEndPoint endPoint)
        {
            _steamSocketManager.BufferQueue.TryDequeue(out Message message);

            Buffer.BlockCopy(message.Data, 0, buffer, 0, message.Data.Length);

            endPoint = message.Endpoint;

            if (_steamOptions.EnableDebug)
                _steamSocketManager.LogDebug(
                    $"Message Received From : {endPoint} Successfully. Steamsockets stats BufferQueue: {_steamSocketManager.BufferQueue.Count} Message: {BitConverter.ToString(buffer)}");

            return message.Data.Length;
        }

        /// <summary>
        /// Sends a packet to an endpoint
        /// <para>Implementation should use <paramref name="length"/> because <paramref name="packet"/> is a buffer than may contain data from previous packets</para>
        /// </summary>
        /// <param name="endPoint">where packet is being sent to</param>
        /// <param name="packet">buffer that contains the packet, starting at index 0</param>
        /// <param name="length">length of the packet</param>
        public unsafe void Send(IEndPoint endPoint, byte[] packet, int length)
        {
            fixed (byte* ptr = packet)
            {
                var sendBuffer = (IntPtr)ptr;
                EResult res;

                if (!_steamSocketManager.SteamConnections.TryGetValue(endPoint, out HSteamNetConnection hSConnection))
                {
                    _steamSocketManager.LogDebug($"Cannot find endpoint: {endPoint} in dictionary. Was this connection ever accepted?", LogType.Warning);

                    return;
                }

                if ((res = SteamNetworkingSockets.SendMessageToConnection(hSConnection, sendBuffer, (uint)length,
                        Constants.k_nSteamNetworkingSend_Unreliable, out long successful)) == EResult.k_EResultOK)
                {
                    if (_steamOptions.EnableDebug)
                        _steamSocketManager.LogDebug($"Message was sent successfully");
                }
                else
                {
                    if (_steamOptions.EnableDebug)
                        _steamSocketManager.LogDebug($"Message was not sent successfully with status code: {res}");
                }
            }
        }

        #endregion
    }
}
#endif
