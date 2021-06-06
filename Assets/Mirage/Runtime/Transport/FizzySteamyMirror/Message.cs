using System.Net;
using Steamworks;

namespace Mirage.Sockets.FizzySteam
{
    public struct Message
    {
        public byte[] Data;
        public EndPoint Endpoint;

        public Message(byte[] data, EndPoint endPoint)
        {
            Data = data;
            Endpoint = endPoint;
        }
    }
}
