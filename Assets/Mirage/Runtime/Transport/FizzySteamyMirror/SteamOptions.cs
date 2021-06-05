#region Statements

using System;
using UnityEngine;

#endregion

namespace Mirage.Sockets.FizzySteam
{
    [Serializable]
    public class SteamOptions
    {
        [Tooltip("Enable Debug Mode")] public bool EnableDebug = false;
        [Tooltip("Which type of steam mode do you want to use.")] public SteamModes SteamMode = SteamModes.UDP;
        [Tooltip("Enter address of the ip of server connect to or the steam user id to if in p2p mode.")] public string Address = "localhost";
        [Tooltip("Only used in udp mode.")] public ushort Port = 7777;
        [Range(1,512), Tooltip("Number of messages we want to poll steam for at once time.")] public int MaxMessagesPolling = 256;
    }

    public enum SteamModes : byte
    {
        P2P = 0,
        UDP = 1,
        SDR = 2
    }
}
