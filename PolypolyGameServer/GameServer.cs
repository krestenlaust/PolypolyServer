using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PolypolyGameServer
{
    public class GameServer
    {
        private const int FramesPerSecondCap = 30;

        public readonly Dictionary<byte, Player> Players = new Dictionary<byte, Player>();
        private readonly byte hostID = 0;
        private TcpListener tcpListener;
        private float fixedDeltaTime;
        private GameLogic logic;
        internal SimpleLogger log;

        /// <summary>
        /// Reserves port, opens a game server.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public GameServer(IPAddress address, short port)
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
        public GameServer(IPAddress address, short port, SimpleLogger logger)
        {
            log = logger;
            logic = new GameLogic(this);
            tcpListener = new TcpListener(address, port);
        }

        /// <summary>
        /// Start accepting players.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public void Start(CancellationToken cancellationToken)
        {
            StartAcceptingPlayers();
            tcpListener.BeginAcceptTcpClient(PlayerConnected, null);

            log.Print($"Server listening on {tcpListener.LocalEndpoint}...");

            Task.Run(() => NetworkLoop(cancellationToken), cancellationToken);
        }

        private void StopAcceptingPlayers() => tcpListener.Stop();

        private void StartAcceptingPlayers() => tcpListener.Start();

        private void ListenForPlayer() => tcpListener.BeginAcceptTcpClient(PlayerConnected, null);

        /// <summary>
        ///     Makes sure the player is up-to-date.
        /// </summary>
        private void IntroducePlayer(ref NetworkStream stream, byte playerID)
        {
            var newPlayerPacket = Packet.Construct.PlayerConnected(playerID);
            BroadcastPacket(newPlayerPacket); // ignored

            foreach (var otherID in Players.Keys.ToList())
            {
                if (otherID == playerID)
                    continue;

                var otherPlayerPacket = Packet.Construct.PlayerConnected(otherID);
                stream.Write(otherPlayerPacket, 0, otherPlayerPacket.Length);

                var otherPlayerName = Packet.Construct.UpdatePlayerNickname(Players[otherID].Nickname, otherID);
                stream.Write(otherPlayerName, 0, otherPlayerName.Length);

                if (Players[otherID].isReady)
                {
                    var otherPlayerReady = Packet.Construct.UpdatePlayerReady(otherID, true);
                    stream.Write(otherPlayerReady, 0, otherPlayerReady.Length);
                }

                var otherPlayerColor = Packet.Construct.UpdatePlayerColor(otherID, Players[otherID].Color);
                stream.Write(otherPlayerColor, 0, otherPlayerColor.Length);
            }

            var updateHostPacket = Packet.Construct.UpdateHost(hostID);
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

            var playerID = (byte) Players.Count;

            var player = new Player(netClient, playerID == hostID, Player.DEFAULT_NAME + playerID);
            Players[playerID] = player;

            if (Players.Count < logic.gameConfig.MaxPlayers)
                ListenForPlayer();

            var stream = netClient.GetStream();
            var updateIDPacket = Packet.Construct.AssignPlayerID(playerID);
            stream.Write(updateIDPacket, 0, updateIDPacket.Length);

            log.Print($"[{netClient.Client.RemoteEndPoint}] Has connected and been assigned ID: {playerID}");

            IntroducePlayer(ref stream, playerID);
        }

        private void DisconnectAllClients()
        {
            foreach (var VARIABLE in Players)
            {
                VARIABLE.Value.NetClient.Close();
            }
        }

        private void NetworkLoop(CancellationToken token)
        {
            Stopwatch frameTime = new Stopwatch();

            while (true)
            {
                bool cancelled = token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(Math.Max(0,
                    1000 / FramesPerSecondCap - (int) frameTime.ElapsedMilliseconds)));
                
                fixedDeltaTime = frameTime.ElapsedMilliseconds / 1000f;
                frameTime.Restart();

                if (cancelled)
                {
                    StopAcceptingPlayers();
                    tcpListener = null;
                    DisconnectAllClients();
                    break;
                }

                var playerIDs = Players.Keys.ToList();
                foreach (byte ID in playerIDs)
                {
                    // Client disconnected, handle disconnection if channel is empty
                    if (!Players[ID].NetClient.Connected && Players[ID].NetClient.Available == 0)
                    {
                        byte[] disconnectPacket;
                        if (logic.isGameInProgress)
                        {
                            // Keep seat open (if disconnected unintentionally)
                            disconnectPacket =
                                Packet.Construct.PlayerDisconnected(ID, false, Packet.DisconnectReason.LostConnection);
                        }
                        else
                        {
                            // Empty seat
                            Players.Remove(ID);

                            disconnectPacket =
                                Packet.Construct.PlayerDisconnected(ID, true, Packet.DisconnectReason.LostConnection);
                        }

                        BroadcastPacket(disconnectPacket);
                        continue;
                    }

                    if (Players[ID].NetClient.Available == 0)
                        continue;

                    var stream = Players[ID].NetClient.GetStream();
                    var packetHeader = (Packet.PacketType) stream.ReadByte();
                    byte[] broadcastPacket = null;

                    log.Print($"[Network][{ID}] {Enum.GetName(typeof(Packet.PacketType), packetHeader)}");
                    switch (packetHeader)
                    {
                        case Packet.PacketType.DicerollRequest:
                            logic.ThrowDiceNetwork(ID);
                            break;
                        case Packet.PacketType.PlayerNickname:
                            Packet.Deconstruct.PlayerNickname(stream, out var nickname);
                            if (Players[ID].isReady)
                            {
                                log.Print($"[{ID}] Recieved nickname request, but player is ready. Discarded.");
                                break;
                            }

                            if (nickname.Length > 15) nickname = nickname.Substring(0, 15);

                            Players[ID].Nickname = nickname;
                            log.Print($"[{ID}] Changed username to {nickname}");

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
                            Players[ID].NetClient.Close();
                            Players.Remove(ID);

                            broadcastPacket =
                                Packet.Construct.PlayerDisconnected(ID, true, Packet.DisconnectReason.Left);
                            
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

                            broadcastPacket = Packet.Construct.PlayerDisconnected(playerId, true,
                                Packet.DisconnectReason.Kicked);

                            Players[playerId].NetClient.Close();
                            Players.Remove(playerId);
                            break;
                        case Packet.PacketType.PrisonReply:
                            Packet.Deconstruct.PrisonReply(stream, out var useCard);

                            Players[ID].ReplyJailOffer = useCard;
                            break;
                        case Packet.PacketType.PropertyReply:
                            Packet.Deconstruct.PropertyReply(stream, out var purchase);

                            Players[ID].ReplyPropertyOffer = purchase;
                            break;
                    }

                    if (!(broadcastPacket is null))
                    {
                        BroadcastPacket(broadcastPacket);
                    }
                }

                if (logic.isGameInProgress)
                    logic.FixedUpdate();
            }
        }

        public void BroadcastPacket(byte[] packet)
        {
            foreach (var item in Players)
            {
                if (item.Value.NetClient?.Connected != true)
                    continue;

                var stream = item.Value.NetClient.GetStream();
                if (stream?.CanWrite == true) stream.Write(packet, 0, packet.Length);
            }
        }
    }
}