// <copyright file="Program.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

using Sharprompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace Developer_Server
{
    class Program
    {
        private const string ServerNotRunningMsg = "Server isn't running.";
        private const string ServerStoppingMsg = "Stopping server...";
        private const string ServerStoppedMsg = "Server stopped.";
        private const string GameNotStartedMsg = "Game hasn't started";

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
            ShowLogs,
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
                            Console.WriteLine(ServerNotRunningMsg);
                            continue;
                        }

                        token.Cancel();

                        Console.WriteLine(ServerStoppingMsg);
                        Thread.Sleep(2000);

                        Console.WriteLine(ServerStoppedMsg);
                        break;
                    case MainCommand.StartServer:
                        try
                        {
                            gameServer = new DeveloperGameServer(false);
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
                            Console.WriteLine(ServerNotRunningMsg);
                            continue;
                        }

                        if (gameServer.GameLogic is null)
                        {
                            Console.WriteLine(GameNotStartedMsg);
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
                            Console.WriteLine(ServerNotRunningMsg);
                            continue;
                        }

                        switch (Prompt.Select<GameSetting>("Select game setting"))
                        {
                            case GameSetting.ManualDiceRoll:
                                if (gameServer.GameLogic is null)
                                {
                                    Console.WriteLine(GameNotStartedMsg);
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
                            case GameSetting.ShowLogs:
                                gameServer.EnableLogging();
                                Console.WriteLine("Press anything to exit log view");
                                Console.ReadKey();

                                gameServer.DisableLogging();
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
