using System;
using System.Collections.Generic;
using System.Linq;
using static PolypolyGameServer.ServerBoard;

namespace PolypolyGameServer
{
    class GameLogic
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
        
        public readonly GameConfig gameConfig = GameConfig.StandardConfig;
        public readonly ServerBoard serverBoard;
        public bool isGameInProgress { get; internal set; }
        
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
        
        public GameLogic(Lobby gameServer)
        {
            this.lobby = gameServer;
            serverBoard = GenerateStandardBoard();
        }

        private void Print(string value)
        {
            lobby.log.Print("[Game] " + value);
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
                            if (lobby.Players[currentPlayerId].ReplyJailOffer is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            var jailCard = lobby.Players[currentPlayerId].ReplyJailOffer.Value;

                            if (jailCard)
                            {
                                if (!lobby.Players[currentPlayerId].hasJailCoupon)
                                    SubtractPlayerMoney(currentPlayerId, gameConfig.ChanceCardPrisonCouponWorth, false);
                                else
                                    lobby.Players[currentPlayerId].hasJailCoupon = false;

                                UpdatePlayerJailturns(currentPlayerId, 0);

                                Print($"[{currentPlayerId}] Removed player from jail");
                            }

                            ThrowDiceNetwork(currentPlayerId);
                            lobby.Players[currentPlayerId].ReplyJailOffer = null;

                            gameStates.AddLast(GameState.DiceThrown);
                            break;
                        case PlayerChoice.PropertyOffer:
                            if (lobby.Players[currentPlayerId].ReplyPropertyOffer is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            var buyProperty = lobby.Players[currentPlayerId].ReplyPropertyOffer.Value;

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
                                    bool groupMonopoly = IsGroupMonopolyActivated(consideredProperty.GroupID, consideredProperty.Owner);
                                    UpdateGroupMonopoly(consideredProperty.GroupID, groupMonopoly);

                                    if (groupMonopoly)
                                    {
                                        // check if other group is a monopoly group.
                                        byte otherGroupID = (byte)(consideredProperty.GroupID - consideredProperty.GroupID % 2);
                                        if (otherGroupID == consideredProperty.GroupID)
                                            otherGroupID++;

                                        // monopoly!
                                        if (IsGroupMonopolyActivated(otherGroupID, consideredProperty.Owner))
                                        {
                                            GameFinished(Packet.GameOverType.Monopoly, consideredProperty.Owner);
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
                            lobby.Players[currentPlayerId].ReplyPropertyOffer = null;
                            break;
                        case PlayerChoice.Auction:
                            if (lobby.Players[currentPlayerId].ReplyAuctionIndex is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            byte auctionIndex = lobby.Players[currentPlayerId].ReplyAuctionIndex.Value;

                            TileProperty propertyForAuction = serverBoard.PropertyTiles[auctionIndex];

                            if (propertyForAuction?.Owner != currentPlayerId)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                Print("Player does not own selected property");
                                lobby.Players[currentPlayerId].ReplyAuctionIndex = null;
                                break;
                            }

                            int propertyValue = propertyForAuction.Value;

                            if (propertyValue < auctionAmount)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                Print("Property not worth enough");
                                lobby.Players[currentPlayerId].ReplyAuctionIndex = null;
                                break;
                            }

                            // check whether group monopoly should be lifted after auction.
                            if (IsGroupMonopolyActivated(propertyForAuction.GroupID, propertyForAuction.Owner))
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

                            lobby.Players[currentPlayerId].ReplyAuctionIndex = null;
                            break;
                    }

                    break;
                case GameState.WaitingForAnimation:
                    var animationDone = lobby.Players.All(p => p.Value.isAnimationDone);

                    if (animationDone || timeTillSkipAnimation <= 0)
                    {
                        if (turnCount == -1)
                        {
                            // First turn ever. Clients have loaded the scene.
                            Print("Game has started");

                            SyncronizeBoard();

                            foreach (var playerId in lobby.Players.Keys)
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
                    if (hasGameStateUpdated) Print("Waiting for dice throw");

                    if (timeTillSkipAnimation <= 0) ThrowDiceNetwork(currentPlayerId);

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
                    if (lobby.Players[currentPlayerId].Position + diceResult >= serverBoard.Size)
                        AddPlayerMoney(currentPlayerId, gameConfig.PassGoReward);

                    if (diceDouble)
                    {
                        lobby.Players[currentPlayerId].ConsecutiveDoubleDice++;
                        Print(lobby.Players[currentPlayerId].ConsecutiveDoubleDice + " consecutive double dice");

                        if (lobby.Players[currentPlayerId].ConsecutiveDoubleDice >= 3)
                            SendToJail(currentPlayerId);
                        else
                            extraTurn = true;

                        if (lobby.Players[currentPlayerId].JailTurns > 0)
                        {
                            UpdatePlayerJailturns(currentPlayerId, 0);
                            lobby.Players[currentPlayerId].ConsecutiveDoubleDice = 0;
                        }
                    }
                    else
                    {
                        lobby.Players[currentPlayerId].ConsecutiveDoubleDice = 0;
                    }

                    if (lobby.Players[currentPlayerId].JailTurns > 0)
                    {
                        Print("Skipping because " + currentPlayerId + " is in jail");
                        UpdatePlayerJailturns(currentPlayerId,
                            (byte) (lobby.Players[currentPlayerId].JailTurns - 1));

                        SyncronizeEffects();
                        gameStates.AddLast(GameState.NextTurn);
                        break;
                    }

                    var newPosition = (lobby.Players[currentPlayerId].Position + diceResult) % serverBoard.Size;
                    UpdatePlayerPosition(currentPlayerId, (byte) newPosition, Packet.MoveType.Walk);
                    SyncronizeEffects();

                    // Handle player land location
                    HandlePlayerLandOnTile(currentPlayerId);

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
                        currentPlayerId = NextPlayerID();
                    }

                    if (hasGameStateUpdated) Print($"Turn {turnCount}, Player: {currentPlayerId}");

                    // notify clients of turn start
                    var packet = Packet.Construct.UpdatePlayerTurn(currentPlayerId);
                    queuedPackets.Enqueue(packet);
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

        private void HandlePlayerLandOnTile(byte playerID)
        {
            byte position = (byte) lobby.Players[playerID].Position;

            switch (serverBoard.TileTypes[position])
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
                    SubtractPlayerMoney(currentPlayerId, gameConfig.TaxAmount, true);
                    break;
                case TileType.Property:
                    var property = serverBoard.PropertyTiles[position];

                    bool canAfford = lobby.Players[playerID].Money >= property.BaseCost;

                    // unpurchased or owned by current player and building is maxed out.
                    if ((property.Owner == byte.MaxValue || property.Owner == currentPlayerId) && property.BuildingLevel != TileProperty.BuildingState.Level3)
                    {
                        MakePropertyOffer(playerID, property, canAfford);

                        if (!canAfford) break;

                        consideredProperty = property;
                        consideredPropertyPosition = position;

                        waitingForChoice = PlayerChoice.PropertyOffer;
                        gameStates.AddLast(GameState.WaitingForPlayerChoice);
                    }
                    else if (property.Owner != currentPlayerId && property.Owner != byte.MaxValue)
                    {
                        if (lobby.Players[property.Owner].JailTurns != 0 && !gameConfig.CollectRentInPrison)
                        {
                            Print("Player is in prison, no dice.");
                            break;
                        }

                        int rentCost = property.Rent;

                        // player has double rent coupon, what a lucky fella.
                        if (lobby.Players[currentPlayerId].hasDoubleRentCoupon)
                        {
                            rentCost *= 2;
                            lobby.Players[currentPlayerId].hasDoubleRentCoupon = false;
                        }

                        // is double rent.
                        if (IsGroupMonopolyActivated(property.GroupID, property.Owner))
                        {
                            rentCost *= 2;
                        }

                        var moneyPaid = SubtractPlayerMoney(currentPlayerId, rentCost, true);
                        AddPlayerMoney(property.Owner, moneyPaid);
                        Print("Paid rent");
                    }
                    break;
                case TileType.ChanceCard:
                    var chanceCards = Enum.GetValues(typeof(Packet.ChanceCard));
                    var card =
                        (Packet.ChanceCard) chanceCards.GetValue(random.Next(chanceCards.Length));

                    ChanceCardDrawn(card);

                    Print($"Chance card: {Enum.GetName(typeof(Packet.ChanceCard), card)}");

                    switch (card)
                    {
                        case Packet.ChanceCard.GotoPrison:
                            SendToJail(currentPlayerId);

                            break;
                        case Packet.ChanceCard.MoneyAdd:
                            AddPlayerMoney(currentPlayerId, gameConfig.ChanceCardMoneyReward);

                            break;
                        case Packet.ChanceCard.MoneyDeduct:
                            SubtractPlayerMoney(currentPlayerId, gameConfig.ChanceCardMoneyPenalty, true);

                            break;
                        case Packet.ChanceCard.DoubleRentCoupon:
                            lobby.Players[currentPlayerId].hasDoubleRentCoupon = true;

                            break;
                        case Packet.ChanceCard.PrisonCoupon:
                            if (lobby.Players[currentPlayerId].hasJailCoupon)
                            {
                                // give reward instead of extra coupon.
                                AddPlayerMoney(currentPlayerId, gameConfig.ChanceCardPrisonCouponWorth);
                            }
                            else
                            {
                                lobby.Players[currentPlayerId].hasJailCoupon = true;
                                // TODO: Network update
                            }

                            break;
                        case Packet.ChanceCard.TrainCoupon:
                            UpdatePlayerPosition(currentPlayerId, serverBoard.TraintileIndex,
                                Packet.MoveType.Walk);

                            HandlePlayerLandOnTile(playerID);
                            break;
                        case Packet.ChanceCard.MoveFourTilesBack:
                            UpdatePlayerPosition(currentPlayerId, (byte) (position - 4),
                                Packet.MoveType.Walk);

                            HandlePlayerLandOnTile(playerID);
                            break;
                        case Packet.ChanceCard.Go:
                            UpdatePlayerPosition(currentPlayerId, 0, Packet.MoveType.Walk);
                            AddPlayerMoney(currentPlayerId, gameConfig.PassGoReward);
                            break;
                        case Packet.ChanceCard.ForceAuction:
                            if (serverBoard.PropertyTiles.Any(p => p?.Owner == currentPlayerId))
                            {
                                TriggerAuction(0);
                            }
                            break;
                    }
                    break;
            }
        }

        /// <summary>
        /// returns true if group monopoly is activated for a particular group.
        /// </summary>
        /// <param name="groupID"></param>
        /// <returns></returns>
        private bool IsGroupMonopolyActivated(byte groupID)
        {
            IEnumerable<TileProperty> groupTiles = serverBoard.PropertyTiles.Where(p => p.GroupID == groupID);
            byte owner = groupTiles.First().Owner;

            if (owner == byte.MaxValue)
            {
                return false;
            }

            return !groupTiles.Skip(1).Any(p => p.Owner != owner);
        }
        
        private void GameFinished(Packet.GameOverType gameOverType, byte winner)
        {
            isGameInProgress = false;
            Print("Game over! " + Enum.GetName(typeof(Packet.GameOverType), gameOverType) + " Winner: " + winner);
            queuedPackets.Enqueue(Packet.Construct.GameOver(gameOverType, winner));
        }

        private bool IsGroupMonopolyActivated(byte groupID, byte owner) => !serverBoard.PropertyTiles.Where(p => p?.GroupID == groupID).Any(p => p.Owner != owner);

        private void ClearAnimationDone()
        {
            foreach (var id in lobby.Players.Keys) lobby.Players[id].isAnimationDone = false;
            Print("Reset animation done signals");
        }

        private byte NextPlayerID()
        {
            byte newTurn = currentPlayerId;

            int playerCount = lobby.Players.Count;

            int i = 0;
            for (int id = newTurn; i < playerCount; id = (id + 1) % playerCount, i++)
            {
                // skip player who had turn now
                if (i == 0)
                    continue;

                if (lobby.Players[(byte)id].isBankrupt)
                {
                    continue;
                }

                newTurn = (byte)id;
                break;
            }

            return newTurn;
        }

        /// <summary>
        /// </summary>
        /// <param name="playerID"></param>
        public void ThrowDiceNetwork(byte playerID)
        {
            if (playerID != currentPlayerId)
            {
                Print("Wrong player requested dice roll");
                return;
            }

            recentDiceThrowResult = RollDice();

            var packet = Packet.Construct.DicerollResult(playerID, recentDiceThrowResult.Item1,
                recentDiceThrowResult.Item2);

            lobby.BroadcastPacket(packet);
        }

        private void SyncronizeEffects()
        {
            Print("Syncronizing");
            while (queuedPackets.Count > 0)
            {
                lobby.BroadcastPacket(queuedPackets.Dequeue());
            }
        }

        /// <summary>
        ///     Updates player values <c>Position</c>.
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="newPosition"></param>
        /// <param name="moveType"></param>
        private void UpdatePlayerPosition(byte playerID, byte newPosition, Packet.MoveType moveType)
        {
            newPosition = (byte) (newPosition % serverBoard.Size);

            lobby.Players[playerID].Position = newPosition;

            var packet = Packet.Construct.UpdatePlayerPosition(playerID, newPosition, moveType);
            queuedPackets.Enqueue(packet);
        }

        private void ChanceCardDrawn(Packet.ChanceCard card)
        {
            queuedPackets.Enqueue(Packet.Construct.DrawChanceCard(card));
        }

        /// <summary>
        ///     Updates player values <c>Position</c> and <c>JailTurns</c>.
        /// </summary>
        /// <param name="playerID"></param>
        private void SendToJail(byte playerID)
        {
            lobby.Players[playerID].Position = serverBoard.JailtileIndex;
            lobby.Players[playerID].JailTurns = gameConfig.SentenceDuration;

            queuedPackets.Enqueue(Packet.Construct.PlayerJail(playerID, gameConfig.SentenceDuration));
            queuedPackets.Enqueue(Packet.Construct.UpdatePlayerPosition(playerID, serverBoard.JailtileIndex, Packet.MoveType.DirectMove));

            if (gameStates?.First?.Value != GameState.WaitingForAnimation)
            {
                gameStates.AddLast(GameState.WaitingForAnimation);
            }

            // disabled for now
            //waitingForChoice = PlayerChoice.JailOffer;
            //gameStates.AddLast(GameState.WaitingForPlayerChoice);
            //MakeJailOffer(playerID);
        }

        private void MakeJailOffer(byte playerID)
        {
            queuedPackets.Enqueue(Packet.Construct.PrisonCardOffer(lobby.Players[playerID].hasJailCoupon));
        }

        private void MakePropertyOffer(byte playerID, TileProperty property, bool canAfford)
        {
            queuedPackets.Enqueue(Packet.Construct.PropertyOffer(playerID, property.BuildingLevel, property.Rent,
                property.BaseCost, canAfford));
        }
        
        /// <summary>
        /// Updates player value <c>Money</c>.
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="newAmount"></param>
        private void SetPlayerMoney(byte playerID, int newAmount)
        {
            lobby.Players[playerID].Money = newAmount;

            Print($"[{playerID}] new balance: {newAmount}");

            var previousAmount = lobby.Players[playerID].Money;
            var isIncreased = newAmount > previousAmount;

            var packet = Packet.Construct.PlayerUpdateMoney(playerID, newAmount, isIncreased);

            queuedPackets.Enqueue(packet);
        }

        private void UpdateGroupMonopoly(byte groupID, bool status)
        {
            Print("GroupID: " + groupID + " : " + status);
            queuedPackets.Enqueue(Packet.Construct.UpdateGroupDoubleRent(groupID, status));
        }

        /// <summary>
        ///     Updates a gameServer.Players jailtime and notifies all clients.
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="jailturns"></param>
        private void UpdatePlayerJailturns(byte playerID, byte jailturns)
        {
            lobby.Players[playerID].JailTurns = jailturns;
            
            queuedPackets.Enqueue(Packet.Construct.PlayerJail(playerID, jailturns));
        }

        private void UpdateBoardProperty(byte tileID, TileProperty tile)
        {
            queuedPackets.Enqueue(Packet.Construct.UpdateBoardProperty(tileID, tile));
        }

        private void SyncronizeBoard()
        {
            var propertyCount = serverBoard.PropertyTiles.Count(Tile => !(Tile is null));

            var packet = new byte[Packet.Construct.SIZE_UpdateBoardProperty * propertyCount];

            var packetPtr = 0;
            for (var i = 0; i < serverBoard.PropertyTiles.Length; i++)
            {
                if (serverBoard.PropertyTiles[i] == null)
                    continue;

                var partialPacket = Packet.Construct.UpdateBoardProperty((byte) i, serverBoard.PropertyTiles[i]);
                partialPacket.CopyTo(packet, packetPtr);
                packetPtr += Packet.Construct.SIZE_UpdateBoardProperty;
            }

            lobby.BroadcastPacket(packet);
        }

        private void AddPlayerMoney(byte playerID, int income)
        {
            SetPlayerMoney(playerID, lobby.Players[playerID].Money + income);
        }

        private void TriggerAuction(int amountToAuction)
        {
            auctionAmount = amountToAuction;

            gameStates.AddFirst(GameState.WaitingForPlayerChoice);
            waitingForChoice = PlayerChoice.Auction;

            queuedPackets.Enqueue(Packet.Construct.AuctionProperty(currentPlayerId, amountToAuction));
            SyncronizeEffects();
        }

        /// <summary>
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="loss"></param>
        /// <returns>The amount subtracted</returns>
        private int SubtractPlayerMoney(byte playerID, int loss, bool triggerAuction)
        {
            SetPlayerMoney(playerID, lobby.Players[playerID].Money - loss);

            if (lobby.Players[playerID].Money < 0 && triggerAuction)
            {
                int mostExpensive = serverBoard.PropertyTiles.Max(p => p is null ? 0 : p.Value);
                int auctionAmount = Math.Abs(lobby.Players[playerID].Money);

                if (auctionAmount > mostExpensive)
                {
                    lobby.Players[playerID].isBankrupt = true;
                    Print($"{playerID} is now bankrupt");
                    SetPlayerMoney(playerID, 0);
                    queuedPackets.Enqueue(Packet.Construct.PlayerBankrupt(playerID));
                    
                    // TODO: Maybe clear list too?
                    gameStates.AddFirst(GameState.NextTurn);
                    return loss + lobby.Players[playerID].Money;
                }
                else
                {
                    TriggerAuction(auctionAmount);
                }
            }

            return loss;
        }

        private (byte, byte) RollDice()
        {
            // Maybe turn this into a function returning 2 bytes instead to make animations on client-side.

            return ((byte) random.Next(1, 7), (byte) random.Next(1, 7));
        }
    }
}