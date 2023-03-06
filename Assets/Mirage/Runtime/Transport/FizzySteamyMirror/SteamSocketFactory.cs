#if !DISABLESTEAMWORKS

#region Statements

using System;
using System.Net;
using System.Net.Sockets;
using Mirage.SocketLayer;
using Steamworks;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace Mirage.Sockets.FizzySteam
{
    public sealed class SteamSocketFactory : SocketFactory
    {
        #region Fields

        [FormerlySerializedAs("_steamOptions")]
        public SteamOptions SteamOptions = new SteamOptions();

        [NonSerialized] public bool SteamInitialized;

        public Action<bool> OnSteamInitialized;

        #endregion

        #region Unity Methods

        private void Start()
        {
            if (SteamOptions.InitSteam)
            {
                Init();
            }
        }

        private void OnDestroy()
        {
            if (SteamInitialized && SteamOptions.InitSteam)
            {
#if UNITY_SERVER
                GameServer.Shutdown();
#else
                SteamAPI.Shutdown();
#endif
            }
        }

        private void Update()
        {
            if (SteamOptions.ControlCallbackRunning && SteamInitialized)
            {
#if UNITY_SERVER
                GameServer.RunCallbacks();
#else
                SteamAPI.RunCallbacks();
#endif
            }
        }

        #endregion

        #region Class Methods

        /// <summary>
        /// Initializes the Steam API.
        /// </summary>
        /// <returns>Returns true if initialization was successful. False if already initialized or the initialization could not be completed.</returns>
        public bool Init()
        {
            if (SteamInitialized)
                return false;


#if UNITY_SERVER
            SteamInitialized = Steamworks.GameServer.Init(0, SteamOptions.Port, SteamOptions.QueryPort,
                EServerMode.eServerModeNoAuthentication, SteamOptions.Version);
#else

            if (SteamAPI.RestartAppIfNecessary((AppId_t)SteamOptions.AppID))
            {
                Application.Quit();
                return false;
            }

            SteamInitialized = SteamAPI.Init();
#endif


            OnSteamInitialized?.Invoke(SteamInitialized);

            if (!SteamInitialized)
            {
                Debug.LogError(
                    "[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.");

                return false;
            }

            return true;
        }

        /// <summary>
        ///     Check against specific devices to make sure we support it.
        /// </summary>
#if UNITY_SERVER
        private bool IsSteam =>
            Application.platform == RuntimePlatform.LinuxServer ||
            Application.platform == RuntimePlatform.WindowsServer ||
            Application.platform == RuntimePlatform.OSXServer;
#else
        private bool IsSteam => Application.platform == RuntimePlatform.LinuxEditor ||
                                Application.platform == RuntimePlatform.LinuxPlayer ||
                                Application.platform == RuntimePlatform.WindowsPlayer ||
                                Application.platform == RuntimePlatform.WindowsEditor ||
                                Application.platform == RuntimePlatform.OSXPlayer ||
                                Application.platform == RuntimePlatform.OSXEditor;
#endif


        /// <summary>
        ///     Make sure device is supported.
        /// </summary>
        private void ThrowIfNotSupported()
        {
            if (!IsSteam)
            {
                throw new NotSupportedException("Steam Socket can only be run on devices that supports steam.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addressString"></param>
        /// <returns></returns>
        private IPAddress GetAddress(string addressString)
        {
            if (IPAddress.TryParse(addressString, out IPAddress address))
            {
                return address;
            }

            IPAddress[] results = Dns.GetHostAddresses(addressString);

            if (results.Length == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            return results[0];
        }

        #endregion

        #region Overrides of SocketFactory

        /// <summary>Max size for packets sent to or received from Socket
        /// <para>Called once when Sockets are created</para></summary>
        public override int MaxPacketSize => 1200;

        /// <summary>Creates a <see cref="ISocket" /> to be used by <see cref="Peer" /> on the server</summary>
        /// <exception cref="NotSupportedException">Throw when Server is not supported on current platform</exception>
        public override ISocket CreateServerSocket()
        {
            ThrowIfNotSupported();

            return new SteamSocket(SteamOptions, true);
        }

        /// <summary>Creates the <see cref="EndPoint" /> that the Server Socket will bind to</summary>
        public override IEndPoint GetBindEndPoint()
        {
            return new SteamEndPointWrapper(new IPEndPoint(IPAddress.IPv6Any, SteamOptions.Port));
        }

        /// <summary>Creates a <see cref="ISocket" /> to be used by <see cref="Peer" /> on the client</summary>
        /// <exception cref="NotSupportedException">Throw when Client is not supported on current platform</exception>
        public override ISocket CreateClientSocket()
        {
            ThrowIfNotSupported();

            return new SteamSocket(SteamOptions, false);
        }

        /// <summary>Creates the <see cref="EndPoint" /> that the Client Socket will connect to using the parameter given</summary>
        public override IEndPoint GetConnectEndPoint(string address = null, ushort? port = null)
        {
            if (SteamOptions.SteamMode == SteamModes.P2P)
                return new SteamEndpoint(new CSteamID(ulong.Parse(address ?? SteamOptions.Address)));

            string addressString = address ?? SteamOptions.Address;
            IPAddress ipAddress = GetAddress(addressString);

            ushort portIn = port ?? SteamOptions.Port;

            return new SteamEndPointWrapper(new IPEndPoint(ipAddress, portIn));
        }

        public class SteamEndPointWrapper : IEndPoint
        {
            public EndPoint inner;

            public SteamEndPointWrapper(EndPoint endPoint)
            {
                inner = endPoint;
            }

            public override bool Equals(object obj)
            {
                if (obj is SteamEndPointWrapper other)
                {
                    return inner.Equals(other.inner);
                }

                return false;
            }

            public override int GetHashCode()
            {
                return inner.GetHashCode();
            }

            public override string ToString()
            {
                return inner.ToString();
            }

            IEndPoint IEndPoint.CreateCopy()
            {
                // copy the inner endpoint
                EndPoint copy = inner.Create(inner.Serialize());
                return new SteamEndPointWrapper(copy);
            }
        }

        #endregion
    }
}
#endif