#if !DISABLESTEAMWORKS
#region Statements

using System;
using UnityEngine;

#endregion

namespace Mirage.Sockets.FizzySteam
{
    [Serializable]
    public class SteamOptions
    {
        [Header("Steam Initialization Options")]

        [Tooltip("Please assign this to your app id once you have none. Default is demo space wars by steam.")]
        public uint AppID = 480;

        [Tooltip("Which type of steam mode do you want to use.")]
        public SteamModes SteamMode = SteamModes.UDP;

        [Tooltip("Use steam relay system?")]
        public bool useSteamRelay = true;

        [Tooltip("Set this to false if you want to initialize the SteamClient yourself.")]
        public bool InitSteam = true;

        [Tooltip("Should steam sockets control callback functionality.?")]
        public bool ControlCallbackRunning = true;

        [Header("Debug Options")]

        [Tooltip("Enable Debug Mode")]
        public bool EnableDebug = false;

        [Header("Network Settings")]

        [Range(1, 512), Tooltip("Number of messages we want to poll steam for at once time.")]
        public int MaxMessagesPolling = 256;

        [Tooltip("Enter address of the ip of server connect to or the steam user id to if in p2p mode.")]
        public string Address = "localhost";

        [Tooltip("Only used in udp mode.")]
        public ushort Port = 7777;
    }

    public enum SteamModes : byte
    {
        P2P = 0,
        UDP = 1,
        //SDR = 2
    }
}
#endif
