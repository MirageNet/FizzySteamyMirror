using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using UnityEngine.Events;
using static Mirage.Sockets.FizzySteam.NetworkPrinter;

namespace Mirage.Sockets.FizzySteam
{
    /// <summary>
    /// This class may ONLY be used to handle lobby creation + joining + handling (eg. lobby invitation overlay, getting lobby member avatars, etc...).
    /// Everything else (eg. starting/joining server, changing scene, etc...) MUST be handled via the supplied callbacks.
    /// This steam lobby implementation offers functionality to create and join a lobby just by a specified name.
    /// Additionally, lobby members may invite others via the steam overlay (available only on builds deployed on steam).
    /// </summary>
    /// <remarks>
    /// This component should be instantiated on game startup and exist ONLY ONCE during all of its lifetime, because lobby information should remain persistent.
    /// The reason why it should exist precisely on game startup is because you probably want to call <see cref="OnSteamInitialised"/> after steam initialisation to
    /// be able to join a lobby after the game was started via a friend invite.
    /// If you do not understand what is happening here, please consult the steamworks documentation (https://partner.steamgames.com) before asking.
    /// </remarks>
    [DisallowMultipleComponent]
    public class SteamLobbyManager : MonoBehaviour
    {
        protected enum SteamLobbyStatus
        {
            Idle,
            Creating,
            Joining,
            Requesting
        }

        #region Callbacks
        
        protected Callback<LobbyMatchList_t> OnCallbackLobbyListReceived;
        protected Callback<LobbyCreated_t> OnCallbackLobbyCreated;
        protected Callback<LobbyEnter_t> OnCallbackLobbyEntered;
        protected Callback<LobbyChatUpdate_t> OnCallbackPlayerJoinedOrLeft;
        protected Callback<GameOverlayActivated_t> OnCallbackGameOverlayActivated;
        protected Callback<GameLobbyJoinRequested_t> OnCallbackInviteAccepted;
        protected Callback<AvatarImageLoaded_t> OnCallbackAvatarImageLoaded;

        /// <summary>Contains the <see cref="CSteamID"/> of the user that left the steam lobby (excluding the local user; see <see cref="onLocalUserLeftSteamLobby"/>).</summary>
        public UnityEvent<CSteamID> onUserLeftSteamLobby;
        /// <summary>Is invoked when the local user left a steam lobby.</summary>
        public UnityEvent onLocalUserLeftSteamLobby;
        /// <summary>Contains the <see cref="CSteamID"/> of the user that joined the steam lobby (excluding the local user; see <see cref="onLocalUserJoinedSteamLobby"/>).</summary>
        public UnityEvent<CSteamID> onUserJoinedSteamLobby;
        /// <summary>Is invoked when the local user joined a steam lobby.</summary>
        public UnityEvent onLocalUserJoinedSteamLobby;
        /// <summary>Contains <see langword="true"/>, when the steam overlay is currently active; otherwise, <see langword="false"/>.</summary>
        public UnityEvent<bool> onSteamOverlayToggled;
        /// <summary>Is invoked when a steam lobby was created successfully.</summary>
        public UnityEvent onLobbyCreated;
        /// <summary>Is invoked when steam failed to create a lobby.</summary>
        /// <remarks>
        /// This event can only be invoked after <see cref="CreateLobby"/> returned <see langword="true"/>.
        /// <see cref="CreateLobby"/> does NOT invoke it because this event is only meant for errors related to steam.
        /// </remarks>
        public UnityEvent onLobbyCreationFailed;
        /// <summary>Is invoked when a request to join a lobby, either by name or upon invitation, failed.</summary>
        public UnityEvent onLobbyJoinRequestFailed;
        /// <summary>Is invoked once <see cref="RequestSteamUserInformation"/> finished loading the requested user's steam profile picture.</summary>
        public UnityEvent<SteamProfileInformation> onSteamUserInformationReceived;

        #endregion

        #region Lobby Data
        
        protected const string LobbyDataName = "name";

        protected CSteamID LobbyId;
        public CSteamID Owner { get; protected set; }
        protected readonly Dictionary<CSteamID, string> ClientsInLobby = new(4);

        protected SteamLobbyStatus Status;
        
        public virtual bool IsInLobby => LobbyId != CSteamID.Nil;
        public virtual bool IsLobbyOwner => SteamUser.GetSteamID() == Owner;
        public virtual string OwnerAddress => Owner.ToString();
        
        /// <summary>Is only set when creating, joining, or inside a lobby; otherwise, null.</summary>
        public virtual string LobbyName { get; protected set; }

        public virtual Dictionary<CSteamID, string> GetLobbyMembersWithNames() => ClientsInLobby;

        #endregion
        
        #region Setup/Teardown

        protected virtual void Awake()
        {
            RegisterSteamCallbacks();
            GetComponent<SteamSocketFactory>().OnSteamInitialized += OnSteamInitialised;
        }

        protected virtual void OnSteamInitialised(bool success)
        {
            if (!success) return;
            // Request profile picture of self on start for UI elements and caching (in case UI elements are setup earlier than steam initialisation).
            RequestSteamUserInformation(SteamUser.GetSteamID());
            // Join on startup if started via steam invitation.
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "+connect_lobby")
                {
                    Log($"Connecting to lobby {args[i + 1]} via invitation...");
                    SteamMatchmaking.JoinLobby((CSteamID)ulong.Parse(args[i + 1]));
                    Status = SteamLobbyStatus.Joining;
                }
            }
            GetComponent<SteamSocketFactory>().OnSteamInitialized -= OnSteamInitialised;
        }

        protected virtual void OnApplicationQuit()
        {
            UnregisterSteamCallbacks();
            LeaveLobbyInternal();
        }

        protected virtual void RegisterSteamCallbacks()
        {
            OnCallbackLobbyListReceived = Callback<LobbyMatchList_t>.Create(OnLobbyListReceived);
            OnCallbackLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            OnCallbackLobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            OnCallbackPlayerJoinedOrLeft = Callback<LobbyChatUpdate_t>.Create(OnPlayerJoinedOrLeft);
            OnCallbackGameOverlayActivated = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
            OnCallbackInviteAccepted = Callback<GameLobbyJoinRequested_t>.Create(OnInvitationAccepted);
            OnCallbackAvatarImageLoaded = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
        }

        protected virtual void UnregisterSteamCallbacks()
        {
            OnCallbackLobbyListReceived?.Dispose();
            OnCallbackLobbyListReceived = null;
            OnCallbackLobbyCreated?.Dispose();
            OnCallbackLobbyCreated = null;
            OnCallbackLobbyEntered?.Dispose();
            OnCallbackLobbyEntered = null;
            OnCallbackPlayerJoinedOrLeft?.Dispose();
            OnCallbackPlayerJoinedOrLeft = null;
            OnCallbackGameOverlayActivated?.Dispose();
            OnCallbackGameOverlayActivated = null;
            OnCallbackInviteAccepted?.Dispose();
            OnCallbackInviteAccepted = null;
            OnCallbackAvatarImageLoaded?.Dispose();
            OnCallbackAvatarImageLoaded = null;
        }
        
        #endregion
        
        #region Public interaction methods

        /// <summary>
        /// Requests steam to create a lobby.
        /// </summary>
        /// <param name="maxConnections">The maximally allowed number of users in the lobby. Must be bigger than zero!</param>
        /// <param name="lobbyName">The name under which your lobby is publicly joinable.</param>
        /// <returns><see langword="true"/>, when the steam API call to create a lobby was invoked; otherwise, <see langword="false"/>.</returns>
        public virtual bool CreateLobby(int maxConnections, string lobbyName)
        {
            if (Status != SteamLobbyStatus.Idle)
            {
                Warn($"Can not create lobby because handler is busy '{Status.ToString()}'.");
                return false;
            }

            if (IsInLobby)
            {
                Warn("Can not create a lobby because the user is already in another lobby.");
                return false;
            }
            
            if (maxConnections <= 0)
            {
                Warn("The number of connections for a Steam lobby must be greater than 0! Setting to the default 4.");
                maxConnections = 4;
            }

            if (string.IsNullOrEmpty(lobbyName))
            {
                Warn("The steam lobby name must not be empty!");
                return false;
            }

            Status = SteamLobbyStatus.Creating;
            LobbyName = lobbyName.ToLower();
            Log("Creating lobby...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxConnections);
            return true;
        }
        /// <summary>Leaves the current lobby.</summary>
        /// <remarks>The lobby will be left immediately, meaning the local user will not receive any more lobby callbacks afterwards.</remarks>
        /// <returns><see langword="true"/>, when the current user was in a steam lobby; otherwise, <see langword="false"/>.</returns>
        public virtual bool LeaveLobby()
        {
            if (!LeaveLobbyInternal())
            {
                Warn("User is not in a steam lobby but tried leaving.");
                return false;
            }
            
            return true;
        }

        /// <inheritdoc cref="LeaveLobby"/>
        protected virtual bool LeaveLobbyInternal()
        {
            if (IsInLobby)
            {
                Log("Leaving steam lobby...");
                ClientsInLobby.Clear();
                SteamMatchmaking.LeaveLobby(LobbyId);
                Owner = CSteamID.Nil;
                LobbyId = CSteamID.Nil;
                Status = SteamLobbyStatus.Idle;
                LobbyName = null;
                onLocalUserLeftSteamLobby?.Invoke();
                Log("Steam lobby left.");
                return true;
            }

            return false;
        }
        
        /// <summary>Opens the steam overlay dedicated to inviting friends to join the currently active steam lobby.</summary>
        /// <remarks>This ONLY works (reliably) when used from a game started via steam. This method basically does nothing when used with a build that is not deployed on steam.</remarks>
        /// <returns><see langword="true"/>, when the current user is in a steam lobby; otherwise, <see langword="false"/>.</returns>
        public virtual bool OpenInvitationOverlay()
        {
            if (!IsInLobby)
            {
                Warn("Invitation overlay could not be opened because there is no steam lobby.");
                return false;
            }
            Log("Opening steam friend invitation overlay...");
            SteamFriends.ActivateGameOverlayInviteDialog(LobbyId);
            return true;
        }

        /// <summary>
        /// Requests a steam user's <see cref="SteamProfileInformation"/>.
        /// </summary>
        /// <param name="userId">The user's identifier needed to request their <see cref="SteamProfileInformation"/>.</param>
        /// <remarks><see cref="onSteamUserInformationReceived"/> will contain the requested <see cref="SteamProfileInformation"/> whose <see cref="SteamProfileInformation.Avatar"/> can be <see langword="null"/> if the user has not set a profile picture or the image is unavailable.</remarks>
        public virtual void RequestSteamUserInformation(CSteamID userId)
        {
            var imageHandle = SteamFriends.GetLargeFriendAvatar(userId);
            switch (imageHandle)
            {
                case -1:
                    // Await steam callback.
                    Log($"Awaiting profile picture callback for {SteamFriends.GetFriendPersonaName(userId)}.");
                    return;
                case 0:
                    // No avatar set for user -> manually fire event to notify requesting user.
                    Log($"No profile picture available for {SteamFriends.GetFriendPersonaName(userId)}.");
                    onSteamUserInformationReceived?.Invoke(new SteamProfileInformation(userId, null));
                    return;
                default:
                    if (!TryGetCachedProfilePicture(imageHandle, out var avatar)) goto case 0;
                    // Avatar already cached -> manually fire the event containing it.
                    Log($"Cached profile picture available for {SteamFriends.GetFriendPersonaName(userId)}.");
                    onSteamUserInformationReceived?.Invoke(new SteamProfileInformation(userId, avatar));
                    return;
            }
        }
        
        public virtual bool RequestJoinLobby(string lobbyName)
        {
            if (LobbyId != CSteamID.Nil)
            {
                Warn("User requested to join a lobby while being in a lobby. Currently this is not allowed.");
                return false;
            }
            
            if (string.IsNullOrEmpty(lobbyName))
            {
                Warn("The steam lobby name must not be empty!");
                return false;
            }

            if (Status != SteamLobbyStatus.Idle)
            {
                Warn($"Can not request to join lobby because handler is busy '{Status.ToString()}'.");
                return false;
            }

            Status = SteamLobbyStatus.Requesting;
            LobbyName = lobbyName.ToLower();
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyDataName, LobbyName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.RequestLobbyList();
            return true;
        }
        
        #endregion
        
        #region Steam event handling
        
        /// <summary>
        /// Is called after creating a lobby.
        /// </summary>
        /// <remarks>Just because this method is called does not necessarily mean the lobby was successfully created.</remarks>
        protected virtual void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK || result.m_ulSteamIDLobby == 0)
            {
                Warn($"Lobby creation failed with {result.m_eResult.ToString()}");
                onLobbyCreationFailed?.Invoke();
                return;
            }
            
            Owner = SteamUser.GetSteamID();
            LobbyId = (CSteamID)result.m_ulSteamIDLobby;
            // Set lobby data.
            // Lobby is already public.
            SteamMatchmaking.SetLobbyJoinable((CSteamID)result.m_ulSteamIDLobby, true);
            SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, LobbyDataName, LobbyName);
            ClientsInLobby.Add(Owner, SteamFriends.GetPersonaName());

            onLobbyCreated?.Invoke();
        }
        
        /// <summary>
        /// The callback for <see cref="SteamMatchmaking.RequestLobbyList"/> (called in <see cref="RequestJoinLobby"/>).
        /// </summary>
        /// <param name="result">Contains all lobbies matching the previously configured criteria before <see cref="SteamMatchmaking.RequestLobbyList"/> was called.</param>
        protected virtual void OnLobbyListReceived(LobbyMatchList_t result)
        {
            Log($"{nameof(OnLobbyListReceived)} returned with {nameof(result)} {result.m_nLobbiesMatching}");

            for (var i = 0; i < result.m_nLobbiesMatching; i++)
            {
                var lobbyByIndex = SteamMatchmaking.GetLobbyByIndex(i);
                var lobbyDataName = SteamMatchmaking.GetLobbyData(lobbyByIndex, LobbyDataName);
                if (string.IsNullOrEmpty(lobbyDataName)) continue;
                Log($"Received lobby with name {lobbyDataName}. trying to join...");
                Status = SteamLobbyStatus.Joining;
                SteamMatchmaking.JoinLobby(lobbyByIndex);
                return;
            }

            Status = SteamLobbyStatus.Idle;
            LobbyName = null;
            onLobbyJoinRequestFailed?.Invoke();
        }
        
        /// <summary>Is called on the local client upon entering a lobby.</summary>
        /// <remarks>Is NOT called on other clients when another client joins.</remarks>
        protected virtual void OnLobbyEntered(LobbyEnter_t result)
        {
            if ((EChatRoomEnterResponse)result.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Warn($"An error occured while attempting to join the specified lobby. {EChatRoomEnterResponse.k_EChatRoomEnterResponseError.ToString()}");
                Status = SteamLobbyStatus.Idle;
                onLobbyJoinRequestFailed?.Invoke();
                return;
            }
            
            Log("Lobby entered.");
            LobbyId = (CSteamID)result.m_ulSteamIDLobby;
            Owner = SteamMatchmaking.GetLobbyOwner((CSteamID)result.m_ulSteamIDLobby);
            // Non-server client entered.
            if (!IsLobbyOwner)
            {
                var memberCount = SteamMatchmaking.GetNumLobbyMembers(LobbyId);
                for (int i = 0; i < memberCount; i++)
                {
                    var memberId = SteamMatchmaking.GetLobbyMemberByIndex(LobbyId, i);
                    ClientsInLobby.Add(memberId, SteamFriends.GetFriendPersonaName(memberId));
                }
                
                // Set lobby name here (again) in case this is an invited player.
                LobbyName = SteamMatchmaking.GetLobbyData(LobbyId, LobbyDataName);
            }

            Status = SteamLobbyStatus.Idle;
            onLocalUserJoinedSteamLobby?.Invoke();
        }
        
        /// <summary>Is called when the steam overlay is toggled.</summary>
        /// <param name="param">Whether the steam overlay is activated or deactivated.</param>
        protected virtual void OnGameOverlayActivated(GameOverlayActivated_t param)
        {
            Log($"Game overlay {(param.m_bActive != 0 ? "activated" : "deactivated")}.");
            onSteamOverlayToggled?.Invoke(param.m_bActive != 0);
        }
        
        /// <summary>
        /// Is called when the local player accepted an invitation to join a steam lobby.
        /// Invokes the steam API call to join the lobby via the lobby ID provided in <paramref name="param"/>.
        /// </summary>
        /// <param name="param">Contains the <see cref="CSteamID"/> of the friend that sent the invitation and contains the lobby ID for joining.</param>
        protected virtual void OnInvitationAccepted(GameLobbyJoinRequested_t param)
        {
            if (Status != SteamLobbyStatus.Idle)
            {
                Warn($"Invitation can not be accepted because handler is busy '{Status.ToString()}'.");
                return;
            }

            if (IsInLobby)
            {
                Warn("Joining another lobby is prohibited while in a lobby.");
                return;
            }
            
            Log($"Invitation of {SteamFriends.GetFriendPersonaName(param.m_steamIDFriend)} accepted. {SteamFriends.GetPersonaName()} attempts to join lobby {param.m_steamIDLobby}...");
            Status = SteamLobbyStatus.Joining;
            SteamMatchmaking.JoinLobby(param.m_steamIDLobby);
        }
        
        /// <summary>
        /// Is called when another player joined or left a lobby the local client is already part of.
        /// </summary>
        /// <param name="result">Contains the <see cref="CSteamID"/> of the affected player as well as the information on what action he just performed (joined, left, kicked, etc...).</param>
        protected virtual void OnPlayerJoinedOrLeft(LobbyChatUpdate_t result)
        {
            var steamId = (CSteamID)result.m_ulSteamIDUserChanged;
            string userName = SteamFriends.GetFriendPersonaName(steamId);
            switch ((EChatMemberStateChange)result.m_rgfChatMemberStateChange)
            {
                case EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                    Log($"Player {userName} joined the steam lobby.");
                    ClientsInLobby.Add(steamId, userName);
                    onUserJoinedSteamLobby?.Invoke(steamId);
                    break;
                case EChatMemberStateChange.k_EChatMemberStateChangeLeft:
                case EChatMemberStateChange.k_EChatMemberStateChangeDisconnected:
                case EChatMemberStateChange.k_EChatMemberStateChangeKicked:
                case EChatMemberStateChange.k_EChatMemberStateChangeBanned:
                    Log($"Player {userName} disconnected from the steam lobby.");
                    ClientsInLobby.Remove(steamId);
                    onUserLeftSteamLobby?.Invoke(steamId);
                    if (steamId == Owner)
                    {
                        Log("Lobby owner left. Disbanding lobby...");
                        LeaveLobby();
                    }
                    break;
                default:
                    throw new NotImplementedException($"{nameof(result.m_rgfChatMemberStateChange)} = {result.m_rgfChatMemberStateChange}");
            }
        }
        
        /// <summary>
        /// Is called when a client's steam profile picture is requested.
        /// </summary>
        /// <param name="param">Contains the image handle needed to request the actual profile picture from steam alongside required parameters for that.</param>
        /// <remarks>This is not called when the requested client's steam profile picture is already locally cached and available.</remarks>
        protected virtual void OnAvatarImageLoaded(AvatarImageLoaded_t param)
        {
            if (param.m_iImage == 0)
            {
                Warn($"User {SteamFriends.GetFriendPersonaName(param.m_steamID)} does not have a profile picture.");
                onSteamUserInformationReceived?.Invoke(new SteamProfileInformation(param.m_steamID, null));
                return;
            }

            byte[] image = new byte[param.m_iTall * param.m_iWide * 4];
            if (!SteamUtils.GetImageRGBA(param.m_iImage, image, image.Length))
            {
                Warn("Steam profile picture image handle is invalid.");
                onSteamUserInformationReceived?.Invoke(new SteamProfileInformation(param.m_steamID, null));
                return;
            }

            Texture2D avatar = new Texture2D(param.m_iWide, param.m_iTall, TextureFormat.RGBA32, false, false);
            avatar.LoadRawTextureData(image);
            avatar.Apply();
            
            onSteamUserInformationReceived?.Invoke(new SteamProfileInformation(param.m_steamID, avatar));
        }
        
        #endregion
        
        #region Steam profile picture helper

        /// <summary>
        /// Tries to load an already locally cached steam profile picture via its <paramref name="imageHandle"/>.
        /// </summary>
        /// <param name="imageHandle">The image handle needed to retrieve the profile picture.</param>
        /// <param name="avatar">If this method returns <see langword="true"/>, <paramref name="avatar"/> contains a steam profile picture.</param>
        /// <remarks>Compared to the callback from <see cref="RequestSteamUserInformation"/>, this method does not invoke any events.</remarks>
        /// <returns><see langword="true"/>, if the profile picture was loaded successfully into <paramref name="avatar"/>; otherwise, <see langword="false"/>.</returns>
        protected virtual bool TryGetCachedProfilePicture(int imageHandle, out Texture2D avatar)
        {
            avatar = null;
            if (!SteamUtils.GetImageSize(imageHandle, out var width, out var height))
            {
                Warn("Steam profile picture image handle is invalid.");
                return false;
            }
            
            byte[] image = new byte[height * width * 4];
            if (!SteamUtils.GetImageRGBA(imageHandle, image, image.Length))
            {
                Warn("Steam profile picture image handle is invalid.");
                return false;
            }

            avatar = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, false);
            avatar.LoadRawTextureData(image);
            avatar.Apply();
            return true;
        }

        #endregion
    }
}