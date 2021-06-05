#region Statements

using System;
using System.Net;
using System.Net.Sockets;
using Mirage.SocketLayer;
using UnityEngine;

#endregion

namespace Mirage.Sockets.FizzySteam
{
    public sealed class SteamSocketFactory : SocketFactory
    {
        #region Fields

        [SerializeField] private SteamOptions _steamOptions;

        #endregion

        #region Class Methods

        /// <summary>
        ///     Check against specific devices to make sure we support it.
        /// </summary>
        private bool IsSteam => Application.platform == RuntimePlatform.LinuxEditor ||
                                Application.platform == RuntimePlatform.LinuxPlayer ||
                                Application.platform == RuntimePlatform.WindowsPlayer ||
                                Application.platform == RuntimePlatform.WindowsEditor ||
                                Application.platform == RuntimePlatform.OSXPlayer ||
                                Application.platform == RuntimePlatform.OSXEditor;

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

        /// <summary>Creates a <see cref="ISocket" /> to be used by <see cref="Peer" /> on the server</summary>
        /// <exception cref="NotSupportedException">Throw when Server is not supported on current platform</exception>
        public override ISocket CreateServerSocket()
        {
            ThrowIfNotSupported();

            return new SteamSocket(_steamOptions, true);
        }

        /// <summary>Creates the <see cref="EndPoint" /> that the Server Socket will bind to</summary>
        public override EndPoint GetBindEndPoint()
        {
            return new IPEndPoint(IPAddress.IPv6Any, _steamOptions.Port);
        }

        /// <summary>Creates a <see cref="ISocket" /> to be used by <see cref="Peer" /> on the client</summary>
        /// <exception cref="NotSupportedException">Throw when Client is not supported on current platform</exception>
        public override ISocket CreateClientSocket()
        {
            ThrowIfNotSupported();

            return new SteamSocket(_steamOptions, false);
        }

        /// <summary>Creates the <see cref="EndPoint" /> that the Client Socket will connect to using the parameter given</summary>
        public override EndPoint GetConnectEndPoint(string address = null, ushort? port = null)
        {
            string addressString = address ?? _steamOptions.Address;
            IPAddress ipAddress = GetAddress(addressString);

            ushort portIn = port ?? _steamOptions.Port;

            return new IPEndPoint(ipAddress, portIn);
        }

        #endregion
    }
}
