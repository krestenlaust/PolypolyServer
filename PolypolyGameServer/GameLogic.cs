using System;
using System.Collections.Generic;
using System.Linq;
using NetworkProtocol;
using static NetworkProtocol.Packet;
using static NetworkProtocol.GameBoard;

namespace PolypolyGame
{
    public class GameLogic
    {
        private enum PlayerChoice
        {
            PropertyOffer,
            JailOffer,
            Auction
        }

        private enum GameState
        {
            WaitingForPlayerChoice,
            WaitingForDiceThrow,
            DiceThrown,
            WaitingForAnimation,
            AnimationDone,
            NextTurn
        }
        
        public readonly GameBoard ServerBoard;
        public readonly Dictionary<byte, Player> Players = new Dictionary<byte, Player>();

        private readonly GameConfig gameConfig;
        private readonly Lobby lobby;
        private readonly Queue<byte[]> queuedPackets = new Queue<byte[]>();
        private readonly Random random = new Random();
        private TileProperty consideredProperty;
        private byte consideredPropertyPosition;
        private int auctionAmount;
        private byte currentPlayerId;
        private bool extraTurn;
        private bool hasGameStateUpdated = true;
        /// <summary>
        ///     State after animation is done.
        /// </summary>
        private LinkedList<GameState> gameStates;
        private GameState previusGameState = GameState.WaitingForAnimation;
        private (byte, byte) recentDiceThrowResult;
        private float timeTillSkipAnimation = 8;
        private int turnCount = -1;
        private PlayerChoice waitingForChoice;
        
        public GameLogic(Lobby gameServer, List<byte> clientIDs, GameConfig gameConfig)
        {
            lobby = gameServer;
            ServerBoard = GenerateStandardBoard();
            this.gameConfig = gameConfig;

            foreach (var item in clientIDs)
            {
                Players[item] = new Player();
            }
        }

        private void Print(string value)
        {
            lobby.log?.Print("[Game] " + value);
        }

        public void UpdateState()
        {
            /* // annoying
            if (timeTillSkip > 0)
            {
                timeTillSkip = Math.Max(timeTillSkip - fixedDeltaTime, 0);
            }*/

            if (gameStates is null)
            {
                gameStates = new LinkedList<GameState>();
                gameStates.AddLast(GameState.WaitingForAnimation);
            }

            GameState currentState = gameStates.First.Value;
            gameStates.RemoveFirst();
            hasGameStateUpdated = currentState != previusGameState;
            previusGameState = currentState;

            switch (currentState)
            {
                case GameState.WaitingForPlayerChoice:
                    switch (waitingForChoice)
                    {
                        case PlayerChoice.JailOffer:
                            if (Players[currentPlayerId].ReplyJailOffer is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            var jailCard = Players[currentPlayerId].ReplyJailOffer.Value;

                            if (jailCard)
                            {
                                if (!Players[currentPlayerId].hasJailCoupon)
                                    SubtractPlayerMoney(currentPlayerId, gameConfig.ChanceCardPrisonCouponWorth, false);
                                else
                                    UpdateJailCouponStatus(currentPlayerId, false);

                                UpdatePlayerJailturns(currentPlayerId, 0);

                                Print($"[{currentPlayerId}] Removed player from jail");
                            }

                            ThrowDiceNetwork(currentPlayerId, GetDiceResult());
                            Players[currentPlayerId].ReplyJailOffer = null;

                            gameStates.AddLast(GameState.DiceThrown);
                            break;
                        case PlayerChoice.PropertyOffer:
                            if (Players[currentPlayerId].ReplyPropertyOffer is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            var buyProperty = Players[currentPlayerId].ReplyPropertyOffer.Value;

                            if (buyProperty)
                            {
                                // upgrade building
                                consideredProperty.BuildingLevel = (TileProperty.BuildingState)((byte)consideredProperty.BuildingLevel + 1);
                                consideredProperty.Owner = currentPlayerId;

                                int cost;
                                if (consideredProperty.BuildingLevel == TileProperty.BuildingState.Level1)
                                {
                                    cost = consideredProperty.BaseCost;

                                    // TODO: implement group trigger.
                                    // true if player owns all properties of this kind.
                                    bool groupMonopoly = CheckGroupMonopoly(consideredProperty.GroupID, consideredProperty.Owner);
                                    UpdateGroupMonopoly(consideredProperty.GroupID, groupMonopoly);

                                    if (groupMonopoly)
                                    {
                                        // check if other group is a monopoly group.
                                        byte otherGroupID = (byte)(consideredProperty.GroupID - consideredProperty.GroupID % 2);
                                        if (otherGroupID == consideredProperty.GroupID)
                                            otherGroupID++;

                                        // monopoly!
                                        if (CheckGroupMonopoly(otherGroupID, consideredProperty.Owner))
                                        {
                                            GameFinished(GameOverType.Monopoly, consideredProperty.Owner);
                                        }
                                    }
                                }
                                else
                                {
                                    cost = consideredProperty.UpgradeCost;
                                }

                                SubtractPlayerMoney(currentPlayerId, cost, false);
                                UpdateBoardProperty(consideredPropertyPosition, consideredProperty);
                            }
                            
                            SyncronizeEffects();

                            consideredProperty = null;
                            Players[currentPlayerId].ReplyPropertyOffer = null;
                            break;
                        case PlayerChoice.Auction:
                            if (Players[currentPlayerId].ReplyAuctionIndex is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            byte auctionIndex = Players[currentPlayerId].ReplyAuctionIndex.Value;

                            TileProperty propertyForAuction = ServerBoard.PropertyTiles[auctionIndex];

                            if (propertyForAuction?.Owner != currentPlayerId)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                Print("Player does not own selected property");
                                Players[currentPlayerId].ReplyAuctionIndex = null;
                                break;
                            }

                            int propertyValue = propertyForAuction.Value;

                            if (propertyValue < auctionAmount)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                Print("Property not worth enough");
                                Players[currentPlayerId].ReplyAuctionIndex = null;
                                break;
                            }

                            // check whether group monopoly should be lifted after auction.
                            if (CheckGroupMonopoly(propertyForAuction.GroupID, propertyForAuction.Owner))
                            {
                                // ...was 2x rent, now ain't.
                                UpdateGroupMonopoly(propertyForAuction.GroupID, false);
                            }

                            propertyForAuction.BuildingLevel = TileProperty.BuildingState.Unpurchased;
                            propertyForAuction.Owner = byte.MaxValue;

                            AddPlayerMoney(currentPlayerId, propertyValue);
                            UpdateBoardProperty(auctionIndex, propertyForAuction);

                            //SyncronizeEffects();

                            Print("Auctioned building");

                            Players[currentPlayerId].ReplyAuctionIndex = null;
                            break;
                    }

                    break;
                case GameState.WaitingForAnimation:
                    var animationDone = Players.All(p => p.Value.isAnimationDone);

                    if (animationDone || timeTillSkipAnimation <= 0)
                    {
                        if (turnCount == -1)
                        {
                            // First turn ever. Clients have loaded the scene.
                            Print("Game has started");

                            SyncronizeBoard();

                            foreach (var playerId in Players.Keys)
                                AddPlayerMoney(playerId, gameConfig.StartMoney);

                            gameStates.AddFirst(GameState.NextTurn);
                        }

                        SyncronizeEffects();
                        ClearAnimationDone();
                        
                        break;
                    }

                    // add waiting state if still waiting.
                    gameStates.AddFirst(GameState.WaitingForAnimation);
                    break;
                case GameState.WaitingForDiceThrow:
                    if (hasGameStateUpdated) 
                        Print("Waiting for dice throw");

                    if (timeTillSkipAnimation <= 0)
                    {
                        ThrowDiceNetwork(currentPlayerId, GetDiceResult());
                    }

                    if (recentDiceThrowResult.Item1 != 0)
                    {
                        gameStates.AddLast(GameState.DiceThrown);
                        break;
                    }

                    // add waiting state if still waiting.
                    gameStates.AddFirst(GameState.WaitingForDiceThrow);
                    break;
                case GameState.DiceThrown:
                    gameStates.AddFirst(GameState.WaitingForAnimation);

                    Print($"{currentPlayerId}'s dice: {recentDiceThrowResult.Item1}, {recentDiceThrowResult.Item2}");

                    extraTurn = false;

                    var diceResult = (byte) (recentDiceThrowResult.Item1 + recentDiceThrowResult.Item2);
                    var diceDouble = recentDiceThrowResult.Item1 == recentDiceThrowResult.Item2;

                    // Check if passed go
                    if (Players[currentPlayerId].Position + diceResult >= ServerBoard.Size)
                        AddPlayerMoney(currentPlayerId, gameConfig.PassGoReward);

                    if (diceDouble)
                    {
                        Players[currentPlayerId].ConsecutiveDoubleDice++;
                        Print(Players[currentPlayerId].ConsecutiveDoubleDice + " consecutive double dice");

                        if (Players[currentPlayerId].ConsecutiveDoubleDice >= 3)
                            SendToJail(currentPlayerId);
                        else
                            extraTurn = true;

                        if (Players[currentPlayerId].JailTurns > 0)
                        {
                            UpdatePlayerJailturns(currentPlayerId, 0);
                            Players[currentPlayerId].ConsecutiveDoubleDice = 0;
                        }
                    }
                    else
                    {
                        Players[currentPlayerId].ConsecutiveDoubleDice = 0;
                    }

                    if (Players[currentPlayerId].JailTurns > 0)
                    {
                        Print("Skipping because " + currentPlayerId + " is in jail");
                        UpdatePlayerJailturns(currentPlayerId, (byte)(Players[currentPlayerId].JailTurns - 1));

                        SyncronizeEffects();
                        gameStates.AddLast(GameState.NextTurn);
                        break;
                    }

                    int newPosition = (Players[currentPlayerId].Position + diceResult) % ServerBoard.Size;
                    UpdatePlayerPosition(currentPlayerId, (byte)newPosition, MoveType.Walk);
                    SyncronizeEffects();

                    // Handle player land location
                    HandlePlayerLandOnTile();

                    gameStates.AddLast(GameState.NextTurn);
                    break;
                case GameState.NextTurn:
                    // clear previus result
                    recentDiceThrowResult.Item1 = 0;
                    recentDiceThrowResult.Item2 = 0;

                    if (!extraTurn)
                    {
                        turnCount++;

                        // increment player turn
                        currentPlayerId = GetNextPlayerID();
                    }

                    if (hasGameStateUpdated)
                        Print($"Turn {turnCount}, Player: {currentPlayerId}");

                    // notify clients of turn start
                    queuedPackets.Enqueue(Construct.UpdatePlayerTurn(currentPlayerId));
                    SyncronizeEffects();


                    timeTillSkipAnimation = 5;
                    gameStates.AddFirst(GameState.WaitingForDiceThrow);
                    break;
            }

            if (gameStates.Count == 0)
            {
                Print("Error has occurred");
            }
        }

        private void HandlePlayerLandOnTile()
        {
            byte position = (byte)Players[currentPlayerId].Position;

            switch (ServerBoard.TileTypes[position])
            {
                case TileType.Jail:
                case TileType.Nothing:
                    break;
                case TileType.GotoJail:
                    SendToJail(currentPlayerId);
                    
                    // Animation is queued on client anyway.
                    //SyncronizeEffects();

                    break;
                case TileType.Train:
                    AddPlayerMoney(currentPlayerId, gameConfig.TressureTileReward);
                    break;
                case TileType.Tax:
                    SubtractPlayerMoney(currentPlayerId, gameConfig.TaxAmount, true);
                    break;
                case TileType.BigTax:
                    SubtractPlayerMoney(currentPlayerId, gameConfig.TaxAmount * 2, true);
                    break;
                case TileType.Upkeep:
                    int ownedProperties = ServerBoard.PropertyTiles.Count(p => p?.Owner == currentPlayerId);

                    int upkeepCost = gameConfig.TaxAmount / 5 * ownedProperties;
                    
                    SubtractPlayerMoney(currentPlayerId, upkeepCost, true);
                    break;
                case TileType.Property:
                    TileProperty property = ServerBoard.PropertyTiles[position];

                    bool canAfford = Players[currentPlayerId].Money >= property.BaseCost;

                    // unpurchased or owned by current player and building is maxed out.
                    if ((property.Owner == byte.MaxValue || property.Owner == currentPlayerId) && property.BuildingLevel != TileProperty.BuildingState.Level3)
                    {
                        int cost = property.Owner == byte.MaxValue ? property.BaseCost : property.UpgradeCost;

                        MakePropertyOffer(currentPlayerId, property, canAfford, cost);

                        if (!canAfford) break;

                        consideredProperty = property;
                        consideredPropertyPosition = position;

                        waitingForChoice = PlayerChoice.PropertyOffer;
                        gameStates.AddLast(GameState.WaitingForPlayerChoice);
                    }
                    else if (property.Owner != currentPlayerId && property.Owner != byte.MaxValue)
                    {
                        if (Players[property.Owner].JailTurns != 0 && !gameConfig.CollectRentInPrison)
                        {
                            Print("Player is in prison, no dice.");
                            break;
                        }

                        int rentCost = property.Rent;

                        // player has double rent coupon, what a lucky fella.
                        if (Players[currentPlayerId].hasDoubleRentCoupon)
                        {
                            rentCost *= 2;
                            DoubleRentCouponStatus(currentPlayerId, false);
                        }

                        // is double rent.
                        if (CheckGroupMonopoly(property.GroupID, property.Owner))
                        {
                            rentCost *= 2;
                        }

                        var moneyPaid = SubtractPlayerMoney(currentPlayerId, rentCost, true);
                        AddPlayerMoney(property.Owner, moneyPaid);
                        Print("Paid rent");
                    }
                    break;
                case TileType.ChanceCard:
                    var chanceCards = Enum.GetValues(typeof(ChanceCard));
                    var card = (ChanceCard) chanceCards.GetValue(random.Next(chanceCards.Length));

                    ChanceCardDrawn(card);

                    Print($"Chance card: {Enum.GetName(typeof(ChanceCard), card)}");

                    switch (card)
                    {
                        case ChanceCard.GotoPrison:
                            SendToJail(currentPlayerId);

                            break;
                        case ChanceCard.MoneyAdd:
                            AddPlayerMoney(currentPlayerId, gameConfig.ChanceCardMoneyReward);

                            break;
                        case ChanceCard.MoneyDeduct:
                            SubtractPlayerMoney(currentPlayerId, gameConfig.ChanceCardMoneyPenalty, true);

                            break;
                        case ChanceCard.DoubleRentCoupon:
                            DoubleRentCouponStatus(currentPlayerId, true);

                            break;
                        case ChanceCard.PrisonCoupon:
                            if (Players[currentPlayerId].hasJailCoupon)
                            {
                                // give reward instead of extra coupon.
                                AddPlayerMoney(currentPlayerId, gameConfig.ChanceCardPrisonCouponWorth);
                            }
                            else
                            {
                                UpdateJailCouponStatus(currentPlayerId, true);
                            }

                            break;
                        case ChanceCard.TrainCoupon:
                            UpdatePlayerPosition(currentPlayerId, ServerBoard.TraintileIndex, MoveType.Walk);

                            HandlePlayerLandOnTile();
                            break;
                        case ChanceCard.MoveFourTilesBack:
                            UpdatePlayerPosition(currentPlayerId, (byte) (position - 4), MoveType.Walk);

                            HandlePlayerLandOnTile();
                            break;
                        case ChanceCard.Go:
                            UpdatePlayerPosition(currentPlayerId, 0, MoveType.Walk);
                            AddPlayerMoney(currentPlayerId, gameConfig.PassGoReward);
                            break;
                        case ChanceCard.ForceAuction:
                            if (ServerBoard.PropertyTiles.Any(p => p?.Owner == currentPlayerId))
                            {
                                TriggerAuction(0);
                            }
                            break;
                    }
                    break;
            }
        }


        public void DoubleRentCouponStatus(byte playerID, bool status)
        {
            Players[playerID].hasDoubleRentCoupon = status;

            queuedPackets.Enqueue(Construct.UpdatePlayerDoubleRent(playerID, status));
        }

        public void UpdateJailCouponStatus(byte playerID, bool status)
        {
            Players[playerID].hasJailCoupon = status;

            queuedPackets.Enqueue(Construct.UpdatePlayerJailCoupon(playerID, status));
        }

        /// <summary>
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="loss"></param>
        /// <returns>The amount subtracted</returns>
        public int SubtractPlayerMoney(byte playerID, int loss, bool triggerAuction)
        {
            SetPlayerMoney(playerID, Players[playerID].Money - loss);

            if (Players[playerID].Money < 0 && triggerAuction)
            {
                int mostExpensive = ServerBoard.PropertyTiles.Max(p =>
                {
                    if (p is null)
                    {
                        return 0;
                    }

                    if (p.Owner != playerID)
                    {
                        return 0;
                    }

                    return p.Value;
                });

                int auctionAmount = Math.Abs(Players[playerID].Money);

                if (auctionAmount > mostExpensive)
                {
                    Players[playerID].isBankrupt = true;
                    Print($"{playerID} is now bankrupt");
                    SetPlayerMoney(playerID, 0);
                    queuedPackets.Enqueue(Construct.PlayerBankrupt(playerID));

                    // TODO: Maybe clear list too?
                    return loss + Players[playerID].Money;
                }
                else
                {
                    TriggerAuction(auctionAmount);
                }
            }

            return loss;
        }

        /// <summary>
        ///     Updates player values <c>Position</c>.
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="newPosition"></param>
        /// <param name="moveType"></param>
        public void UpdatePlayerPosition(byte playerID, byte newPosition, MoveType moveType)
        {
            newPosition = (byte)(newPosition % ServerBoard.Size);

            Players[playerID].Position = newPosition;

            var packet = Construct.UpdatePlayerPosition(playerID, newPosition, moveType);
            queuedPackets.Enqueue(packet);
        }

        public void AddPlayerMoney(byte playerID, int income)
        {
            SetPlayerMoney(playerID, Players[playerID].Money + income);
        }

        /// <summary>
        ///     Updates a gameServer.Players jailtime and notifies all clients.
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="jailturns"></param>
        public void UpdatePlayerJailturns(byte playerID, byte jailturns)
        {
            Players[playerID].JailTurns = jailturns;

            queuedPackets.Enqueue(Construct.PlayerJail(playerID, jailturns));
        }

        /// <summary>
        /// Updates player value <c>Money</c>.
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="newAmount"></param>
        public void SetPlayerMoney(byte playerID, int newAmount)
        {
            Players[playerID].Money = newAmount;

            Print($"[{playerID}] new balance: {newAmount}");

            var previousAmount = Players[playerID].Money;
            var isIncreased = newAmount > previousAmount;

            var packet = Construct.PlayerUpdateMoney(playerID, newAmount, isIncreased);

            queuedPackets.Enqueue(packet);
        }

        /// <summary>
        /// returns true if group monopoly is activated for a particular group.
        /// </summary>
        private void GameFinished(GameOverType gameOverType, byte winner)
        {
            Print("Game over! " + Enum.GetName(typeof(GameOverType), gameOverType) + ", winner: " + winner);
            queuedPackets.Enqueue(Construct.GameOver(gameOverType, winner));
            
            SyncronizeEffects();
            lobby.EndGame();
        }

        private bool CheckGroupMonopoly(byte groupID)
        {
            IEnumerable<TileProperty> groupTiles = ServerBoard.PropertyTiles.Where(p => p.GroupID == groupID);
            byte owner = groupTiles.First().Owner;

            if (owner == byte.MaxValue)
            {
                return false;
            }

            return !groupTiles.Skip(1).Any(p => p.Owner != owner);
        }

        private bool CheckGroupMonopoly(byte groupID, byte owner) => !ServerBoard.PropertyTiles.Where(p => p?.GroupID == groupID).Any(p => p.Owner != owner);

        private void ClearAnimationDone()
        {
            foreach (var id in Players.Keys) Players[id].isAnimationDone = false;
            Print("Reset animation done signals");
        }

        private byte GetNextPlayerID()
        {
            byte newTurn = currentPlayerId;

            int playerCount = Players.Count;

            int i = 0;
            for (int id = newTurn; i < playerCount; id = (id + 1) % playerCount, i++)
            {
                // skip player who had turn now
                if (i == 0)
                    continue;

                if (Players[(byte)id].isBankrupt)
                {
                    continue;
                }

                newTurn = (byte)id;
                break;
            }

            return newTurn;
        }

        /// <summary>
        /// Throws dice for current player.
        /// </summary>
        /// <param name="dieResult"></param>
        public void ThrowDiceNetwork((byte, byte) dieResult) => ThrowDiceNetwork(currentPlayerId, dieResult);

        /// <summary>
        /// </summary>
        /// <param name="playerID"></param>
        public void ThrowDiceNetwork(byte playerID, (byte, byte) dieResult)
        {
            if (playerID != currentPlayerId)
            {
                Print("Wrong player requested dice roll");
                return;
            }

            recentDiceThrowResult = dieResult;

            lobby.BroadcastPacket(Construct.DicerollResult(playerID, recentDiceThrowResult.Item1, recentDiceThrowResult.Item2));
        }

        public void SyncronizeEffects()
        {
            Print("Syncronizing");

            while (queuedPackets.Count > 0)
            {
                lobby.BroadcastPacket(queuedPackets.Dequeue());
            }
        }

        /// <summary>
        ///     Updates player values <c>Position</c> and <c>JailTurns</c>.
        /// </summary>
        /// <param name="playerID"></param>
        public void SendToJail(byte playerID)
        {
            // no jail for you sir!
            if (Players[playerID].hasJailCoupon)
            {
                UpdateJailCouponStatus(playerID, false);
                return;
            }

            Players[playerID].Position = ServerBoard.JailtileIndex;
            Players[playerID].JailTurns = gameConfig.SentenceDuration;

            queuedPackets.Enqueue(Construct.PlayerJail(playerID, gameConfig.SentenceDuration));
            queuedPackets.Enqueue(Construct.UpdatePlayerPosition(playerID, ServerBoard.JailtileIndex, MoveType.DirectMove));

            if (gameStates?.First?.Value != GameState.WaitingForAnimation)
            {
                gameStates.AddLast(GameState.WaitingForAnimation);
            }

            // disabled for now
            //waitingForChoice = PlayerChoice.JailOffer;
            //gameStates.AddLast(GameState.WaitingForPlayerChoice);
            //MakeJailOffer(playerID);
        }

        private void ChanceCardDrawn(ChanceCard card)
        {
            queuedPackets.Enqueue(Construct.DrawChanceCard(card));
        }

        private void MakeJailOffer(byte playerID)
        {
            queuedPackets.Enqueue(Construct.PrisonCardOffer(Players[playerID].hasJailCoupon));
        }

        private void MakePropertyOffer(byte playerID, TileProperty property, bool canAfford, int cost)
        {
            queuedPackets.Enqueue(Construct.PropertyOffer(playerID, property.BuildingLevel, property.Rent,
                cost, canAfford));
        }

        private void UpdateGroupMonopoly(byte groupID, bool status)
        {
            Print("GroupID: " + groupID + " : " + status);
            queuedPackets.Enqueue(Construct.UpdateGroupDoubleRent(groupID, status));
        }

        private void UpdateBoardProperty(byte tileID, TileProperty tile)
        {
            queuedPackets.Enqueue(Construct.UpdateBoardProperty(tileID, tile));
        }

        private void SyncronizeBoard()
        {
            var propertyCount = ServerBoard.PropertyTiles.Count(Tile => !(Tile is null));

            var packet = new byte[Construct.SIZE_UpdateBoardProperty * propertyCount];

            var packetPtr = 0;
            for (var i = 0; i < ServerBoard.PropertyTiles.Length; i++)
            {
                if (ServerBoard.PropertyTiles[i] == null)
                    continue;

                var partialPacket = Construct.UpdateBoardProperty((byte) i, ServerBoard.PropertyTiles[i]);
                partialPacket.CopyTo(packet, packetPtr);
                packetPtr += Construct.SIZE_UpdateBoardProperty;
            }

            lobby.BroadcastPacket(packet);
        }

        private void TriggerAuction(int amountToAuction)
        {
            auctionAmount = amountToAuction;

            gameStates.AddFirst(GameState.WaitingForPlayerChoice);
            waitingForChoice = PlayerChoice.Auction;

            queuedPackets.Enqueue(Construct.AuctionProperty(currentPlayerId, amountToAuction));
            SyncronizeEffects();
        }

        public (byte, byte) GetDiceResult()
        {
            // Maybe turn this into a function returning 2 bytes instead to make animations on client-side.

            return ((byte) random.Next(1, 7), (byte) random.Next(1, 7));
        }
    }
}