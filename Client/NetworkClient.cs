using PolypolyGame;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkProtocol;
using static NetworkProtocol.Packet;

namespace Client
{
    public class NetworkClient
    {
        /// <summary>
        /// Only contains entries for players who are connected.
        /// </summary>
        public Dictionary<byte, ClientPlayer> Players = new Dictionary<byte, ClientPlayer>();
        public GameBoard Board;

        public byte SelfID { get; private set; } = byte.MaxValue;
        public byte CurrentTurn { private set; get; }
        public byte HostID { private set; get; }

        public bool isConnected
        {
            get
            {
                return serverConn?.Connected == true;
            }
        }
        public bool isHost
        {
            get
            {
                return SelfID == HostID;
            }
        }
        public byte LobbySize { get; private set; } = 4;

        private TcpClient serverConn = new TcpClient();
        private NetworkStream stream;

        public ClientPlayer this[PlayerID playerID]
        {
            get
            {
                return this[playerID.Value];
            }
        }

        public ClientPlayer this[byte playerID]
        {
            get
            {
                return Players[playerID];
            }
        }

        public NetworkClient()
        {
            Board = new GameBoard();
        }

        public void UpdateNetwork()
        {
            if (stream is null)
            {
                return;
            }

            if (serverConn.Connected)
            {
                if (disconnectedInvoked)
                {
                    disconnectedInvoked = false;
                }
            }
            else
            {
                if (!disconnectedInvoked)
                {
                    TriggerPlayerDisconnected(SelfID);
                    disconnectedInvoked = true;
                }

                return;
            }

            while (stream.DataAvailable)
            {
                ServerPacketType packetHeader = (ServerPacketType)stream.ReadByte();

                switch (packetHeader)
                {
                    case ServerPacketType.DicerollResult:
                        {
                            Deconstruct.DicerollResult(stream, out byte playerID, out (byte, byte) rollResult);

                            TriggerDiceRolled(playerID, rollResult);
                        }
                        break;
                    case ServerPacketType.UpdatePlayerTurn:
                        {
                            Deconstruct.UpdatePlayerTurn(stream, out byte playerID);
                            CurrentTurn = playerID;

                            TriggerUpdatePlayerTurn(playerID);
                        }
                        break;
                    case ServerPacketType.UpdatePlayerNickname:
                        {
                            Deconstruct.UpdateNickname(stream, out string nickname, out byte playerID);
                            if (Players.ContainsKey(playerID))
                            {
                                Players[playerID].Nickname = nickname;
                            }

                            TriggerNewNickname(playerID, nickname);
                        }
                        break;
                    case ServerPacketType.AssignPlayerID:
                        {
                            SelfID = (byte)stream.ReadByte();
                            Players[SelfID] = new ClientPlayer();

                            TriggerAssignedID(SelfID);
                        }
                        break;
                    case ServerPacketType.UpdateHost:
                        {
                            Deconstruct.UpdateHost(stream, out byte playerID);

                            foreach (KeyValuePair<byte, ClientPlayer> item in Players)
                            {
                                Players[item.Key].isHost = item.Key == playerID;
                            }

                            HostID = playerID;
                            TriggerUpdateHost(playerID);
                        }
                        break;
                    case ServerPacketType.PlayerDisconnected:
                        {
                            Deconstruct.PlayerDisconnected(stream, out byte playerID, out bool permanent, out DisconnectReason disconnectReason);

                            TriggerPlayerDisconnected(playerID, permanent, disconnectReason);

                            if (playerID != SelfID && permanent)
                            {
                                Players.Remove(playerID);
                            }
                        }
                        break;
                    case ServerPacketType.PlayerConnected:
                        {
                            Deconstruct.PlayerConnected(stream, out byte playerID);
                            Players[playerID] = new ClientPlayer();

                            TriggerPlayerConnected(playerID);
                        }
                        break;
                    case ServerPacketType.UpdatePlayerReady:
                        {
                            Deconstruct.UpdatePlayerReady(stream, out byte playerID, out bool readyStatus);
                            Players[playerID].isReady = readyStatus;

                            TriggerUpdatePlayerReady(playerID, readyStatus);
                        }
                        break;
                    case ServerPacketType.UpdatePlayerMoney:
                        {
                            Deconstruct.UpdatePlayerMoney(stream, out byte playerID, out int newAmount, out bool isIncreased);
                            int oldAmount = Players[playerID].Money;
                            Players[playerID].Money = newAmount;

                            TriggerUpdatePlayerMoney(playerID, newAmount, oldAmount);
                        }
                        break;
                    case ServerPacketType.PlayerJail:
                        {
                            Deconstruct.PlayerJail(stream, out byte playerID, out byte jailTurnsLeft);
                            Players[playerID].JailTurns = jailTurnsLeft;

                            TriggerPlayerJailed(playerID, jailTurnsLeft);
                        }
                        break;
                    case ServerPacketType.UpdatePlayerPosition:
                        {
                            Deconstruct.UpdatePlayerPosition(stream, out byte playerID, out byte newPosition, out MoveType moveType);

                            TriggerUpdatePlayerPosition(playerID, (byte)Players[playerID].Position, newPosition, moveType);

                            Players[playerID].Position = newPosition;
                        }
                        break;
                    case ServerPacketType.GameStarted:
                        {
                            TriggerGameStarted();
                        }
                        break;
                    case ServerPacketType.UpdatePlayerColor:
                        {
                            Deconstruct.UpdatePlayerColor(stream, out byte playerID, out TeamColor color);

                            Players[playerID].Color = color;

                            TriggerUpdatePlayerColor(playerID, color);
                        }
                        break;
                    case ServerPacketType.UpdateBoardProperty:
                        {
                            Deconstruct.UpdateBoardProperty(stream, out byte tileIndex, out GameBoard.TileProperty tile);

                            Board.PropertyTiles[tileIndex] = tile;

                            TriggerUpdateBoardProperty(tileIndex, tile);
                        }
                        break;
                    case ServerPacketType.DrawChanceCard:
                        {
                            Deconstruct.DrawChanceCard(stream, out ChanceCard chanceCard);

                            TriggerDrawChanceCard(chanceCard);
                        }
                        break;
                    case ServerPacketType.PrisonCardOffer:
                        {
                            Deconstruct.PrisonCardOffer(stream, out bool hasCard);

                            TriggerPrisonCardOffer(hasCard);
                        }
                        break;
                    case ServerPacketType.PropertyOffer:
                        {
                            Deconstruct.PropertyOffer(stream, out byte playerID, out GameBoard.TileProperty.BuildingState buildingState, out int baseRent, out int cost, out bool isAffordable);

                            TriggerPropertyOffer(playerID, buildingState, baseRent, cost, isAffordable);
                        }
                        break;
                    case ServerPacketType.AuctionProperty:
                        {
                            Deconstruct.AuctionProperty(stream, out byte playerID, out int auctionValue);

                            TriggerPropertyAuction(playerID, auctionValue);
                        }
                        break;
                    case ServerPacketType.UpdateGroupDoubleRent:
                        {
                            Deconstruct.UpdateGroupDoubleRent(stream, out byte groupID, out bool status);

                            TriggerUpdateGroupDoubleRent(groupID, status);
                        }
                        break;
                    case ServerPacketType.PlayerBankrupt:
                        {
                            Deconstruct.PlayerBankrupt(stream, out byte playerID);

                            Players[playerID].isBankrupt = true;

                            TriggerPlayerBankrupt(playerID);
                        }
                        break;
                    case ServerPacketType.GameOver:
                        {
                            Deconstruct.GameOver(stream, out GameOverType gameOverType, out byte winnerID);

                            TriggerGameOver(gameOverType, winnerID);
                        }
                        break;
                    case ServerPacketType.UpdatePlayerDoubleRent:
                        {
                            Deconstruct.UpdatePlayerDoubleRent(stream, out byte playerID, out bool status);

                            Players[playerID].hasDoubleRentCoupon = status;

                            TriggerPlayerDoubleRentUpdate(playerID, status);
                        }
                        break;
                    case ServerPacketType.UpdatePlayerJailCoupon:
                        {
                            Deconstruct.UpdatePlayerJailCoupon(stream, out byte playerID, out bool status);

                            Players[playerID].hasJailCoupon = status;

                            TriggerPlayerJailCouponUpdate(playerID, status);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public class ClientPlayer
        {
            public byte AvatarType;
            public TeamColor Color;
            public bool isHost;
            public bool isReady;
            public string Nickname;
            public byte ConsecutiveDoubleDice;
            public bool hasDoubleRentCoupon;
            public bool hasJailCoupon;
            public bool isBankrupt;
            public byte JailTurns;
            public int Money;
            public int Position;
            public bool isAnimationDone;
            public bool? ReplyJailOffer;
            public bool? ReplyPropertyOffer;
            public byte? ReplyAuctionIndex;
        }

        /// <summary>
        /// Connects to server.
        /// </summary>
        /// <param name="ip">IP-formatted string or hostname</param>
        /// <param name="port">Valid port</param>
        public void ConnectClient(string ip, short port)
        {
            serverConn = new TcpClient();
            serverConn.Connect(ip, port);

            if (serverConn.Connected)
            {
                disconnectedInvoked = false;
                stream = serverConn.GetStream();
            }
        }

        public struct ConnectResult
        {
            public bool hasConnected;
        }
        /// <summary>
        /// Returns when the client is done trying to connect.
        /// </summary>
        public event Action<ConnectResult> ConnectRequestComplete;
        /// <summary>
        /// Connects to server asynchronously. Get result by subscribing to <c>connectRequestComplete</c>.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public async void ConnectClientAsync(string ip, short port)
        {
            serverConn = new TcpClient();
            Task connTask = serverConn.ConnectAsync(ip, port);
            await connTask;

            if (connTask.IsCompleted)
            {
                disconnectedInvoked = false;
            }

            ConnectRequestComplete?.Invoke(new ConnectResult()
            {
                hasConnected = connTask.IsCompleted && !connTask.IsFaulted
            });
        }

        /// <summary>
        /// Calls <c>onPlayerDisconnected</c> when connection has been closed.
        /// </summary>
        public void DisconnectClient()
        {
            TriggerPlayerDisconnected(SelfID);
            SendToServer(Construct.LeaveGamePacket());

            serverConn = null;
            stream = null;
            Players.Clear();
            SelfID = byte.MaxValue;
            CurrentTurn = 0;
            HostID = 0;
        }

        public void ChangeColorPreference(TeamColor color)
        {
            SendToServer(Construct.ChangeColor(color));
        }

        public void KickPlayer(byte playerID)
        {
            SendToServer(Construct.KickPlayer(playerID));
        }

        public void HostStartGame()
        {
            SendToServer(Construct.StartGame());
        }

        public void SignalAnimationDone()
        {
            SendToServer(Construct.AnimationDone());
        }

        public void UpdateUsername(string newUsername)
        {
            Players[SelfID].Nickname = newUsername;

            SendToServer(Construct.PlayerNickname(newUsername));
        }

        /// <summary>
        /// Rolls dice server-side. Returns result with <c>Events.onDiceRolled</c> event call.
        /// </summary>
        public void RollDice()
        {
            SendToServer(Construct.DicerollRequest());
        }

        public void SendReadyState(bool isReady)
        {
            SendToServer(isReady ? Construct.ReadyPacket() : Construct.UnreadyPacket());
        }

        public void AnswerPropertyOffer(bool purchase)
        {
            SendToServer(Construct.PropertyReply(purchase));
        }

        public void AnswerPrisonCardOffer(bool useCard)
        {
            SendToServer(Construct.PrisonReply(useCard));
        }

        public void AnswerPropertyAuction(byte propertyindex)
        {
            SendToServer(Construct.AuctionReply(propertyindex));
        }

        private void SendToServer(byte[] packet)
        {
            try
            {
                stream?.Write(packet, 0, packet.Length);
            }
            catch (IOException)
            {
            }
        }

        public struct PlayerID
        {
            /// <summary>
            /// Returns Player ID as a value.
            /// </summary>
            public byte Value;

            /// <summary>
            /// Useful for debugging.
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return Value.ToString();
            }

            public PlayerID(byte playerID)
            {
                Value = playerID;
            }
        }

        /// <summary>
        /// Event is invoked when the game starts and only once. Ideally the event signals the client to load the game scene.
        /// </summary>
        public event Action onGameStarted;
        private void TriggerGameStarted() => onGameStarted?.Invoke();

        public struct PlayerConnectedArgs
        {
            public PlayerID PlayerID;
        }
        /// <summary>
        /// Event is invoked when a new player establishes a connection to the server. Event <b>also</b> includes <i>this</i> client (when this client connects), check using <c>e.PlayerID.isSelf</c>.
        /// </summary>
        public event Action<PlayerConnectedArgs> onPlayerConnected;
        private void TriggerPlayerConnected(byte playerID)
        {
            onPlayerConnected?.Invoke(new PlayerConnectedArgs
            {
                PlayerID = new PlayerID(playerID)
            });
        }

        private bool disconnectedInvoked = false;
        public struct PlayerDisconnectedArgs
        {
            public PlayerID PlayerID;
            public bool Permanent;
            public DisconnectReason DisconnectReason;
        }
        /// <summary>
        /// Event is invoked whenever a player disconnects. This <b>also</b> includes <i>this</i> client, check using <c>e.PlayerID.isSelf</c>.
        /// </summary>
        public event Action<PlayerDisconnectedArgs> onPlayerDisconnected;
        private void TriggerPlayerDisconnected(byte playerID, bool permanent, DisconnectReason disconnectReason)
        {
            onPlayerDisconnected?.Invoke(new PlayerDisconnectedArgs
            {
                PlayerID = new PlayerID(playerID),
                Permanent = permanent,
                DisconnectReason = disconnectReason
            });
        }
        private void TriggerPlayerDisconnected(byte playerID)
        {
            onPlayerDisconnected?.Invoke(new PlayerDisconnectedArgs
            {
                PlayerID = new PlayerID(playerID)
            });
        }

        public struct UpdatePlayerTurnArgs
        {
            public PlayerID PlayerID;
        }
        /// <summary>
        /// Event is invoked when a new turn begins.
        /// </summary>
        public event Action<UpdatePlayerTurnArgs> onUpdatePlayerTurn;
        private void TriggerUpdatePlayerTurn(byte playerID)
        {
            onUpdatePlayerTurn?.Invoke(new UpdatePlayerTurnArgs
            {
                PlayerID = new PlayerID(playerID)
            });
        }

        public struct PlayerBankruptArgs
        {
            public PlayerID PlayerID;
        }
        public event Action<PlayerBankruptArgs> onPlayerBankrupt;
        private void TriggerPlayerBankrupt(byte playerID)
        {
            onPlayerBankrupt?.Invoke(new PlayerBankruptArgs
            {
                PlayerID = new PlayerID(playerID)
            });
        }

        public struct DiceRolledArgs
        {
            public PlayerID PlayerID;
            public (byte, byte) Result;
        }
        /// <summary>
        /// Event returns dice roll result, ideally used for player movement and dice animation.
        /// </summary>
        public event Action<DiceRolledArgs> onDiceRolled;
        private void TriggerDiceRolled(byte playerID, (byte, byte) result)
        {
            onDiceRolled?.Invoke(new DiceRolledArgs
            {
                PlayerID = new PlayerID(playerID),
                Result = result
            });
        }

        public struct UpdateNicknameArgs
        {
            public PlayerID PlayerID;
            public string NewNickname;
        }
        /// <summary>
        /// Event is invoked whenever a player updates their nickname. Including <i>this</i> client.
        /// </summary>
        public event Action<UpdateNicknameArgs> onNewNickname;
        private void TriggerNewNickname(byte playerID, string NewNickname)
        {
            onNewNickname?.Invoke(new UpdateNicknameArgs
            {
                PlayerID = new PlayerID(playerID),
                NewNickname = NewNickname
            });
        }

        public struct UpdatePlayerReadyArgs
        {
            public PlayerID PlayerID;
            public bool ReadyStatus;
        }
        /// <summary>
        /// Event is invoked whenever a player updates their ready status. Including <i>this</i> client.
        /// </summary>
        public event Action<UpdatePlayerReadyArgs> onUpdateReadyPlayer;
        private void TriggerUpdatePlayerReady(byte playerID, bool readyStatus)
        {
            onUpdateReadyPlayer?.Invoke(new UpdatePlayerReadyArgs
            {
                PlayerID = new PlayerID(playerID),
                ReadyStatus = readyStatus
            });
        }

        public struct UpdatePlayerPositionArgs
        {
            public PlayerID PlayerID;
            public byte NewPosition;
            public byte OldPosition;
            public MoveType MoveType;
        }
        /// <summary>
        /// Event is invoked when a player position is updated, e.g. goto prison, chance card, dice roll.
        /// </summary>
        public event Action<UpdatePlayerPositionArgs> onUpdatePlayerPosition;
        private void TriggerUpdatePlayerPosition(byte playerID, byte oldPosition, byte newPosition, MoveType moveType)
        {
            onUpdatePlayerPosition?.Invoke(new UpdatePlayerPositionArgs
            {
                PlayerID = new PlayerID(playerID),
                NewPosition = newPosition,
                OldPosition = oldPosition,
                MoveType = moveType
            });
        }

        public struct UpdateHostArgs
        {
            public PlayerID PlayerID;
        }
        /// <summary>
        /// Event is invoked when a new host has been chosen.
        /// </summary>
        public event Action<UpdateHostArgs> onUpdateHost;
        private void TriggerUpdateHost(byte playerID)
        {
            onUpdateHost?.Invoke(new UpdateHostArgs
            {
                PlayerID = new PlayerID(playerID)
            });
        }

        public struct AssignedIDArgs
        {
            public PlayerID PlayerID;
        }
        /// <summary>
        /// Called when a player is assigned ID, (when a player joins).
        /// </summary>
        public event Action<AssignedIDArgs> onAssignedID;
        private void TriggerAssignedID(byte playerID)
        {
            onAssignedID?.Invoke(new AssignedIDArgs
            {
                PlayerID = new PlayerID(playerID)
            });
        }

        public struct PlayerJailedArgs
        {
            public PlayerID PlayerID;
            public byte TurnsLeft;
        }
        /// <summary>
        /// Called when a player is jailed.
        /// </summary>
        public event Action<PlayerJailedArgs> onPlayerJailed;
        private void TriggerPlayerJailed(byte playerID, byte turnsLeft)
        {
            onPlayerJailed?.Invoke(new PlayerJailedArgs
            {
                PlayerID = new PlayerID(playerID),
                TurnsLeft = turnsLeft
            });
        }

        public struct GameOverArgs
        {
            public PlayerID WinnerID;
            public GameOverType GameOverType;
        }
        public event Action<GameOverArgs> onGameOver;
        private void TriggerGameOver(GameOverType gameOverType, byte winnerID)
        {
            onGameOver?.Invoke(new GameOverArgs
            {
                WinnerID = new PlayerID(winnerID),
                GameOverType = gameOverType
            });
        }

        public struct PlayerDoubleRentUpdateArgs
        {
            public PlayerID PlayerID;
            public bool Status;
        }
        public event Action<PlayerDoubleRentUpdateArgs> onPlayerDoubleRentUpdate;
        private void TriggerPlayerDoubleRentUpdate(byte playerID, bool status)
        {
            onPlayerDoubleRentUpdate?.Invoke(new PlayerDoubleRentUpdateArgs
            {
                PlayerID = new PlayerID(playerID),
                Status = status
            });
        }

        public struct PlayerJailCouponUpdateArgs
        {
            public PlayerID PlayerID;
            public bool Status;
        }
        public event Action<PlayerJailCouponUpdateArgs> onPlayerJailCouponUpdate;
        private void TriggerPlayerJailCouponUpdate(byte playerID, bool status)
        {
            onPlayerJailCouponUpdate?.Invoke(new PlayerJailCouponUpdateArgs
            {
                PlayerID = new PlayerID(playerID),
                Status = status
            });
        }

        public struct UpdatePlayerMoneyArgs
        {
            public PlayerID PlayerID;
            public int newMoneyAmount;
            public int oldMoneyAmount;
            public bool isIncreased
            {
                get
                {
                    return newMoneyAmount >= oldMoneyAmount;
                }
            }
        }
        /// <summary>
        /// Called when a player's account balance is updated.
        /// </summary>
        public event Action<UpdatePlayerMoneyArgs> onUpdatePlayerMoney;
        private void TriggerUpdatePlayerMoney(byte playerID, int newAmount, int oldAmount)
        {
            onUpdatePlayerMoney?.Invoke(new UpdatePlayerMoneyArgs
            {
                PlayerID = new PlayerID(playerID),
                newMoneyAmount = newAmount,
                oldMoneyAmount = oldAmount
            });
        }

        public struct UpdatePlayerColorArgs
        {
            public PlayerID PlayerID;
            public TeamColor Color;
        }
        /// <summary>
        /// Called when a player's preferred color is updated.
        /// </summary>
        public event Action<UpdatePlayerColorArgs> onUpdatePlayerColor;
        private void TriggerUpdatePlayerColor(byte playerID, TeamColor color)
        {
            onUpdatePlayerColor?.Invoke(new UpdatePlayerColorArgs
            {
                PlayerID = new PlayerID(playerID),
                Color = color
            });
        }

        public struct UpdateBoardPropertyArgs
        {
            public byte TileIndex;
            public GameBoard.TileProperty Tile;
        }
        /// <summary>
        /// Contains information about board properties.
        /// </summary>
        public event Action<UpdateBoardPropertyArgs> onUpdateBoardProperty;
        private void TriggerUpdateBoardProperty(byte tileIndex, GameBoard.TileProperty tile)
        {
            onUpdateBoardProperty?.Invoke(new UpdateBoardPropertyArgs
            {
                TileIndex = tileIndex,
                Tile = tile
            });
        }

        public struct DrawChanceCardArgs
        {
            public ChanceCard ChanceCard;
        }
        /// <summary>
        /// Called when a chancecard is drawn.
        /// </summary>
        public event Action<DrawChanceCardArgs> onDrawChanceCard;
        private void TriggerDrawChanceCard(ChanceCard chanceCard)
        {
            onDrawChanceCard?.Invoke(new DrawChanceCardArgs
            {
                ChanceCard = chanceCard
            });
        }

        public struct PropertyOfferArgs
        {
            public PlayerID PlayerID;
            public GameBoard.TileProperty.BuildingState BuildingLevel;
            public int BaseRent;
            public int Cost;
            public bool isAffordable;
        }
        public event Action<PropertyOfferArgs> onPropertyOffer;
        private void TriggerPropertyOffer(byte playerID, GameBoard.TileProperty.BuildingState buildingState, int rent, int cost, bool isAffordable)
        {
            onPropertyOffer?.Invoke(new PropertyOfferArgs
            {
                PlayerID = new PlayerID(playerID),
                BuildingLevel = buildingState,
                BaseRent = rent,
                Cost = cost,
                isAffordable = isAffordable
            });
        }

        public struct AuctionPropertyArgs
        {
            public PlayerID PlayerID;
            public int AuctionAmount;
        }
        public event Action<AuctionPropertyArgs> onAuctionProperty;
        private void TriggerPropertyAuction(byte playerID, int auctionAmount)
        {
            onAuctionProperty?.Invoke(new AuctionPropertyArgs
            {
                PlayerID = new PlayerID(playerID),
                AuctionAmount = auctionAmount
            });
        }

        public struct UpdateGroupDoubleRentArgs
        {
            public byte GroupID;
            public bool Status;
        }
        /// <summary>
        /// Called when double rent is triggered for a group.
        /// </summary>
        public event Action<UpdateGroupDoubleRentArgs> onUpdateGroupDoubleRent;
        private void TriggerUpdateGroupDoubleRent(byte groupID, bool status)
        {
            onUpdateGroupDoubleRent?.Invoke(new UpdateGroupDoubleRentArgs
            {
                GroupID = groupID,
                Status = status
            });
        }

        public struct PrisonCardOfferArgs
        {
            /// <summary>
            /// If <c>hasCard</c> is true then a purchase of prison card is needed.
            /// </summary>
            public bool hasCard;
        }
        /// <summary>
        /// Called when a prison card is received. Only for the player in question.
        /// </summary>
        public event Action<PrisonCardOfferArgs> onPrisonCardOffer;
        private void TriggerPrisonCardOffer(bool hasCard)
        {
            onPrisonCardOffer?.Invoke(
                new PrisonCardOfferArgs
                {
                    hasCard = hasCard
                });
        }
    }
}
