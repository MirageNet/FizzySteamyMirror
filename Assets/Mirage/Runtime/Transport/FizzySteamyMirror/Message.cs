using Mirage.SocketLayer;

namespace Mirage.Sockets.FizzySteam
{
    public struct Message
    {
        public byte[] Data;
        public IEndPoint Endpoint;

        public Message(byte[] data, IEndPoint endPoint)
        {
            Data = data;
            Endpoint = endPoint;
        }
    }
}
