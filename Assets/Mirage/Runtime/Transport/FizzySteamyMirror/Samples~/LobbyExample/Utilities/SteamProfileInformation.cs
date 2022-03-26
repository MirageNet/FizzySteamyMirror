using Steamworks;
using UnityEngine;

namespace Mirage.Sockets.FizzySteam
{
    public class SteamProfileInformation
    {
        public CSteamID Id { get; }
        public string Name => SteamFriends.GetFriendPersonaName(Id);
        public Texture2D Avatar { get; }
        
        public SteamProfileInformation(CSteamID id, Texture2D avatar)
        {
            Id = id;
            Avatar = avatar;
        }
    }
}