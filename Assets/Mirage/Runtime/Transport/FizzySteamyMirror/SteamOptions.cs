#region Statements

using Steamworks;

#endregion

namespace Mirage.FizzySteam
{
    public struct SteamOptions
    {
        public bool AllowSteamRelay;
        public int MaxConnections;
        public int ConnectionTimeOut;
        public CSteamID ConnectionAddress;
        public EP2PSend[] Channels;
    }
}