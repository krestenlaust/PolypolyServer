using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace PolypolyGameServer
{
    public class Lobby
    {
        public readonly Dictionary<byte, Player> Players = new Dictionary<byte, Player>();
        internal SimpleLogger log;
        private byte? hostID = null;
        private TcpListener tcpListener;
        private GameLogic logic;

        /// <summary>
        /// Reserves port, opens a game server.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public Lobby(IPAddress address, short port)
        {
            log = new SimpleLogger();
            logic = new GameLogic(this);
            tcpListener = new TcpListener(address, port);
        }

        /// <summary>
        /// Reserves port, opens a game server using specified logger.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="logger"></param>
        public Lobby(IPAddress address, short port, SimpleLogger logger)
        {
            log = logger;
            logic = new GameLogic(this);
            tcpListener = new TcpListener(address, port);
        }

        private void Print(string msg)
        {
            log.Print("[Network] " + msg);
        }

        /// <summary>
        /// Start accepting players.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public void Start()
        {
            Console.WriteLine("Version 1.2.0");

            StartAcceptingPlayers();
            tcpListener.BeginAcceptTcpClient(PlayerConnected, null);

            Print($"Server listening on {tcpListener.LocalEndpoint}...");
        }

        public void Stop()
        {
            StopAcceptingPlayers();
            tcpListener = null;
            DisconnectAllClients();
        }

        private void StopAcceptingPlayers() => tcpListener?.Stop();

        private void StartAcceptingPlayers() => tcpListener.Start();

        private void ListenForPlayer() => tcpListener.BeginAcceptTcpClient(PlayerConnected, null);

        /// <summary>
        ///     Makes sure the player is up-to-date.
        /// </summary>
        private void IntroducePlayer(ref NetworkStream stream, byte playerID)
        {
            byte[] newPlayerPacket = Packet.Construct.PlayerConnected(playerID);
            BroadcastPacket(newPlayerPacket);

            foreach (byte otherID in Players.Keys.ToList())
            {
                if (otherID == playerID)
                    continue;

                byte[] otherPlayerPacket = Packet.Construct.PlayerConnected(otherID);
                stream.Write(otherPlayerPacket, 0, otherPlayerPacket.Length);

                byte[] otherPlayerName = Packet.Construct.UpdatePlayerNickname(Players[otherID].Nickname, otherID);
                stream.Write(otherPlayerName, 0, otherPlayerName.Length);

                if (Players[otherID].isReady)
                {
                    byte[] otherPlayerReady = Packet.Construct.UpdatePlayerReady(otherID, true);
                    stream.Write(otherPlayerReady, 0, otherPlayerReady.Length);
                }

                byte[] otherPlayerColor = Packet.Construct.UpdatePlayerColor(otherID, Players[otherID].Color);
                stream.Write(otherPlayerColor, 0, otherPlayerColor.Length);
            }

            byte[] updateHostPacket = Packet.Construct.UpdateHost(hostID.Value);
            stream.Write(updateHostPacket, 0, updateHostPacket.Length);
        }

        /// <summary>
        /// Gets called when a new player connects.
        /// </summary>
        /// <param name="ar"></param>
        private void PlayerConnected(IAsyncResult ar)
        {
            TcpClient netClient;

            try
            {
                netClient = tcpListener?.EndAcceptTcpClient(ar);
            }
            catch (Exception)
            {
                return;
            }

            if (netClient is null)
            {
                return;
            }

            byte playerID = (byte)Players.Count;

            Player player = new Player(netClient, Player.DEFAULT_NAME + playerID);
            Players[playerID] = player;

            if (hostID is null)
            {
                MigrateHost(player, playerID);
            }

            if (Players.Count < logic.gameConfig.MaxPlayers)
                ListenForPlayer();

            NetworkStream stream = netClient.GetStream();
            byte[] updateIDPacket = Packet.Construct.AssignPlayerID(playerID);
            stream.Write(updateIDPacket, 0, updateIDPacket.Length);

            Print($"[{netClient.Client.RemoteEndPoint}] Has connected and been assigned ID: {playerID}");

            IntroducePlayer(ref stream, playerID);
        }

        private void DisconnectAllClients()
        {
            foreach (var VARIABLE in Players)
            {
                VARIABLE.Value.NetClient.Close();
            }
        }

        public void GameLoop()
        {
            var playerIDs = Players.Keys.ToList();
            foreach (byte ID in playerIDs)
            {
                while (Players[ID].NetClient.Available > 0)
                {
                    NetworkStream stream = Players[ID].NetClient.GetStream();
                    Packet.PacketType packetHeader = (Packet.PacketType)stream.ReadByte();
                    byte[] broadcastPacket = null;

                    Print($"[{ID}] {Enum.GetName(typeof(Packet.PacketType), packetHeader)}");
                    switch (packetHeader)
                    {
                        case Packet.PacketType.DicerollRequest:
                            logic.ThrowDiceNetwork(ID);
                            break;
                        case Packet.PacketType.PlayerNickname:
                            Packet.Deconstruct.PlayerNickname(stream, out var nickname);
                            if (Players[ID].isReady)
                            {
                                Print($"[{ID}] Recieved nickname request, but player is ready. Discarded.");
                                break;
                            }

                            if (nickname.Length > 15) nickname = nickname.Substring(0, 15);

                            Players[ID].Nickname = nickname;
                            Print($"[{ID}] Changed username to {nickname}");

                            // Synkronisér brugernavn med andre brugere.
                            broadcastPacket = Packet.Construct.UpdatePlayerNickname(nickname, ID);
                            break;
                        case Packet.PacketType.ReadyPacket:
                            Players[ID].isReady = true;

                            broadcastPacket = Packet.Construct.UpdatePlayerReady(ID, true);
                            break;
                        case Packet.PacketType.UnreadyPacket:
                            Players[ID].isReady = false;

                            broadcastPacket = Packet.Construct.UpdatePlayerReady(ID, false);
                            break;
                        case Packet.PacketType.StartGamePacket:
                            if (!Players[ID].isHost || logic.isGameInProgress)
                                break;

                            var playersAreReady = true;
                            foreach (var player in Players.Values.ToList())
                            {
                                if (!playersAreReady)
                                    break;
                                playersAreReady = playersAreReady && (player.isReady || player.isHost);
                            }

                            broadcastPacket = Packet.Construct.GameStarted();
                            logic.isGameInProgress = playersAreReady;

                            break;
                        case Packet.PacketType.LeaveGamePacket:
                            DisconnectPlayer(ID);

                            break;
                        case Packet.PacketType.AnimationDone:
                            Players[ID].isAnimationDone = true;

                            break;
                        case Packet.PacketType.ChangeColor:
                            Packet.Deconstruct.ChangeColor(stream, out var color);

                            Players[ID].Color = color;

                            broadcastPacket = Packet.Construct.UpdatePlayerColor(ID, color);

                            break;
                        case Packet.PacketType.KickPlayer:
                            Packet.Deconstruct.KickPlayer(stream, out var playerId);

                            if (ID != hostID)
                                break;

                            Players[playerId].NetClient?.Close();
                            Players.Remove(playerId);

                            broadcastPacket = Packet.Construct.PlayerDisconnected(playerId, true,
                                Packet.DisconnectReason.Kicked);
                            break;
                        case Packet.PacketType.PrisonReply:
                            Packet.Deconstruct.PrisonReply(stream, out bool useCard);

                            Players[ID].ReplyJailOffer = useCard;
                            break;
                        case Packet.PacketType.PropertyReply:
                            Packet.Deconstruct.PropertyReply(stream, out bool purchase);

                            Players[ID].ReplyPropertyOffer = purchase;
                            break;
                        case Packet.PacketType.AuctionReply:
                            Packet.Deconstruct.AuctionReply(stream, out byte propertyIndex);

                            Players[ID].ReplyAuctionIndex = propertyIndex;
                            break;
                    }

                    if (!(broadcastPacket is null))
                    {
                        BroadcastPacket(broadcastPacket);
                    }
                }
            }

            if (logic.isGameInProgress)
                logic.UpdateState();
        }

        private void DisconnectPlayer(byte playerID)
        {
            Print("Player not responding, disconnected.");

            Players.Remove(playerID);

            // Host migration
            if (hostID == playerID)
            {
                try
                {
                    var nextHost = Players.First();

                    MigrateHost(nextHost.Value, nextHost.Key);
                }
                catch (Exception)
                {
                    hostID = null;
                }
            }

            byte[] packet = Packet.Construct.PlayerDisconnected(playerID, true, Packet.DisconnectReason.LostConnection);

            BroadcastPacket(packet);
        }

        private void MigrateHost(Player newHost, byte newHostID)
        {
            Print("Host migration to: " + newHost);
            newHost.isHost = true;
            hostID = newHostID;
            BroadcastPacket(Packet.Construct.UpdateHost(newHostID));
        }

        public bool SendToPlayer(byte playerID, byte[] packet)
        {
            try
            {
                NetworkStream stream = Players[playerID].NetClient.GetStream();
                stream.Write(packet, 0, packet.Length);
                return true;
            }
            catch (IOException)
            {
                DisconnectPlayer(playerID);
                return false;
            }
        }

        public void BroadcastPacket(byte[] packet)
        {
            var players = Players.Keys.ToList();

            foreach (var item in players)
            {
                SendToPlayer(item, packet);
            }
        }
    }
}