#region Statements

using Steamworks;

#endregion

namespace Mirror.FizzySteam
{
    public struct ClientOptions
    {
        public bool AllowSteamRelay;
        public int ConnectionTimeOut;
        public CSteamID ConnectionAddress;
    }
}
