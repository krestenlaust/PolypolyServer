﻿// <copyright file="Packet.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

using System;
using System.Net.Sockets;
using System.Text;
using PolypolyGame;

namespace NetworkProtocol
{
    /// <summary>
    /// Helper class for constructing packets.
    /// </summary>
    public static class Packet
    {
        /// <summary>
        /// Types of chancecards.
        /// </summary>
        public enum ChanceCard : byte
        {
            /// <summary>
            /// Adds a fixed amount of money to the players balance.
            /// </summary>
            MoneyAdd,

            /// <summary>
            /// The player is moved to the train-tile and activates the train-event.
            /// </summary>
            TrainCoupon,
            
            /// <summary>
            /// The next time the player is sent to prison, they stay where they are.
            /// If the player already owns a prison coupon, they earn the estimated value.
            /// </summary>
            PrisonCoupon,

            /// <summary>
            /// The player is moved to the first tile.
            /// </summary>
            Go,

            /// <summary>
            /// The player is sentenced and moved to jail.
            /// </summary>
            GotoPrison,

            /// <summary>
            /// Deducts a fixed amount of money from the players balance.
            /// </summary>
            MoneyDeduct,

            /// <summary>
            /// The next time the player lands on a property, they pay double.
            /// </summary>
            DoubleRentCoupon,

            /// <summary>
            /// The auction-event is triggered for the player, if the player has any properties.
            /// </summary>
            ForceAuction,

            /// <summary>
            /// The player is moved four tiles back, and the corresponding tile is 'activated'.
            /// </summary>
            MoveFourTilesBack,

            /// <summary>
            /// Nothing happens.
            /// </summary>
            Blank,
        }

        /// <summary>
        /// Describes why a client disconnected.
        /// </summary>
        public enum DisconnectReason : byte
        {
            /// <summary>
            /// The client lost connection.
            /// </summary>
            LostConnection = 1,

            /// <summary>
            /// The client left the game/lobby.
            /// </summary>
            Left = 2,

            /// <summary>
            /// The client forced to leave the game by the host.
            /// </summary>
            Kicked = 3,
        }

        /// <summary>
        /// Describes the way a player avatar should move.
        /// </summary>
        public enum MoveType : byte
        {
            /// <summary>
            /// Move player avatar a step at a time.
            /// </summary>
            Walk,

            /// <summary>
            /// Move player avatar — without steps — in a straight line.
            /// </summary>
            DirectMove,
        }

        /// <summary>
        /// The different ways a game can end.
        /// </summary>
        public enum GameOverType : byte
        {
            /// <summary>
            /// A player achieved an monopoly.
            /// </summary>
            Monopoly,

            /// <summary>
            /// The time ran out.
            /// </summary>
            Time,

            /// <summary>
            /// The host ended the game.
            /// </summary>
            HostEnded,
        }

        /// <summary>
        /// Client to host.
        /// </summary>
        public enum ClientPacketType : byte
        {
            DicerollRequest = 1, // ✓
            PlayerNickname = 3, // ✓, tells server clients nickname.
            ReadyPacket = 5, // ✓
            UnreadyPacket = 7, // ✓, Updates ready status.
            StartGamePacket = 9, // ✓
            LeaveGamePacket = 11, // ✓
            AnimationDone = 13, // ✓
            ChangeColor = 15, // ✓
            KickPlayer = 17, // ✓
            PrisonReply = 19, // ✓, om man gerne vil købe/bruge et kort eller rulle terninger, når man er i fængsel
            PropertyReply = 21, // ✓, om man vil købe eller ej
            AuctionReply = 23, // ✓
        }

        /// <summary>
        /// Host to client.
        /// </summary>
        public enum ServerPacketType : byte
        {
            DicerollResult = 2, // ✓
            UpdatePlayerTurn = 4, // ✓
            UpdatePlayerNickname = 6, // ✓
            AssignPlayerID = 8, // ✓
            UpdateHost = 10, // ✓
            PlayerDisconnected = 12, // ✓
            PlayerConnected = 14, // ✓
            UpdatePlayerReady = 16, // ✓
            UpdatePlayerMoney = 18, // ✓
            PlayerJail = 20, // ✓
            UpdatePlayerPosition = 22, // ✓
            GameStarted = 24, // ✓
            UpdatePlayerColor = 26, // ✓
            UpdateBoardProperty = 28, // ✓
            DrawChanceCard = 30, // ✓
            PropertyOffer = 32, // ✓, til når en spiller lander på en grund som ikke er købt eller de kan udvidde
            PrisonCardOffer =
                34, // ✓, til når man er i fængsel og man kan vælge mellem at 1) købe et kort, 2) bruge et man har i forvejen, 3) rulle.
            AuctionProperty = 36, // ✓
            PlayerBankrupt = 38, // ✓
            UpdateGroupDoubleRent = 40, // ✓
            GameOver = 42, // ✓
            UpdatePlayerDoubleRent = 44, // ✓
            UpdatePlayerJailCoupon = 46, // ✓
        }

        /// <summary>
        /// Helper class for constructing packets.
        /// </summary>
        public static class Construct
        {
            public const int SIZE_PlayerJail = sizeof(byte) + sizeof(byte) + sizeof(byte);

            public const int SIZE_UpdateBoardProperty =
                sizeof(byte) + sizeof(byte) + sizeof(byte) + sizeof(byte) + sizeof(int) + sizeof(byte);

            public const int SIZE_UpdatePlayerPosition = sizeof(byte) + sizeof(byte) + sizeof(byte) + sizeof(byte);
            public const int SIZE_PlayerConnected = sizeof(byte) + sizeof(byte);

            public static byte[] UpdatePlayerDoubleRent(byte playerID, bool status)
            {
                return new byte[]
                {
                    (byte)ServerPacketType.UpdatePlayerDoubleRent,
                    playerID,
                    (byte)(status ? 1 : 0)
                };
            }

            public static byte[] UpdatePlayerJailCoupon(byte playerID, bool status)
            {
                return new byte[]
                {
                    (byte)ServerPacketType.UpdatePlayerJailCoupon,
                    playerID,
                    (byte)(status ? 1 : 0)
                };
            }

            public static byte[] UpdateGroupDoubleRent(byte groupID, bool status)
            {
                return new[]
                {
                    (byte)ServerPacketType.UpdateGroupDoubleRent,
                    groupID,
                    (byte)(status ? 1 : 0)
                };
            }

            /// <summary>
            /// Used by server to signal when a player has gone bankrupt and is out of the game.
            /// </summary>
            /// <param name="playerID"></param>
            public static byte[] PlayerBankrupt(byte playerID)
            {
                return new[]
                {
                    (byte)ServerPacketType.PlayerBankrupt,
                    playerID
                };
            }

            /// <summary>
            /// Used by server to signal game has ended.
            /// </summary>
            /// <param name="gameOverType"></param>
            /// <param name="winner"></param>
            /// <returns></returns>
            public static byte[] GameOver(GameOverType gameOverType, byte winner)
            {
                return new[]
                {
                    (byte)ServerPacketType.GameOver,
                    (byte)gameOverType,
                    winner
                };
            }

            /// <summary>
            ///  Used by server.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="buildingState"></param>
            /// <param name="baseRent"></param>
            /// <param name="cost"></param>
            /// <param name="isAffordable"></param>
            /// <returns></returns>
            public static byte[] PropertyOffer(
                byte playerID, GameBoard.TileProperty.BuildingState buildingState, int baseRent, int cost, bool isAffordable)
            {
                var packet = new byte[sizeof(ServerPacketType) + sizeof(byte) + sizeof(GameBoard.TileProperty.BuildingState) +
                                      sizeof(int) + sizeof(int) + sizeof(bool)];

                packet[0] = (byte)ServerPacketType.PropertyOffer;
                packet[1] = playerID;
                packet[2] = (byte)buildingState;

                var baseRentBytes = BitConverter.GetBytes(baseRent);
                baseRentBytes.CopyTo(packet, 3);

                var costBytes = BitConverter.GetBytes(cost);
                costBytes.CopyTo(packet, 7);

                packet[11] = BitConverter.GetBytes(isAffordable)[0];

                return packet;
            }

            /// <summary>
            /// Used by server. Only sent to single player
            /// </summary>
            /// <param name="hasCard"></param>
            /// <returns></returns>
            public static byte[] PrisonCardOffer(bool hasCard)
            {
                var packet = new byte[sizeof(byte) + sizeof(bool)];

                packet[0] = (byte)ServerPacketType.PrisonCardOffer;
                packet[1] = BitConverter.GetBytes(hasCard)[0];

                return packet;
            }

            /// <summary>
            /// Used by client. If useCard, then buy if player does not own card, otherwise signal diceroll.
            /// </summary>
            /// <param name="useCard"></param>
            /// <returns></returns>
            public static byte[] PrisonReply(bool useCard)
            {
                var packet = new byte[sizeof(ServerPacketType) + sizeof(bool)];

                packet[0] = (byte)ClientPacketType.PrisonReply;
                packet[1] = BitConverter.GetBytes(useCard)[0];

                return packet;
            }

            /// <summary>
            /// Used by client for saying telling whether to purchase or not.
            /// </summary>
            /// <param name="purchase"></param>
            /// <returns></returns>
            public static byte[] PropertyReply(bool purchase)
            {
                byte[] packet = new byte[sizeof(ServerPacketType) + sizeof(bool)];

                packet[0] = (byte)ClientPacketType.PropertyReply;
                packet[1] = BitConverter.GetBytes(purchase)[0];

                return packet;
            }

            /// <summary>
            /// Used by client.
            /// </summary>
            public static byte[] AnimationDone()
            {
                return new[] { (byte)ClientPacketType.AnimationDone };
            }

            /// <summary>
            /// Used by client.
            /// </summary>
            /// <returns></returns>
            public static byte[] StartGame()
            {
                return new[] { (byte)ClientPacketType.StartGamePacket };
            }

            /// <summary>
            /// Used by server.
            /// </summary>
            public static byte[] GameStarted()
            {
                return new[] { (byte)ServerPacketType.GameStarted };
            }

            /// <summary>
            /// Used by server to tell players that a player has to auction one of their properties.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="auctionValue"></param>
            /// <returns></returns>
            public static byte[] AuctionProperty(byte playerID, int auctionValue)
            {
                byte[] packet = new byte[sizeof(ServerPacketType) + 1 + sizeof(int)];

                packet[0] = (byte)ServerPacketType.AuctionProperty;
                packet[1] = playerID;
                BitConverter.GetBytes(auctionValue).CopyTo(packet, 2);

                return packet;
            }

            /// <summary>
            /// Used by client to tell what property to auction.
            /// </summary>
            /// <param name="propertyIndex"></param>
            /// <returns></returns>
            public static byte[] AuctionReply(byte propertyIndex)
            {
                return new[] {
                    (byte)ClientPacketType.AuctionReply,
                    propertyIndex
                };
            }

            /// <summary>
            /// Used by client.
            /// </summary>
            /// <param name="color"></param>
            /// <returns></returns>
            public static byte[] ChangeColor(TeamColor color)
            {
                return new[]
                {
                    (byte)ClientPacketType.ChangeColor,
                    (byte)color
                };
            }

            /// <summary>
            /// Used by client to tell server who to kick.
            /// </summary>
            /// <param name="playerID"></param>
            /// <returns></returns>
            public static byte[] KickPlayer(byte playerID)
            {
                return new[]
                {
                    (byte)ClientPacketType.KickPlayer,
                    playerID
                };
            }

            /// <summary>
            /// Used by server to tell clients to animate chancecard.
            /// </summary>
            /// <param name="chanceCard"></param>
            /// <returns></returns>
            public static byte[] DrawChanceCard(ChanceCard chanceCard)
            {
                return new[]
                {
                    (byte)ServerPacketType.DrawChanceCard,
                    (byte)chanceCard
                };
            }

            /// <summary>
            ///     Used by server.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="color"></param>
            /// <returns></returns>
            public static byte[] UpdatePlayerColor(byte playerID, TeamColor color)
            {
                var packet = new byte[sizeof(ServerPacketType) + 1 + sizeof(TeamColor)];

                packet[0] = (byte)ServerPacketType.UpdatePlayerColor;
                packet[1] = playerID;
                packet[2] = (byte)color;

                return packet;
            }

            /// <summary>
            ///     Used by server. Broadcast to clients when jailed player has their turn.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="jailTurnsLeft"></param>
            /// <returns></returns>
            public static byte[] PlayerJail(byte playerID, byte jailTurnsLeft)
            {
                var packet = new byte[sizeof(ServerPacketType) + 2];

                packet[0] = (byte)ServerPacketType.PlayerJail;
                packet[1] = playerID;
                packet[2] = jailTurnsLeft;

                return packet;
            }

            /// <summary>
            /// Used by client. Sends a request to roll the dice (to the server).
            /// </summary>
            /// <returns></returns>
            public static byte[] DicerollRequest()
            {
                return new byte[] { (byte)ClientPacketType.DicerollRequest };
            }

            /// <summary>
            /// Used by server. Updates the client-board state. Used
            /// </summary>
            /// <param name="tileID"></param>
            /// <param name="tile"></param>
            /// <returns></returns>
            public static byte[] UpdateBoardProperty(byte tileID, GameBoard.TileProperty tile)
            {
                var packet = new byte[
                    sizeof(ServerPacketType) +
                    sizeof(byte) +
                    sizeof(byte) +
                    sizeof(GameBoard.TileProperty.BuildingState) +
                    sizeof(byte) +
                    sizeof(int)];

                packet[0] = (byte)ServerPacketType.UpdateBoardProperty;
                packet[1] = tileID;
                packet[2] = tile.Owner;
                packet[3] = (byte)tile.BuildingLevel;
                packet[4] = tile.GroupID;

                var baseRent = BitConverter.GetBytes(tile.BaseCost);
                baseRent.CopyTo(packet, 5);

                return packet;
            }

            /// <summary>
            /// Used by server. Broadcast to clients to inform who is next.
            /// </summary>
            /// <param name="playerID"></param>
            /// <returns></returns>
            public static byte[] UpdatePlayerTurn(byte playerID)
            {
                var packet = new byte[sizeof(ServerPacketType) + sizeof(byte)];

                packet[0] = (byte)ServerPacketType.UpdatePlayerTurn;
                packet[1] = playerID;

                return packet;
            }

            /// <summary>
            /// Used by client.
            /// </summary>
            /// <returns></returns>
            public static byte[] ReadyPacket()
            {
                return new[] { (byte)ClientPacketType.ReadyPacket };
            }

            /// <summary>
            /// Used by client.
            /// </summary>
            /// <returns></returns>
            public static byte[] UnreadyPacket()
            {
                return new[] { (byte)ClientPacketType.UnreadyPacket };
            }


            /// <summary>
            /// Used by server. Broadcast to all clients when recieving a ready/unready packet from a client.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="readyStatus"></param>
            /// <returns></returns>
            public static byte[] UpdatePlayerReady(byte playerID, bool readyStatus)
            {
                var packet = new byte[sizeof(ServerPacketType) + 1 + sizeof(bool)];

                packet[0] = (byte)ServerPacketType.UpdatePlayerReady;
                packet[1] = playerID;
                packet[2] = BitConverter.GetBytes(readyStatus)[0];

                return packet;
            }


            /// <summary>
            /// Used by client.
            /// </summary>
            /// <returns></returns>
            public static byte[] LeaveGamePacket()
            {
                return new[] { (byte)ClientPacketType.LeaveGamePacket };
            }

            /// <summary>
            /// Used by client. Sent to server to inform nickname update.
            /// </summary>
            /// <param name="nickname"></param>
            /// <returns></returns>
            public static byte[] PlayerNickname(string nickname)
            {
                var nicknameEncoded = Encoding.UTF8.GetBytes(nickname);
                var lengthBytes = BitConverter.GetBytes(nicknameEncoded.Length);

                var packet = new byte[
                    sizeof(byte) +
                    sizeof(int) +
                    nicknameEncoded.Length];

                packet[0] = (byte)ClientPacketType.PlayerNickname;
                lengthBytes.CopyTo(packet, 1);
                nicknameEncoded.CopyTo(packet, 5);

                return packet;
            }

            /// <summary>
            /// Used by server. Broadcast to clients to inform diceroll result.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="die1Result"></param>
            /// <param name="die2Result"></param>
            /// <returns></returns>
            public static byte[] DicerollResult(byte playerID, byte die1Result, byte die2Result)
            {
                var packet = new byte[sizeof(ServerPacketType) + 1 + 2];

                packet[0] = (byte)ServerPacketType.DicerollResult;
                packet[1] = playerID;
                packet[2] = die1Result;
                packet[3] = die2Result;

                return packet;
            }

            /// <summary>
            /// Used by server. Broadcast to clients to inform nickname update.
            /// </summary>
            /// <param name="nickname"></param>
            /// <param name="playerID"></param>
            /// <returns></returns>
            public static byte[] UpdatePlayerNickname(string nickname, byte playerID)
            {
                var nicknameEncoded = Encoding.UTF8.GetBytes(nickname);
                var lengthBytes = BitConverter.GetBytes(nicknameEncoded.Length);

                var packet = new byte[sizeof(ServerPacketType) + sizeof(byte) + sizeof(int) + nicknameEncoded.Length];

                packet[0] = (byte)ServerPacketType.UpdatePlayerNickname;
                packet[1] = playerID;

                lengthBytes.CopyTo(packet, 2);
                nicknameEncoded.CopyTo(packet, 6);

                return packet;
            }

            /// <summary>
            /// Used by server. Sent from server to connecting client to inform given player ID.
            /// </summary>
            /// <param name="playerID"></param>
            /// <returns></returns>
            public static byte[] AssignPlayerID(byte playerID)
            {
                var packet = new byte[sizeof(ServerPacketType) + 1];
                packet[0] = (byte)ServerPacketType.AssignPlayerID;
                packet[1] = playerID;

                return packet;
            }

            /// <summary>
            /// Used by server. Broadcast to clients to inform host change or simply identify the host.
            /// </summary>
            /// <param name="playerID"></param>
            /// <returns></returns>
            public static byte[] UpdateHost(byte playerID)
            {
                var packet = new byte[sizeof(ServerPacketType) + 1];

                packet[0] = (byte)ServerPacketType.UpdateHost;
                packet[1] = playerID;

                return packet;
            }

            /// <summary>
            /// Used by server. Broadcast to clients to inform position chance.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="newPosition"></param>
            /// <param name="moveType"></param>
            /// <returns></returns>
            public static byte[] UpdatePlayerPosition(byte playerID, byte newPosition, MoveType moveType)
            {
                var packet = new byte[sizeof(ServerPacketType) + 2 + sizeof(MoveType)];

                packet[0] = (byte)ServerPacketType.UpdatePlayerPosition;
                packet[1] = playerID;
                packet[2] = newPosition;
                packet[3] = (byte)moveType;

                return packet;
            }

            /// <summary>
            /// Used by server to tell clients that a player has connected.
            /// </summary>
            /// <param name="playerID"></param>
            /// <returns></returns>
            public static byte[] PlayerConnected(byte playerID)
            {
                var packet = new byte[sizeof(ServerPacketType) + 1];
                packet[0] = (byte)ServerPacketType.PlayerConnected;
                packet[1] = playerID;

                return packet;
            }

            /// <summary>
            /// Used by server.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="permanent"></param>
            /// <param name="disconnectReason"></param>
            /// <returns></returns>
            public static byte[] PlayerDisconnected(byte playerID, bool permanent, DisconnectReason disconnectReason)
            {
                var packet = new byte[sizeof(ServerPacketType) + 1 + sizeof(bool) + sizeof(DisconnectReason)];
                packet[0] = (byte)ServerPacketType.PlayerDisconnected;
                packet[1] = playerID;
                packet[2] = BitConverter.GetBytes(permanent)[0];
                packet[3] = (byte)disconnectReason;

                return packet;
            }

            /// <summary>
            /// Used by server to tell clients that a players balance has changed.
            /// </summary>
            /// <param name="playerID"></param>
            /// <param name="newAmount"></param>
            /// <param name="isIncreased"></param>
            /// <returns></returns>
            public static byte[] PlayerUpdateMoney(byte playerID, int newAmount, bool isIncreased)
            {
                byte[] packet = new byte[sizeof(ServerPacketType) + 1 + sizeof(int) + sizeof(bool)];

                packet[0] = (byte)ServerPacketType.UpdatePlayerMoney;
                packet[1] = playerID;
                var newAmountBytes = BitConverter.GetBytes(newAmount);
                newAmountBytes.CopyTo(packet, 2);
                packet[6] = BitConverter.GetBytes(isIncreased)[0];

                return packet;
            }
        }

        /// <summary>
        /// Helper class for deconstructing packets by networkstream.
        /// </summary>
        public static class Deconstruct
        {
            #region For server

            /// <summary>
            /// Used by client.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="gameOverType"></param>
            /// <param name="winner"></param>
            public static void GameOver(NetworkStream stream, out GameOverType gameOverType, out byte winner)
            {
                gameOverType = (GameOverType)stream.ReadByte();
                winner = (byte)stream.ReadByte();
            }

            public static void UpdatePlayerDoubleRent(NetworkStream stream, out byte playerID, out bool status)
            {
                playerID = (byte)stream.ReadByte();
                status = stream.ReadByte() == 1;
            }

            public static void UpdatePlayerJailCoupon(NetworkStream stream, out byte playerID, out bool status)
            {
                playerID = (byte)stream.ReadByte();
                status = stream.ReadByte() == 1;
            }

            public static void UpdateGroupDoubleRent(NetworkStream stream, out byte groupID, out bool status)
            {
                groupID = (byte)stream.ReadByte();
                status = stream.ReadByte() == 1;
            }

            public static void PropertyReply(NetworkStream stream, out bool purchase)
            {
                var purchaseBytes = new byte[sizeof(bool)];
                stream.Read(purchaseBytes, 0, purchaseBytes.Length);

                purchase = BitConverter.ToBoolean(purchaseBytes, 0);
            }

            public static void PrisonReply(NetworkStream stream, out bool useCard)
            {
                var useCardBytes = new byte[sizeof(bool)];
                stream.Read(useCardBytes, 0, useCardBytes.Length);

                useCard = BitConverter.ToBoolean(useCardBytes, 0);
            }

            public static void AuctionReply(NetworkStream stream, out byte propertyIndex)
            {
                propertyIndex = (byte)stream.ReadByte();
            }

            public static void AuctionProperty(NetworkStream stream, out byte playerID, out int auctionValue)
            {
                playerID = (byte)stream.ReadByte();

                byte[] auctionValueBytes = new byte[sizeof(int)];
                stream.Read(auctionValueBytes, 0, sizeof(int));

                auctionValue = BitConverter.ToInt32(auctionValueBytes, 0);
            }

            /// <summary>
            ///     Used by server
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="nickname"></param>
            public static void PlayerNickname(NetworkStream stream, out string nickname)
            {
                var nicknameLengthBytes = new byte[sizeof(int)];
                stream.Read(nicknameLengthBytes, 0, sizeof(int));

                var nicknameLength = BitConverter.ToInt32(nicknameLengthBytes, 0);

                var nicknameBytes = new byte[nicknameLength];
                stream.Read(nicknameBytes, 0, nicknameLength);

                nickname = Encoding.UTF8.GetString(nicknameBytes);
            }

            public static void ChangeColor(NetworkStream stream, out TeamColor color)
            {
                color = (TeamColor)stream.ReadByte();
            }

            #endregion

            #region For client

            public static void PlayerBankrupt(NetworkStream stream, out byte playerID)
            {
                playerID = (byte)stream.ReadByte();
            }

            public static void PropertyOffer(
                NetworkStream stream, out byte playerID, out GameBoard.TileProperty.BuildingState buildingState, out int baseRent, out int cost, out bool isAffordable)
            {
                playerID = (byte)stream.ReadByte();
                buildingState = (GameBoard.TileProperty.BuildingState)stream.ReadByte();

                var baseRentBytes = new byte[sizeof(int)];
                stream.Read(baseRentBytes, 0, baseRentBytes.Length);
                baseRent = BitConverter.ToInt32(baseRentBytes, 0);

                var costBytes = new byte[sizeof(int)];
                stream.Read(costBytes, 0, costBytes.Length);
                cost = BitConverter.ToInt32(costBytes, 0);

                isAffordable = stream.ReadByte() == 1;
            }

            public static void PrisonCardOffer(NetworkStream stream, out bool hasCard)
            {
                hasCard = stream.ReadByte() == 1;
            }

            public static void UpdatePlayerTurn(NetworkStream stream, out byte playerID)
            {
                playerID = (byte)stream.ReadByte();
            }

            public static void UpdatePlayerColor(NetworkStream stream, out byte playerID, out TeamColor color)
            {
                playerID = (byte)stream.ReadByte();
                color = (TeamColor)stream.ReadByte();
            }

            /// <summary>
            ///     Used by client
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="nickname"></param>
            /// <param name="playerID"></param>
            public static void UpdateNickname(NetworkStream stream, out string nickname, out byte playerID)
            {
                playerID = (byte)stream.ReadByte();

                var lengthBytes = new byte[sizeof(int)];
                stream.Read(lengthBytes, 0, sizeof(int));

                var length = BitConverter.ToInt32(lengthBytes, 0);
                var nicknameEncoded = new byte[length];

                stream.Read(nicknameEncoded, 0, length);

                nickname = Encoding.UTF8.GetString(nicknameEncoded);
            }

            /// <summary>
            ///     Used by client. Updates player jail turns left.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="playerID"></param>
            /// <param name="jailTurnsLeft"></param>
            public static void PlayerJail(NetworkStream stream, out byte playerID, out byte jailTurnsLeft)
            {
                playerID = (byte)stream.ReadByte();
                jailTurnsLeft = (byte)stream.ReadByte();
            }

            public static void UpdatePlayerPosition(NetworkStream stream, out byte playerID, out byte newPosition, out MoveType moveType)
            {
                playerID = (byte)stream.ReadByte();
                newPosition = (byte)stream.ReadByte();
                moveType = (MoveType)stream.ReadByte();
            }

            /// <summary>
            /// Used by client.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="playerID"></param>
            /// <param name="dieResults"></param>
            public static void DicerollResult(NetworkStream stream, out byte playerID, out (byte Die1, byte Die2) dieResults)
            {
                playerID = (byte)stream.ReadByte();
                dieResults = ((byte)stream.ReadByte(), (byte)stream.ReadByte());
            }

            /// <summary>
            /// Used by client.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="playerID"></param>
            public static void UpdateHost(NetworkStream stream, out byte playerID)
            {
                playerID = (byte)stream.ReadByte();
            }

            /// <summary>
            ///     Used by client. Broadcast from server to clients, when a player leaves/disconnects.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="playerID"></param>
            /// <param name="permanent"></param>
            /// <param name="disconnectReason"></param>
            public static void PlayerDisconnected(
                NetworkStream stream, out byte playerID, out bool permanent, out DisconnectReason disconnectReason)
            {
                playerID = (byte)stream.ReadByte();
                permanent = stream.ReadByte() == 1;
                disconnectReason = (DisconnectReason)stream.ReadByte();
            }

            /// <summary>
            ///     Used by client. Broadcast from server to clients, when a player joins.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="playerID"></param>
            public static void PlayerConnected(NetworkStream stream, out byte playerID)
            {
                playerID = (byte)stream.ReadByte();
            }

            /// <summary>
            ///     Used by client.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="playerID"></param>
            /// <param name="newAmount"></param>
            /// <param name="isIncreased"></param>
            public static void UpdatePlayerMoney(NetworkStream stream, out byte playerID, out int newAmount, out bool isIncreased)
            {
                playerID = (byte)stream.ReadByte();

                var newAmountBytes = new byte[sizeof(int)];
                stream.Read(newAmountBytes, 0, 4);
                newAmount = BitConverter.ToInt32(newAmountBytes, 0);

                isIncreased = stream.ReadByte() == 1;
            }

            /// <summary>
            ///     Used by client.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="playerID"></param>
            /// <param name="readyStatus"></param>
            public static void UpdatePlayerReady(NetworkStream stream, out byte playerID, out bool readyStatus)
            {
                playerID = (byte)stream.ReadByte();
                readyStatus = stream.ReadByte() == 1;
            }

            /// <summary>
            ///     Used by client.
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="tileID"></param>
            /// <param name="tile"></param>
            public static void UpdateBoardProperty(NetworkStream stream, out byte tileID, out GameBoard.TileProperty tile)
            {
                tileID = (byte)stream.ReadByte();
                byte owner = (byte)stream.ReadByte();
                var buildingState = (GameBoard.TileProperty.BuildingState)stream.ReadByte();

                byte groupID = (byte)stream.ReadByte();
                var baseRentBytes = new byte[sizeof(int)];
                stream.Read(baseRentBytes, 0, sizeof(int));

                var baseRent = BitConverter.ToInt32(baseRentBytes, 0);

                tile = new GameBoard.TileProperty(baseRent, groupID, owner, buildingState);
            }

            public static void DrawChanceCard(NetworkStream stream, out ChanceCard chanceCard)
            {
                chanceCard = (ChanceCard)stream.ReadByte();
            }

            public static void KickPlayer(NetworkStream stream, out byte playerID)
            {
                playerID = (byte)stream.ReadByte();
            }

            #endregion
        }
    }
}