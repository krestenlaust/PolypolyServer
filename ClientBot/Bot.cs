// <copyright file="Bot.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

using Client;
using PolypolyGame;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static NetworkProtocol.GameBoard;

namespace ClientBot
{
    /// <summary>
    /// Acts as a networked client and reacts to events. Can output game statistics.
    /// </summary>
    public class Bot
    {
        private readonly NetworkClient networkClient;
        private readonly StreamWriter statisticsLog;
        private bool headerWritten;
        private int turnCount = 0;

        public Bot(string hostAddress, short port, bool logRoundsToFile = false)
        {
            networkClient = new NetworkClient();
            networkClient.ConnectClient(hostAddress, port);

            networkClient.onAuctionProperty += AuctionProperty;
            networkClient.onDiceRolled += (_) => networkClient.SignalAnimationDone();
            networkClient.onDrawChanceCard += (_) => networkClient.SignalAnimationDone();
            networkClient.onGameOver += GameOver;
            networkClient.onGameStarted += GameStarted;
            networkClient.onPrisonCardOffer += PrisonCardOffered;
            networkClient.onPropertyOffer += PropertyOffered;
            networkClient.onUpdatePlayerColor += PlayerColorUpdated;
            networkClient.onUpdatePlayerTurn += PlayerTurnUpdated;
            networkClient.onPlayerConnected += PlayerConnected;
            networkClient.onNewNickname += NicknamedUpdated;
            networkClient.onUpdateReadyPlayer += ReadyPlayerUpdated;

            if (logRoundsToFile)
            {
                statisticsLog = File.CreateText("roundlog.csv");
            }
        }

        private void ReadyPlayerUpdated(NetworkClient.UpdatePlayerReadyArgs e)
        {
            if (e.PlayerID.Value == networkClient.SelfID)
            {
                return;
            }

            // TODO: MaxPlayerCount not used
            if (networkClient.isHost && networkClient.Players.Count == 4)
            {
                networkClient.HostStartGame();
            }
        }

        private void NicknamedUpdated(NetworkClient.UpdateNicknameArgs e)
        {
            if (e.PlayerID.Value == networkClient.SelfID)
            {
                return;
            }
        }

        private void PlayerConnected(NetworkClient.PlayerConnectedArgs e)
        {
            if (e.PlayerID.Value != networkClient.SelfID)
            {
                return;
            }

            networkClient.UpdateUsername("CPU");
            networkClient.SendReadyState(true);
        }

        private string LogRound()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in networkClient.Players)
            {
                sb.Append(item.Value.Money);
                sb.Append(',');
            }

            return sb.ToString();
        }

        private void PlayerTurnUpdated(NetworkClient.UpdatePlayerTurnArgs e)
        {
            // TODO: MaxPlayerCount not used
            if (!(statisticsLog is null) && (turnCount++ % 4 == 0))
            {
                if (!headerWritten)
                {
                    foreach (var item in networkClient.Players)
                    {
                        statisticsLog.Write($"{item.Key},");
                    }

                    statisticsLog.WriteLine();
                    headerWritten = true;
                }

                statisticsLog.WriteLine(LogRound());
                statisticsLog.Flush();
            }

            if (e.PlayerID.Value == networkClient.SelfID)
            {
                networkClient.RollDice();
            }
        }

        private void PlayerColorUpdated(NetworkClient.UpdatePlayerColorArgs e)
        {
            if (e.PlayerID.Value == networkClient.SelfID)
            {
                return;
            }

            if (e.Color != networkClient[networkClient.SelfID].Color)
            {
                return;
            }

            // Change to unused color.
            var colorsInUse = (from player in networkClient.Players
                               select player.Value.Color).ToHashSet();

            foreach (var item in Enum.GetValues(typeof(TeamColor)).Cast<TeamColor>())
            {
                if (colorsInUse.Contains(item))
                {
                    continue;
                }

                networkClient.SendReadyState(false);
                networkClient.ChangeColorPreference(item);
                networkClient.SendReadyState(true);
                break;
            }
        }

        private void PropertyOffered(NetworkClient.PropertyOfferArgs e)
        {
            if (e.isAffordable && e.PlayerID.Value == networkClient.SelfID)
            {
                networkClient.AnswerPropertyOffer(true);
            }
        }

        private void PrisonCardOffered(NetworkClient.PrisonCardOfferArgs obj)
        {
            // Not implemented on server, thus not implemented here.
        }

        private void GameStarted()
        {
            networkClient.SignalAnimationDone();
        }

        private void GameOver(NetworkClient.GameOverArgs obj)
        {
        }

        private void AuctionProperty(NetworkClient.AuctionPropertyArgs e)
        {
            if (e.PlayerID.Value != networkClient.SelfID)
            {
                return;
            }

            Dictionary<byte, int> propertyByValue = new Dictionary<byte, int>();

            for (byte i = 0; i < networkClient.Board.PropertyTiles.Length; i++)
            {
                TileProperty property = networkClient.Board.PropertyTiles[i];

                if (property is null)
                {
                    continue;
                }

                if (property.Owner != networkClient.SelfID)
                {
                    continue;
                }

                if (property.Value < e.AuctionAmount)
                {
                    continue;
                }

                propertyByValue[i] = property.Value;
            }

            if (propertyByValue.Count == 0)
            {
                return;
            }

            byte sellIndex = (from property in propertyByValue
                              orderby property.Value
                              select property.Key).First();

            networkClient.AnswerPropertyAuction(sellIndex);
        }

        public void UpdateNetwork() => networkClient?.UpdateNetwork();
    }
}
