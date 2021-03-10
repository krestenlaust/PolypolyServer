using System;
using System.Collections.Generic;
using System.Linq;
using Client;
using PolypolyGameServer;
using static NetworkProtocol.GameBoard;

namespace ClientBot
{
    public class Bot
    {
        private readonly NetworkClient NetworkClient;

        public Bot(string hostAddress, short port)
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

        private void PlayerTurnUpdated(NetworkClient.UpdatePlayerTurnArgs e)
        {
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
