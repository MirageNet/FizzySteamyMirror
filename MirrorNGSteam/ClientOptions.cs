#region Statements

using Steamworks;

#endregion

namespace OMN.Scripts.Networking.MirrorNGSteam
{
    public struct ClientOptions
    {
        public bool AllowSteamRelay;
        public int ConnectionTimeOut;
        public CSteamID ConnectionAddress;
    }
}