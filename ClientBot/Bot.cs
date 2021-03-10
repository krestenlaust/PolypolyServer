using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client;
using PolypolyGame;
using static NetworkProtocol.GameBoard;

namespace ClientBot
{
    public class Bot
    {
        private readonly NetworkClient NetworkClient;
        private StreamWriter statisticsLog;
        private bool headerWritten;
        private int turnCount = 0;

        public Bot(string hostAddress, short port, bool logRoundsToFile=false)
        {
            NetworkClient = new NetworkClient();
            NetworkClient.ConnectClient(hostAddress, port);

            NetworkClient.onAuctionProperty += AuctionProperty;
            NetworkClient.onDiceRolled += (_) => NetworkClient.SignalAnimationDone();
            NetworkClient.onDrawChanceCard += (_) => NetworkClient.SignalAnimationDone();
            NetworkClient.onGameOver += GameOver;
            NetworkClient.onGameStarted += GameStarted;
            NetworkClient.onPrisonCardOffer += PrisonCardOffered;
            NetworkClient.onPropertyOffer += PropertyOffered;
            NetworkClient.onUpdatePlayerColor += PlayerColorUpdated;
            NetworkClient.onUpdatePlayerTurn += PlayerTurnUpdated;
            NetworkClient.onPlayerConnected += PlayerConnected;
            NetworkClient.onNewNickname += NicknamedUpdated;
            NetworkClient.onUpdateReadyPlayer += ReadyPlayerUpdated;

            if (logRoundsToFile)
            {
                statisticsLog = File.CreateText("roundlog.csv");
            }
        }

        private void ReadyPlayerUpdated(NetworkClient.UpdatePlayerReadyArgs e)
        {
            if (e.PlayerID.Value == NetworkClient.SelfID)
            {
                return;
            }

            if (NetworkClient.isHost && NetworkClient.Players.Count == 4)
            {
                NetworkClient.HostStartGame();
            }
        }

        private void NicknamedUpdated(NetworkClient.UpdateNicknameArgs e)
        {
            if (e.PlayerID.Value == NetworkClient.SelfID)
            {
                return;
            }
        }

        private void PlayerConnected(NetworkClient.PlayerConnectedArgs e)
        {
            if (e.PlayerID.Value != NetworkClient.SelfID)
            {
                return;
            }

            NetworkClient.UpdateUsername("CPU");
            NetworkClient.SendReadyState(true);
        }

        private string LogRound()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in NetworkClient.Players)
            {
                sb.Append(item.Value.Money);
                sb.Append(',');
            }

            return sb.ToString();
        }

        private void PlayerTurnUpdated(NetworkClient.UpdatePlayerTurnArgs e)
        {
            if (!(statisticsLog is null) && (turnCount++ % 4 == 0))
            {
                if (!headerWritten)
                {
                    foreach (var item in NetworkClient.Players)
                    {
                        statisticsLog.Write($"{item.Key},");
                    }

                    statisticsLog.WriteLine();
                    headerWritten = true;
                }

                statisticsLog.WriteLine(LogRound());
                statisticsLog.Flush();
            }

            if (e.PlayerID.Value == NetworkClient.SelfID)
            {
                NetworkClient.RollDice();
            }
        }

        private void PlayerColorUpdated(NetworkClient.UpdatePlayerColorArgs e)
        {
            if (e.PlayerID.Value == NetworkClient.SelfID)
            {
                return;
            }

            if (e.Color != NetworkClient[NetworkClient.SelfID].Color)
            {
                return;
            }

            // Change to unused color.
            var colorsInUse = (from player in NetworkClient.Players
                              select player.Value.Color).ToHashSet();

            foreach (var item in Enum.GetValues(typeof(TeamColor)).Cast<TeamColor>())
            {
                if (colorsInUse.Contains(item))
                {
                    continue;
                }

                NetworkClient.SendReadyState(false);
                NetworkClient.ChangeColorPreference(item);
                NetworkClient.SendReadyState(true);
                break;
            }
        }

        private void PropertyOffered(NetworkClient.PropertyOfferArgs e)
        {
            if (e.isAffordable && e.PlayerID.Value == NetworkClient.SelfID)
            {
                NetworkClient.AnswerPropertyOffer(true);
            }
        }

        private void PrisonCardOffered(NetworkClient.PrisonCardOfferArgs obj)
        {
            // Not implemented on server, thus not implemented here.
        }

        private void GameStarted()
        {
            NetworkClient.SignalAnimationDone();
        }

        private void GameOver(NetworkClient.GameOverArgs obj)
        {
        }

        private void AuctionProperty(NetworkClient.AuctionPropertyArgs e)
        {
            if (e.PlayerID.Value != NetworkClient.SelfID)
            {
                return;
            }

            Dictionary<byte, int> propertyByValue = new Dictionary<byte, int>();

            for (byte i = 0; i < NetworkClient.Board.PropertyTiles.Length; i++)
            {
                TileProperty property = NetworkClient.Board.PropertyTiles[i];

                if (property is null)
                {
                    continue;
                }

                if (property.Owner != NetworkClient.SelfID)
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

            NetworkClient.AnswerPropertyAuction(sellIndex);
        }

        public void UpdateNetwork() => NetworkClient?.UpdateNetwork();
    }
}
