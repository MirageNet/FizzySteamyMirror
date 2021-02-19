#region Statements

using Steamworks;

#endregion

namespace Mirage.FizzySteam
{
    public struct Message
    {
        public readonly CSteamID steamId;
        public readonly InternalMessages eventType;
        public readonly byte[] data;
        public int Channel;

        public Message(CSteamID steamId, InternalMessages eventType, byte[] data, int channel)
        {
            this.steamId = steamId;
            this.eventType = eventType;
            this.data = data;
            this.Channel = channel;
        }
    }
}