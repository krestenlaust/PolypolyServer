using System;
using System.Collections.Generic;
using System.Linq;
using static PolypolyGameServer.ServerBoard;

namespace PolypolyGameServer
{
    public class GameLogic
    {
        private enum PlayerChoice
        {
            PropertyOffer,
            JailOffer
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
        private readonly Random random = new Random();
        private TileProperty consideredProperty;
        private byte consideredPropertyPosition;
        private byte currentPlayerId;
        private bool extraTurn;
        private bool hasGameStateUpdated = true;

        /// <summary>
        ///     State after animation is done.
        /// </summary>
        private LinkedList<GameState> gameStates;

        private GameState previusGameState = GameState.WaitingForAnimation;
        private (byte, byte) RecentDiceThrowResult;
        private float timeTillSkipAnimation = 8;
        private int turnCount = -1;
        private PlayerChoice waitingForChoice;
        private readonly GameServer gameServer;
        private readonly Queue<byte[]> queuedPackets = new Queue<byte[]>();
        
        public GameLogic(GameServer gameServer)
        {
            this.gameServer = gameServer;
            serverBoard = ServerBoard.GenerateStandardBoard();
        }

        private void Print(string value)
        {
            gameServer.log.Print("[Game] " + value);
        }

        public void FixedUpdate()
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
            Print(currentState.ToString());
            gameStates.RemoveFirst();
            hasGameStateUpdated = currentState != previusGameState;
            previusGameState = currentState;

            switch (currentState)
            {
                case GameState.WaitingForPlayerChoice:
                    switch (waitingForChoice)
                    {
                        case PlayerChoice.JailOffer:
                            if (gameServer.Players[currentPlayerId].ReplyJailOffer is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            var jailCard = gameServer.Players[currentPlayerId].ReplyJailOffer.Value;

                            if (jailCard)
                            {
                                if (!gameServer.Players[currentPlayerId].hasJailCoupon)
                                    SubtractPlayerMoney(currentPlayerId, gameConfig.PrisonBailCost);
                                else
                                    gameServer.Players[currentPlayerId].hasJailCoupon = false;

                                UpdatePlayerJailturns(currentPlayerId, 0);

                                Print($"[{currentPlayerId}] Removed player from jail");
                            }

                            ThrowDiceNetwork(currentPlayerId);
                            gameServer.Players[currentPlayerId].ReplyJailOffer = null;

                            gameStates.AddLast(GameState.DiceThrown);
                            break;
                        case PlayerChoice.PropertyOffer:
                            if (gameServer.Players[currentPlayerId].ReplyPropertyOffer is null)
                            {
                                gameStates.AddFirst(GameState.WaitingForPlayerChoice);
                                break;
                            }

                            var buyProperty = gameServer.Players[currentPlayerId].ReplyPropertyOffer.Value;

                            if (buyProperty)
                            {
                                // upgrade building
                                consideredProperty.BuildingLevel = (TileProperty.BuildingState)((byte)consideredProperty.BuildingLevel + 1);
                                consideredProperty.Owner = currentPlayerId;
                                SubtractPlayerMoney(currentPlayerId, consideredProperty.BaseCost);
                                UpdateBoardProperty(consideredPropertyPosition, consideredProperty);
                            }
                            
                            SyncronizeEffects();

                            consideredProperty = null;
                            gameServer.Players[currentPlayerId].ReplyPropertyOffer = null;
                            break;
                    }

                    break;
                case GameState.WaitingForAnimation:
                    var animationDone = gameServer.Players.All(p => p.Value.isAnimationDone);

                    if (animationDone || timeTillSkipAnimation <= 0)
                    {
                        //gameStates.Enqueue(GameState.AnimationDone);

                        if (turnCount == -1)
                        {
                            // First turn ever. Clients have loaded the scene.
                            Print("Game has started1");

                            SyncronizeBoard();

                            foreach (var playerId in gameServer.Players.Keys)
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

                    if (RecentDiceThrowResult.Item1 != 0)
                    {
                        gameStates.AddLast(GameState.DiceThrown);
                        break;
                    }

                    // add waiting state if still waiting.
                    gameStates.AddFirst(GameState.WaitingForDiceThrow);
                    break;
                case GameState.DiceThrown:
                    gameStates.AddFirst(GameState.WaitingForAnimation);

                    Print($"{currentPlayerId}'s dice: {RecentDiceThrowResult.Item1}, {RecentDiceThrowResult.Item2}");

                    extraTurn = false;

                    var diceResult = (byte) (RecentDiceThrowResult.Item1 + RecentDiceThrowResult.Item2);
                    var diceDouble = RecentDiceThrowResult.Item1 == RecentDiceThrowResult.Item2;

                    // Check if passed go
                    if (gameServer.Players[currentPlayerId].Position + diceResult >= serverBoard.Size)
                        AddPlayerMoney(currentPlayerId, gameConfig.PassGoReward);

                    if (diceDouble)
                    {
                        gameServer.Players[currentPlayerId].ConsecutiveDoubleDice++;
                        Print(gameServer.Players[currentPlayerId].ConsecutiveDoubleDice + " consecutive double dice");

                        if (gameServer.Players[currentPlayerId].ConsecutiveDoubleDice >= 3)
                            SendToJail(currentPlayerId);
                        else
                            extraTurn = true;

                        if (gameServer.Players[currentPlayerId].JailTurns > 0)
                        {
                            UpdatePlayerJailturns(currentPlayerId, 0);
                            gameServer.Players[currentPlayerId].ConsecutiveDoubleDice = 0;
                        }
                    }
                    else
                    {
                        gameServer.Players[currentPlayerId].ConsecutiveDoubleDice = 0;
                    }

                    if (gameServer.Players[currentPlayerId].JailTurns > 0)
                    {
                        Print("Skipping because " + currentPlayerId + " is in jail");
                        UpdatePlayerJailturns(currentPlayerId,
                            (byte) (gameServer.Players[currentPlayerId].JailTurns - 1));
                        break;
                    }

                    var newPosition = (gameServer.Players[currentPlayerId].Position + diceResult) % serverBoard.Size;
                    UpdatePlayerPosition(currentPlayerId, (byte) newPosition, Packet.MoveType.Walk);
                    SyncronizeEffects();

                    // Handle player land location
                    HandlePlayerLandOnTile(currentPlayerId);

                    gameStates.AddLast(GameState.NextTurn);
                    break;
                case GameState.NextTurn:
                    // clear previus result
                    RecentDiceThrowResult.Item1 = 0;
                    RecentDiceThrowResult.Item2 = 0;

                    if (!extraTurn)
                    {
                        turnCount++;

                        // increment player turn
                        currentPlayerId = (byte)((currentPlayerId + 1) % gameServer.Players.Count);
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

            }
        }

        private void HandlePlayerLandOnTile(byte playerID)
        {
            byte position = (byte) gameServer.Players[playerID].Position;

            switch (serverBoard.TilesType[position])
            {
                case TileType.Jail:
                case TileType.Nothing:
                    break;
                case TileType.GotoJail:
                    SendToJail(currentPlayerId);
                    
                    // Animation is queued on client anyway.
                    SyncronizeEffects();

                    break;
                case TileType.Train:
                    AddPlayerMoney(currentPlayerId, gameConfig.PassGoReward);
                    break;
                case TileType.Tax:
                    SubtractPlayerMoney(currentPlayerId, gameConfig.TaxAmount);
                    break;
                case TileType.BigTax:
                    SubtractPlayerMoney(currentPlayerId, gameConfig.TaxAmount + gameConfig.PrisonBailCost);
                    break;
                case TileType.Upkeep:
                    SubtractPlayerMoney(currentPlayerId, gameConfig.TaxAmount);
                    break;
                case TileType.Property:
                    var property = serverBoard.TilesProperty[position];

                    var canAfford = gameServer.Players[playerID].Money >= property.BaseCost;

                    // unpurchased
                    if ((property.Owner == byte.MaxValue || property.Owner == currentPlayerId) && property.BuildingLevel != TileProperty.BuildingState.Level3)
                    {
                        MakePropertyOffer(playerID, property, canAfford);

                        if (!canAfford) break;

                        consideredProperty = property;
                        consideredPropertyPosition = position;

                        waitingForChoice = PlayerChoice.PropertyOffer;
                        gameStates.AddLast(GameState.WaitingForPlayerChoice);
                    }
                    else if (property.Owner != currentPlayerId)
                    {
                        var moneyPaid = SubtractPlayerMoney(currentPlayerId, property.Rent);
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
                            SubtractPlayerMoney(currentPlayerId, gameConfig.ChanceCardMoneyPenalty);

                            break;
                        case Packet.ChanceCard.DoubleRentCoupon:
                            gameServer.Players[currentPlayerId].hasDoubleRentCoupon = true;

                            break;
                        case Packet.ChanceCard.PrisonCoupon:
                            if (gameServer.Players[currentPlayerId].hasJailCoupon)
                            {
                                // give reward instead of extra coupon.
                                AddPlayerMoney(currentPlayerId, gameConfig.ChanceCardPrisonCouponWorth);
                            }
                            else
                            {
                                gameServer.Players[currentPlayerId].hasJailCoupon = true;
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
                            break;
                    }
                    break;
            }
        }

        private void ClearAnimationDone()
        {
            foreach (var VARIABLE in gameServer.Players.Keys) gameServer.Players[VARIABLE].isAnimationDone = false;
            Print("Reset animation done signals");
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

            RecentDiceThrowResult = RollDice();

            var packet = Packet.Construct.DicerollResult(playerID, RecentDiceThrowResult.Item1,
                RecentDiceThrowResult.Item2);

            gameServer.BroadcastPacket(packet);
        }

        private void SyncronizeEffects()
        {
            Print("Syncronizing");
            while (queuedPackets.Count > 0)
            {
                gameServer.BroadcastPacket(queuedPackets.Dequeue());
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

            gameServer.Players[playerID].Position = newPosition;

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
            gameServer.Players[playerID].Position = serverBoard.JailtileIndex;
            gameServer.Players[playerID].JailTurns = gameConfig.SentenceLength;

            queuedPackets.Enqueue(Packet.Construct.PlayerJail(playerID, gameConfig.SentenceLength));
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
            queuedPackets.Enqueue(Packet.Construct.PrisonCardOffer(gameServer.Players[playerID].hasJailCoupon));
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
            gameServer.Players[playerID].Money = newAmount;

            Print($"[{playerID}] new balance: {newAmount}");

            var previousAmount = gameServer.Players[playerID].Money;
            var isIncreased = newAmount > previousAmount;

            var packet = Packet.Construct.PlayerUpdateMoney(playerID, newAmount, isIncreased);

            queuedPackets.Enqueue(packet);
        }

        /// <summary>
        ///     Updates a gameServer.Players jailtime and notifies all clients.
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="jailturns"></param>
        private void UpdatePlayerJailturns(byte playerID, byte jailturns)
        {
            gameServer.Players[playerID].JailTurns = jailturns;
            
            queuedPackets.Enqueue(Packet.Construct.PlayerJail(playerID, jailturns));
        }

        private void UpdateBoardProperty(byte tileID, TileProperty tile)
        {
            queuedPackets.Enqueue(Packet.Construct.UpdateBoardProperty(tileID, tile));
        }

        private void SyncronizeBoard()
        {
            var propertyCount = serverBoard.TilesProperty.Count(Tile => !(Tile is null));

            var packet = new byte[Packet.Construct.SIZE_UpdateBoardProperty * propertyCount];

            var packetPtr = 0;
            for (var i = 0; i < serverBoard.TilesProperty.Length; i++)
            {
                if (serverBoard.TilesProperty[i] == null)
                    continue;

                var partialPacket = Packet.Construct.UpdateBoardProperty((byte) i, serverBoard.TilesProperty[i]);
                partialPacket.CopyTo(packet, packetPtr);
                packetPtr += Packet.Construct.SIZE_UpdateBoardProperty;
            }

            gameServer.BroadcastPacket(packet);
        }

        private void AddPlayerMoney(byte playerID, int income)
        {
            SetPlayerMoney(playerID, gameServer.Players[playerID].Money + income);
        }

        /// <summary>
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="loss"></param>
        /// <returns>The amount subtracted</returns>
        private int SubtractPlayerMoney(byte playerID, int loss)
        {
            var subtractAmount = Math.Min(gameServer.Players[playerID].Money, loss);
            SetPlayerMoney(playerID, gameServer.Players[playerID].Money - subtractAmount);

            return subtractAmount;
        }

        private (byte, byte) RollDice()
        {
            // Maybe turn this into a function returning 2 bytes instead to make animations on client-side.

            return ((byte) random.Next(1, 7), (byte) random.Next(1, 7));
        }
    }
}