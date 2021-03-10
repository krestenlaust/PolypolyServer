using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ClientBot;
using Sharprompt;
using Sharprompt.Validations;

namespace Developer_Server
{
    class Program
    {
        private enum MainCommand
        {
            Player,
            Game,
            StopServer,
            StartServer,
            Close,
        }
        
        private enum PlayerSetting
        {
            Money,
            //JailCoupon,
            //DoubleRentCoupon,

            Back,
        }

        private enum GameSetting
        {
            ManualDiceRoll,
            AddBot,
            Back,
        }

        static void Main(string[] args)
        {
            DeveloperGameServer gameServer = null;
            CancellationTokenSource token = null;

            while (true)
            {
                Console.WriteLine();
                MainCommand command = Prompt.Select<MainCommand>("Select command");

                switch (command)
                {
                    case MainCommand.StopServer:
                        if (token is null)
                        {
                            Console.WriteLine("Server isn't running.");
                            continue;
                        }

                        token.Cancel();

                        Console.WriteLine("Stopping server...");
                        Thread.Sleep(2000);

                        Console.WriteLine("Server stopped.");
                        break;
                    case MainCommand.StartServer:
                        try
                        {
                            gameServer = new DeveloperGameServer();
                            token = new CancellationTokenSource();
                            gameServer.Start(token.Token);
                        }
                        catch (SocketException)
                        {
                            Console.WriteLine("Server start failed... Wait a short while, then try again.");
                        }
                        break;
                    case MainCommand.Player:
                        if (gameServer is null)
                        {
                            Console.WriteLine("Server isn't running.");
                            continue;
                        }

                        if (gameServer.GameLogic is null)
                        {
                            Console.WriteLine("Game hasn't started");
                            continue;
                        }

                        if (gameServer.GameLogic.Players.Count == 0)
                        {
                            Console.WriteLine("No players");
                            continue;
                        }

                        Dictionary<byte, string> playerIDName = gameServer.GameLogic.Players
                            .ToDictionary(p => p.Key, p => p.Value.ToString());

                        var selectedPlayers = from player in Prompt.MultiSelect("Select player(s)", playerIDName)
                                              select player.Key;

                        PlayerSetting setting = Prompt.Select<PlayerSetting>("Select player setting");

                        switch (setting)
                        {
                            case PlayerSetting.Money:
                                int newBalance = Prompt.Input<int>("Enter new");

                                foreach (byte playerID in selectedPlayers)
                                {
                                    gameServer.GameLogic.SetPlayerMoney(playerID, newBalance);
                                }

                                gameServer.GameLogic.SyncronizeEffects();
                                break;
                            default:
                                break;
                        }
                        break;
                    case MainCommand.Game:
                        if (gameServer is null)
                        {
                            Console.WriteLine("Server isn't running.");
                            continue;
                        }

                        switch (Prompt.Select<GameSetting>("Select game setting"))
                        {
                            case GameSetting.ManualDiceRoll:
                                if (gameServer.GameLogic is null)
                                {
                                    Console.WriteLine("Game hasn't started");
                                    continue;
                                }

                                byte die1 = Prompt.Input<byte>("Die 1");
                                byte die2 = Prompt.Input<byte>("Die 2");

                                gameServer.GameLogic.ThrowDiceNetwork((die1, die2));
                                break;
                            case GameSetting.AddBot:
                                gameServer.SpawnBot();

                                Console.WriteLine("Bot has connected");
                                break;
                            case GameSetting.Back:
                                break;
                            default:
                                break;
                        }
                        break;
                    case MainCommand.Close:
                        return;
                }
            }
        }
    }
}
