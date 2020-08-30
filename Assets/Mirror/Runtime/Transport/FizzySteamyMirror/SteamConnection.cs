#region Statements

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

#endregion

namespace Mirror.FizzySteam
{
    public class SteamConnection : SteamCommon, IChannelConnection
    {
        static readonly ILogger Logger = LogFactory.GetLogger(typeof(SteamConnection));

        #region Variables

        private byte[] _clientSendPoolData;
        private Message _clientReceivePoolData;
        private TaskCompletionSource<Task> _connectedComplete;
        public bool Connected = false;
        public Action ServerFull;

        #endregion

        #region Class Specific

        /// <summary>
        ///     Connect to server.
        /// </summary>
        /// <returns></returns>
        public async Task<IConnection> ConnectAsync()
        {
            if (Logger.logEnabled) Logger.Log($"SteamConnection attempting connection to {Options.ConnectionAddress}");

            if(SteamNetworking.GetP2PSessionState(Options.ConnectionAddress, out var connectionState))
                if (bool.Parse(connectionState.m_bConnectionActive.ToString()))
                    SteamNetworking.CloseP2PSessionWithUser(Options.ConnectionAddress);

            try
            {
                // Send a message to server to initiate handshake connection
                SteamSend(Options.ConnectionAddress, InternalMessages.Connect);

                _connectedComplete = new TaskCompletionSource<Task>();
                Task connectedCompleteTask = _connectedComplete.Task;

                while (await Task.WhenAny(connectedCompleteTask,
                           Task.Delay(TimeSpan.FromSeconds(Math.Max(1, Options.ConnectionTimeOut)))) !=
                       connectedCompleteTask)
                {
                    if (Logger.logEnabled)
                        Logger.LogError(
                            $"SteamConnection connection to {Options.ConnectionAddress.m_SteamID} timed out.");


                    return null;
                }

                Connected = true;

                return this;
            }
            catch (FormatException)
            {
                if (Logger.logEnabled)
                    Logger.LogError("SteamConnection connection string was not in the right format. Did you enter a SteamId?");
            }
            catch (Exception ex)
            {
                if (Logger.logEnabled)
                    Logger.LogError($"SteamConnection error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Process our internal messages away from mirror.
        /// </summary>
        /// <param name="data">The message data coming in.</param>
        private void ProcessInternalMessages(Message data)
        {
            switch (data.eventType)
            {
                case InternalMessages.Accept:

                    if (Logger.logEnabled)
                        Logger.Log("Received internal message of server accepted our request to connect.");

                    _connectedComplete.SetResult(_connectedComplete.Task);

                    break;
                case InternalMessages.Disconnect:
                    if(Logger.logEnabled)
                        Logger.Log("Received internal message to disconnect steam user.");

                    if (Connected)
                        Disconnect();

                    break;
                case InternalMessages.TooManyUsers:

                    if (Logger.logEnabled)
                        Logger.Log("Received internal message that there are too many users connected to server.");

                    // TODO Implement way to tell users server is full? Or does mirror do this?

                    break;
                default:
                    if (Logger.logEnabled)
                        Logger.Log(
                            $"SteamConnection cannot process internal message {data.eventType}. If this is anything other then {InternalMessages.Data} something has gone wrong.");
                    break;
            }
        }

        /// <summary>
        ///     Send an internal message through steam backend. Useful for non mirror data passing.
        /// </summary>
        /// <param name="target">The person we want to send data to.</param>
        /// <param name="type">The type of internal message to send.</param>
        internal override bool SteamSend(CSteamID target, InternalMessages type)
        {
            return SteamNetworking.SendP2PPacket(target, new[] {(byte) type}, 1, EP2PSend.k_EP2PSendReliable,
                Options.Channels.Length);
        }

        /// <summary>
        ///     Update method to be called by the transport.
        /// </summary>
        protected internal override void Update()
        {
            // Check for internal messages first. Our internal messages are on different channel mirror has
            // no idea about.
            if (DataAvailable(out var serverId, out var internalMessage, Options.Channels.Length))
            {
                var internalMsg = new Message(serverId, (InternalMessages) internalMessage[0], internalMessage);

                // Checking to see if user is connected otherwise waiting on acceptance message
                // so let's process this right away don't need to queue.
                if (internalMsg.eventType != InternalMessages.Data)
                {
                    if (Logger.logEnabled)
                        Logger.Log($"SteamConnection: Processing internal message: {internalMsg.eventType}");

                    ProcessInternalMessages(internalMsg);

                    return;
                }
            }
            
            // Check for real data messages coming from mirror channels.
            for (var channel = 0; channel < Options.Channels.Length; channel++)
            {
                if (!DataAvailable(out var steamId, out var receiveBuffer, channel)) continue;

                var dataMsg = new Message(steamId, InternalMessages.Data, receiveBuffer);

                if (Logger.logEnabled)
                    Logger.Log(
                        $"SteamConnection: Queue up message Event Type: {dataMsg.eventType} data: {BitConverter.ToString(dataMsg.data)}");

                QueuedData.Enqueue(dataMsg);
            }
        }


        /// <summary>
        ///     Send data through steam network.
        /// </summary>
        /// <param name="host">The person we want to send data to.</param>
        /// <param name="msgBuffer">The data we are sending.</param>
        /// <param name="channel">The channel we are going to send data on.</param>
        /// <returns></returns>
        private bool Send(CSteamID host, byte[] msgBuffer, int channel)
        {
            return SteamNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, Options.Channels[channel], channel);
        }

        /// <summary>
        ///     Initialize <see cref="SteamConnection"/>
        /// </summary>
        /// <param name="options"></param>
        public SteamConnection(SteamOptions options) : base(options)
        {
            Options = options;
            SteamNetworking.AllowP2PPacketRelay(Options.AllowSteamRelay);
        }

        #endregion

        #region Implementation of IConnection

        /// <summary>
        ///     Send data on default reliable channel.
        /// </summary>
        /// <param name="data">The data we want to send.</param>
        /// <returns>Whether or not we sent our data.</returns>
        public Task SendAsync(ArraySegment<byte> data)
        {
            // Default send to reliable channel;
            return SendAsync(data, 0);
        }

        /// <summary>
        ///     Check if we have data in the pipe line that we need to process
        /// </summary>
        /// <param name="buffer">The buffer we need to write data too.</param>
        /// <returns></returns>
        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            try
            {
                bool waitingForNewMsg = true;

                while (waitingForNewMsg)
                {
                    if (!Connected)
                        return false;

                    if (QueuedData.TryDequeue(out _clientReceivePoolData))
                    {
                        waitingForNewMsg = _clientReceivePoolData.eventType != InternalMessages.Data;
                    }

                    await Task.Delay(10);
                }

                buffer.SetLength(0);

                if (Logger.logEnabled)
                    Logger.Log(
                        $"SteamConnection processing message: {BitConverter.ToString(_clientReceivePoolData.data)}");

                await buffer.WriteAsync(_clientReceivePoolData.data, 0, _clientReceivePoolData.data.Length);

                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        /// <summary>
        ///     Disconnect steam user and close P2P session.
        /// </summary>
        public override void Disconnect()
        {
            if (Logger.logEnabled)
                Logger.Log($"SteamConnection shutting down.");

            Connected = false;

            SteamSend(Options.ConnectionAddress, InternalMessages.Disconnect);

            _clientSendPoolData = null;

            base.Disconnect();

            SteamNetworking.CloseP2PSessionWithUser(Options.ConnectionAddress);
        }

        /// <summary>
        ///     Get the network address using steams id.
        /// </summary>
        /// <returns></returns>
        public EndPoint GetEndPointAddress()
        {
            return new DnsEndPoint(Options.ConnectionAddress.m_SteamID.ToString(), 0);
        }

        /// <summary>
        ///     Send data on a specific channel.
        /// </summary>
        /// <param name="data">The data we want to send.</param>
        /// <param name="channel">The channel we want to send it on.</param>
        /// <returns></returns>
        public Task SendAsync(ArraySegment<byte> data, int channel)
        {
            _clientSendPoolData = new byte[data.Count];

            Array.Copy(data.Array, data.Offset, _clientSendPoolData, 0, data.Count);

            return Send(Options.ConnectionAddress, _clientSendPoolData, channel) ? Task.CompletedTask : null;
        }

        #endregion
    }
}