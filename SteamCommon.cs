#region Statements

using Mirror.FizzySteam;
using Steamworks;

#endregion

namespace Mirror.FizzySteam
{
    public abstract class SteamCommon
    {
        /// <summary>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="type"></param>
        internal abstract bool SteamSend(CSteamID target, InternalMessages type);

        /// <summary>
        ///     Check to see if we have received any data from steam users.
        /// </summary>
        /// <param name="clientSteamID">Returns back the steam id of users who sent message.</param>
        /// <param name="receiveBuffer">The data that was sent to use.</param>
        /// <param name="channel">The channel the data was sent on.</param>
        /// <returns></returns>
        internal bool DataReceivedCheck(out CSteamID clientSteamID, out byte[] receiveBuffer, int channel)
        {
            if (!SteamworksManager.Instance.Initialized)
            {
                receiveBuffer = null;
                clientSteamID = CSteamID.Nil;
                return false;
            }

            if (SteamNetworking.IsP2PPacketAvailable(out var packetSize, channel))
            {
                receiveBuffer = new byte[packetSize];
                return SteamNetworking.ReadP2PPacket(receiveBuffer, packetSize, out _, out clientSteamID, channel);
            }

            receiveBuffer = null;
            clientSteamID = CSteamID.Nil;
            return false;
        }
    }
}