using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Mirror.FizzySteam
{
    public class SteamConnection : IConnection
    {
        SteamServer server;
        SteamServerOptions serverOptions;
        SteamClient client;
        ClientOptions clientOptions;

        public SteamConnection(SteamServerOptions options, SteamServer server)
        {
            this.server = server;
            serverOptions = options;
        }

        public SteamConnection(ClientOptions options, SteamClient client)
        {
            this.client = client;
            clientOptions = options;
        }

        public void Disconnect()
        {
            if (server != null)
            {
                server.Disconnect();
            }
            if (client != null)
            {
                client.Disconnect();
            }
        }

        public EndPoint GetEndPointAddress()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            if (server != null)
            {
                return await server.ReceiveAsync(buffer);
            }
            if (client != null)
            {
                return await client.ReceiveAsync(buffer);
            }
            return false;
        }

        public async Task SendAsync(ArraySegment<byte> data)
        {
            if (server != null)
            {
                await server.SendAsync(data);
            }
            if (client != null)
            {
                await client.SendAsync(data);
            }
        }
    }
}
