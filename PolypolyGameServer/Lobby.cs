// <copyright file="Lobby.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using static NetworkProtocol.Packet;

namespace PolypolyGame
{
    /// <summary>
    /// Represents a single game lobby with players and game settings.
    /// </summary>
    public class Lobby
    {
        public readonly Dictionary<byte, Client> Clients = new Dictionary<byte, Client>();
        public readonly GameConfig Config;
        internal readonly Logger log;
        private const string VersionInfo = "1.2.0";
        private GameLogic gameLogic;
        private byte? hostID = null;
        private TcpListener tcpListener;

        /// <summary>
        /// Invoked once the game starts.
        /// </summary>
        public event Action<GameLogic> GameStarted;

        /// <summary>
        /// Initializes a new instance of the <see cref="Lobby"/> class.
        /// Reserves port and opens a game server.
        /// </summary>
        /// <param name="endPoint">The endpoint the listener is bound to.</param>
        public Lobby(IPEndPoint endPoint)
        {
            log = new Logger();
            tcpListener = new TcpListener(endPoint);
            Config = GameConfig.StandardConfig;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lobby"/> class.
        /// Reserves port, opens a game server and logs using specified logger.
        /// </summary>
        /// <param name="endPoint">The endpoint the listener is bound to.</param>
        /// <param name="logger">The type of logger to use when logging.</param>
        public Lobby(IPEndPoint endPoint, Logger logger)
        {
            log = logger;
            tcpListener = new TcpListener(endPoint);
            Config = GameConfig.StandardConfig;
        }

        /// <summary>
        /// Gets a value indicating whether the game is in progress.
        /// </summary>
        private bool isGameInProgress => !(gameLogic is null);

        /// <summary>
        /// Disposes the game.
        /// </summary>
        public void EndGame()
        {
            gameLogic = null;
        }

        /// <summary>
        /// Start accepting players.
        /// </summary>
        public void Start()
        {
            Print($"Version {VersionInfo}");

            StartAcceptingPlayers();
            tcpListener.BeginAcceptTcpClient(PlayerConnected, null);

            Print($"Server listening on {tcpListener.LocalEndpoint}...");
        }

        /// <summary>
        /// Disposes listener and disconnects all clients.
        /// </summary>
        public void Stop()
        {
            StopAcceptingPlayers();
            tcpListener = null;
            DisconnectAllClients();
        }

        public void GameLoop()
        {
            foreach (var keyValuePair in Clients)
            {
                TcpClient netClient = keyValuePair.Value.NetClient;

                while (netClient.Available > 0)
                {
                    NetworkStream stream = netClient.GetStream();
                    ClientPacketType packetHeader = (ClientPacketType)stream.ReadByte();

                    Print($"[{keyValuePair.Key}] {Enum.GetName(typeof(ClientPacketType), packetHeader)}");

                    byte[] broadcastPacket = null;

                    HandleClientPacket(packetHeader, stream, ref broadcastPacket, keyValuePair.Key);

                    if (broadcastPacket is null)
                    {
                        continue;
                    }

                    BroadcastPacket(broadcastPacket);
                }
            }

            // return if game is not in progress
            if (!isGameInProgress)
            {
                return;
            }

            gameLogic.UpdateState();
        }

        /// <summary>
        /// Iteratively sends packet to all players.
        /// </summary>
        /// <param name="packet">Packet expressed by array of bytes.</param>
        internal void BroadcastPacket(byte[] packet)
        {
            foreach (byte playerID in Clients.Keys.ToList())
            {
                SendToPlayer(playerID, packet);
            }
        }

        private void StopAcceptingPlayers() => tcpListener?.Stop();

        private void StartAcceptingPlayers() => tcpListener.Start();

        private void ListenForPlayer() => tcpListener.BeginAcceptTcpClient(PlayerConnected, null);

        private void Print(string msg) => log?.Print("[Network] " + msg);

        /// <summary>
        /// Makes sure the player is up-to-date.
        /// </summary>
        private void IntroducePlayer(ref NetworkStream stream, byte playerID)
        {
            byte[] updateIDPacket = Construct.AssignPlayerID(playerID);
            stream.Write(updateIDPacket, 0, updateIDPacket.Length);

            byte[] newPlayerPacket = Construct.PlayerConnected(playerID);
            BroadcastPacket(newPlayerPacket);

            foreach (var playerKeyValue in Clients)
            {
                byte otherID = playerKeyValue.Key;
                Client otherClient = playerKeyValue.Value;

                // Skip the new player
                if (otherID == playerID)
                {
                    continue;
                }

                byte[] otherPlayerConnectedPacket = Construct.PlayerConnected(otherID);
                byte[] otherPlayerNamePacket = Construct.UpdatePlayerNickname(otherClient.Nickname, otherID);
                byte[] otherPlayerReadyPacket = Construct.UpdatePlayerReady(otherID, otherClient.isReady);
                byte[] otherPlayerColorPacket = Construct.UpdatePlayerColor(otherID, otherClient.Color);

                stream.Write(otherPlayerConnectedPacket, 0, otherPlayerConnectedPacket.Length);
                stream.Write(otherPlayerNamePacket, 0, otherPlayerNamePacket.Length);
                stream.Write(otherPlayerReadyPacket, 0, otherPlayerReadyPacket.Length);
                stream.Write(otherPlayerColorPacket, 0, otherPlayerColorPacket.Length);
            }

            byte[] updateHostPacket = Construct.UpdateHost(hostID.Value);
            stream.Write(updateHostPacket, 0, updateHostPacket.Length);
        }

        /// <summary>
        /// Gets called when a new player connects.
        /// </summary>
        /// <param name="ar">The asyncronous result of which to retrieve tcpclient.</param>
        private void PlayerConnected(IAsyncResult ar)
        {
            // TcpListener has been disposed.
            if (tcpListener is null)
            {
                return;
            }

            TcpClient netClient;

            try
            {
                netClient = tcpListener.EndAcceptTcpClient(ar);
            }
            catch (SocketException ex)
            {
                // TODO: Find out what causes this exception and document it.
                log.Print(ex.SocketErrorCode);
                log.Print(ex);
                throw ex;
            }

            byte newID = (byte)Clients.Count;

            Client player = new Client(netClient, Client.DefaultName + newID);
            Clients[newID] = player;

            if (hostID is null)
            {
                MigrateHost(newID);
            }

            if (Clients.Count < Config.MaxPlayers)
            {
                ListenForPlayer();
            }

            Print($"[{netClient.Client.RemoteEndPoint}] Has connected and been assigned ID: {newID}");

            NetworkStream stream = netClient.GetStream();
            IntroducePlayer(ref stream, newID);
        }

        private void DisconnectAllClients()
        {
            foreach (var keyValuePair in Clients)
            {
                keyValuePair.Value.NetClient.Close();
            }
        }

        private void HandleClientPacket(ClientPacketType packetType, NetworkStream stream, ref byte[] broadcastPacket, byte clientID)
        {
            Client client = Clients[clientID];

            switch (packetType)
            {
                case ClientPacketType.DicerollRequest:
                    gameLogic.ThrowDiceNetwork(clientID, gameLogic.GetDiceResult());
                    break;
                case ClientPacketType.PlayerNickname:
                    Deconstruct.PlayerNickname(stream, out var nickname);
                    if (client.isReady)
                    {
                        Print($"[{clientID}] Recieved nickname request, but player is ready. Discarded.");
                        break;
                    }

                    // TODO: Hardcoded length limit should be a constant.
                    if (nickname.Length > 15)
                    {
                        nickname = nickname.Substring(0, 15);
                    }

                    client.Nickname = nickname;
                    Print($"[{clientID}] Changed username to {nickname}");

                    // Synkronisér brugernavn med andre brugere.
                    broadcastPacket = Construct.UpdatePlayerNickname(nickname, clientID);
                    break;
                case ClientPacketType.ReadyPacket:
                    client.isReady = true;

                    broadcastPacket = Construct.UpdatePlayerReady(clientID, true);
                    break;
                case ClientPacketType.UnreadyPacket:
                    client.isReady = false;

                    broadcastPacket = Construct.UpdatePlayerReady(clientID, false);
                    break;
                case ClientPacketType.StartGamePacket:
                    if (!client.isHost || isGameInProgress)
                    {
                        break;
                    }

                    bool playersAreReady = !Clients.Any(p => !(p.Value.isReady || p.Value.isHost));

                    if (playersAreReady)
                    {
                        gameLogic = new GameLogic(this, Clients.Keys.ToList(), Config);
                        GameStarted?.Invoke(gameLogic);
                    }

                    broadcastPacket = Construct.GameStarted();
                    break;
                case ClientPacketType.LeaveGamePacket:
                    DisconnectPlayer(clientID, DisconnectReason.Left);

                    break;
                case ClientPacketType.AnimationDone:
                    gameLogic.Players[clientID].isAnimationDone = true;

                    break;
                case ClientPacketType.ChangeColor:
                    Deconstruct.ChangeColor(stream, out var color);

                    client.Color = color;

                    broadcastPacket = Construct.UpdatePlayerColor(clientID, color);

                    break;
                case ClientPacketType.KickPlayer:
                    Deconstruct.KickPlayer(stream, out var playerID);

                    if (clientID != hostID)
                    {
                        break;
                    }

                    DisconnectPlayer(playerID, DisconnectReason.Kicked);

                    break;
                case ClientPacketType.PrisonReply:
                    Deconstruct.PrisonReply(stream, out bool useCard);

                    gameLogic.Players[clientID].ReplyJailOffer = useCard;
                    break;
                case ClientPacketType.PropertyReply:
                    Deconstruct.PropertyReply(stream, out bool purchase);

                    gameLogic.Players[clientID].ReplyPropertyOffer = purchase;
                    break;
                case ClientPacketType.AuctionReply:
                    Deconstruct.AuctionReply(stream, out byte propertyIndex);

                    gameLogic.Players[clientID].ReplyAuctionIndex = propertyIndex;
                    break;
            }
        }

        private void DisconnectPlayer(byte playerID, DisconnectReason disconnectReason)
        {
            Print("Player is being disconnected.");

            Clients.Remove(playerID);

            if (isGameInProgress)
            {
                gameLogic.Players.Remove(playerID);
            }

            // TODO: Possible bug with not notifying client self.
            // no more players, no one to notify.
            if (Clients.Count == 0)
            {
                hostID = null;
                return;
            }

            // Host migration
            if (hostID == playerID)
            {
                var nextHost = Clients.First();

                MigrateHost(nextHost.Key);
            }

            BroadcastPacket(Construct.PlayerDisconnected(playerID, true, disconnectReason));
        }

        private void MigrateHost(byte newHostID)
        {
            Print("Migrating host to " + newHostID);

            Clients[newHostID].isHost = true;
            hostID = newHostID;

            BroadcastPacket(Construct.UpdateHost(newHostID));
        }

        private bool SendToPlayer(byte playerID, byte[] packet)
        {
            TcpClient tcpClient = Clients[playerID].NetClient;

            if (!tcpClient.Connected)
            {
                log.Print("Player not connected");
                DisconnectPlayer(playerID, DisconnectReason.LostConnection);
                return false;
            }

            try
            {
                NetworkStream stream = tcpClient.GetStream();
                stream.Write(packet, 0, packet.Length);
                return true;
            }
            catch (IOException)
            {
                DisconnectPlayer(playerID, DisconnectReason.LostConnection);
                return false;
            }
        }
    }
}