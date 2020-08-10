#region Statements

using Steamworks;

#endregion

namespace Mirror.FizzySteam
{
    public struct Message
    {
        public readonly CSteamID steamId;
        public readonly InternalMessages eventType;
        public readonly byte[] data;

        public Message(CSteamID steamId, InternalMessages eventType, byte[] data)
        {
            this.steamId = steamId;
            this.eventType = eventType;
            this.data = data;
        }
    }
}