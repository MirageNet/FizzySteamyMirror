using UnityEngine;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OMN.Scripts.Managers;

namespace Mirror.FizzySteam
{
    [HelpURL("https://github.com/Chykary/FizzySteamyMirror")]
    public class FizzySteamyMirror : Transport
    {
#if !MirrorNG
        private const string STEAM_SCHEME = "steam";
#else
        public override IEnumerable<string> Scheme => new[] {"steam"};
#endif

        private Client client;
        private Server server;

        private Common activeNode;

        [SerializeField]
        public EP2PSend[] Channels = new EP2PSend[2] { EP2PSend.k_EP2PSendReliable, EP2PSend.k_EP2PSendUnreliable };

        [Tooltip("Timeout for connecting in seconds.")]
        public int Timeout = 25;

        [Tooltip("Allow or disallow P2P connections to fall back to being relayed through the Steam servers if a direct connection or NAT-traversal cannot be established.")]
        public bool AllowSteamRelay = true;

        private void Awake()
        {
            Debug.Assert(Channels != null && Channels.Length > 0, "No channel configured for FizzySteamMirror.");
        }

        private void LateUpdate() => activeNode?.ReceiveData();

#if !MirrorNG
        public override bool ClientConnected() => ClientActive() && client.Connected;
#else
        public bool ClientConnected() => ClientActive() && client.Connected;
#endif
#if !MirrorNG
        public override void ClientConnect(string address)
#else
        public  async Task<IConnection> ClientConnect(string address)
#endif
        {
            if (!SteamworksManager.Instance.Initialized)
            {
                Debug.LogError("SteamWorks not initialized. Client could not be started.");
#if !MirrorNG
                OnClientDisconnected?.Invoke();
#else
                Disconnect();
#endif
                return null;
            }

            if (ServerActive())
            {
                Debug.LogError("Transport already running as server!");
                return null;
            }

            if (!ClientActive())
            {
                Debug.Log($"Starting client, target address {address}.");

                SteamNetworking.AllowP2PPacketRelay(AllowSteamRelay);
                client = Client.CreateClient(this, address);
                activeNode = client;
            }
            else
            {
                Debug.LogError("Client already running!");
            }

            return client;
        }

#if !MirrorNG
        public override void ClientConnect(Uri uri)
#else
        public override async Task<IConnection> ConnectAsync(Uri uri)
#endif
        {
#if !MirrorNG
            if (uri.Scheme != STEAM_SCHEME)
                throw new ArgumentException($"Invalid url {uri}, use {STEAM_SCHEME}://SteamID instead", nameof(uri));

            ClientConnect(uri.Host);
#else
            //if (uri.Scheme != Scheme.GetEnumerator().Current)
            //    throw new ArgumentException($"Invalid url {uri}, use {Scheme}://SteamID instead", nameof(uri));

            return await ClientConnect(uri.Host);
#endif
        }

#if MirrorNG
        public override async Task<IConnection> AcceptAsync()
        {
            // Steam has no way to do async accepting of connections
            // so we create a fake loop to keep server running.
            try
            {
                while (ServerActive())
                {
                    var t = await QueuedConnectionsAsync();
                    
                    if (t != null)
                        return t;

                    await Task.Delay(1000);
                }

                return null;
            }
            catch (ObjectDisposedException)
            {
                // expected,  the connection was closed
                return null;
            }
        }

        private Task<Client> QueuedConnectionsAsync()
        {
            if (server._queuedConnections.Count <= 0)  return Task.FromResult<Client>(null);

            var id = server._queuedConnections.Dequeue();

            return Task.FromResult(id == CSteamID.Nil ? null : Client.CreateClient(this, id));
        }
#endif

#if !MirrorNG
        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            clientPoolData = new byte[segment.Count];

            Array.Copy(segment.Array, segment.Offset, clientPoolData, 0, segment.Count);
            return client.Send(clientPoolData, channelId);
        }

        public override void ClientDisconnect()
        {
            if (ClientActive())
            {
                Shutdown();
            }
        }
#endif
        public bool ClientActive() => client != null;

#if !MirrorNG
        public override bool ServerActive() => server != null;
#else
        public bool ServerActive() => server != null;
#endif
#if !MirrorNG
        public override void ServerStart()
#else
        public void ServerStart()
#endif
        {
            if (!SteamworksManager.Instance.Initialized)
            {
                Debug.LogError("SteamWorks not initialized. Server could not be started.");
                return;
            }

            if (ClientActive())
            {
                Debug.LogError("Transport already running as client!");
                return;
            }

            if (!ServerActive())
            {
                Debug.Log("Starting server.");
                SteamNetworking.AllowP2PPacketRelay(AllowSteamRelay);
                server = Server.CreateServer(this, 4);
                activeNode = server;
            }
            else
            {
                Debug.LogError("Server already started!");
            }
        }

#if !MirrorNG
        public override Uri ServerUri()
#else
        public override IEnumerable<Uri> ServerUri()
#endif
        {
            var steamBuilder = new UriBuilder
            {
                Scheme = "steam",
                Host = SteamUser.GetSteamID().m_SteamID.ToString()
            };

#if !MirrorNG
            return steamBuilder.Uri;
#else
            return new[] {steamBuilder.Uri};
#endif
        }

#if !MirrorNG
        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            serverPoolData = new byte[segment.Count];

            Array.Copy(segment.Array, segment.Offset, serverPoolData, 0, segment.Count);

            return ServerActive() && server.SendAll(connectionIds, serverPoolData, channelId);
        }

        public override bool ServerDisconnect(int connectionId) => ServerActive() && server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId) => ServerActive() ? server.ServerGetClientAddress(connectionId) : string.Empty;
        public override void ServerStop()
        {
            if (ServerActive())
            {
                Shutdown();
            }
        }
#endif

#if MirrorNG
        public override Task ListenAsync()
        {
            ServerStart();

            return Task.CompletedTask;
        }
#endif

#if !MirrorNG
        public override void Shutdown()
#else
        public override void Disconnect()
#endif
        {
            server?.Shutdown();
            client?.Disconnect();

            server = null;
            client = null;
            activeNode = null;

#if UNITY_EDITOR
            Debug.Log("Transport shut down.");
#endif
        }

#if !MirrorNG
        public override int GetMaxPacketSize(int channelId)
#else
        /// <summary>
        ///     Not used atm.
        /// </summary>
        /// <param name="channelId"></param>
        /// <returns></returns>
        public int GetMaxPacketSize(int channelId)
#endif
        {
            switch (Channels[channelId])
            {
                case EP2PSend.k_EP2PSendUnreliable:
                case EP2PSend.k_EP2PSendUnreliableNoDelay:
                    return 1200;
                case EP2PSend.k_EP2PSendReliable:
                case EP2PSend.k_EP2PSendReliableWithBuffering:
                    return 1048576;
                default:
                    throw new NotSupportedException();
            }
        }

#if !MirrorNG
        public override bool Available(
#else
        public override bool Supported
#endif
        {
#if MirrorNG
            get
            {
#endif
                try
                {
                    return SteamworksManager.Instance.Initialized;
                }
                catch
                {
                    return false;
                }
#if MirrorNG
            }
#endif
        }

        private void OnDestroy()
        {
            if (activeNode != null)
            {
#if !MirrorNG
                Shutdown();
#else
                Disconnect();
#endif
            }
        }
    }
}

            {
                File.WriteAllText(fileName, SteamAppID.ToString());
                Debug.Log($"New {fileName} written with SteamAppID {SteamAppID}");
            }

            Debug.Assert(Channels != null && Channels.Length > 0, "No channel configured for FizzySteamMirror.");

            Invoke(nameof(FetchSteamID), 1f);
        }

        private void FetchSteamID()
        {
            if (SteamManager.Initialized)
            {
                SteamUserID = SteamUser.GetSteamID().m_SteamID;
            }
        }

        private void LateUpdate()
        {
            if (activeNode != null)
            {
                activeNode.ReceiveData();
                activeNode.ReceiveInternal();
            }
        }

        public override bool ClientConnected() => ClientActive() && client.Connected;
        public override void ClientConnect(string address)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamWorks not initialized. Client could not be started.");
                return;
            }

            FetchSteamID();

            if (ServerActive())
            {
                Debug.LogError("Transport already running as server!");
                return;
            }

            if (!ClientActive())
            {
                Debug.Log($"Starting client, target address {address}.");

                SteamNetworking.AllowP2PPacketRelay(AllowSteamRelay);
                client = Client.CreateClient(this, address);
                activeNode = client;
            }
            else
            {
                Debug.LogError("Client already running!");
            }
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != STEAM_SCHEME)
                throw new ArgumentException($"Invalid url {uri}, use {STEAM_SCHEME}://SteamID instead", nameof(uri));

            ClientConnect(uri.Host);
        }

        public override bool ClientSend(int channelId, ArraySegment<byte> segment) => client.Send(segment.Array, channelId);
        public override void ClientDisconnect()
        {
            if (ClientActive())
            {
                Shutdown();
            }
        }
        public bool ClientActive() => client != null;


        public override bool ServerActive() => server != null;
        public override void ServerStart()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamWorks not initialized. Server could not be started.");
                return;
            }

            FetchSteamID();

            if (ClientActive())
            {
                Debug.LogError("Transport already running as client!");
                return;
            }

            if (!ServerActive())
            {
                Debug.Log("Starting server.");
                SteamNetworking.AllowP2PPacketRelay(AllowSteamRelay);
                server = Server.CreateServer(this, NetworkManager.singleton.maxConnections);
                activeNode = server;
            }
            else
            {
                Debug.LogError("Server already started!");
            }
        }

        public override Uri ServerUri()
        {
            var steamBuilder = new UriBuilder
            {
                Scheme = STEAM_SCHEME,
                Host = SteamUser.GetSteamID().m_SteamID.ToString()
            };

            return steamBuilder.Uri;
        }

        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment) => ServerActive() && server.SendAll(connectionIds, segment.Array, channelId);
        public override bool ServerDisconnect(int connectionId) => ServerActive() && server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId) => ServerActive() ? server.ServerGetClientAddress(connectionId) : string.Empty;
        public override void ServerStop()
        {
            if (ServerActive())
            {
                Shutdown();
            }
        }

        public override void Shutdown()
        {
            server?.Shutdown();
            client?.Disconnect();

            server = null;
            client = null;
            activeNode = null;
            Debug.Log("Transport shut down.");
        }

        public override int GetMaxPacketSize(int channelId)
        {
            channelId = Math.Min(channelId, Channels.Length - 1);

            EP2PSend sendMethod = Channels[channelId];
            switch (sendMethod)
            {
                case EP2PSend.k_EP2PSendUnreliable:
                case EP2PSend.k_EP2PSendUnreliableNoDelay:
                    return 1200;
                case EP2PSend.k_EP2PSendReliable:
                case EP2PSend.k_EP2PSendReliableWithBuffering:
                    return 1048576;
                default:
                    throw new NotSupportedException();
            }
        }

        public override bool Available()
        {
            try
            {
                return SteamManager.Initialized;
            }
            catch
            {
                return false;
            }
        }

        private void OnDestroy()
        {
            if (activeNode != null)
            {
                Shutdown();
            }
        }
    }
}
