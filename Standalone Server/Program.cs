using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PolypolyGameServer;

namespace Standalone_Server
{
    internal static class Program
    {
        private static Stack<CancellationTokenSource> tokens = new Stack<CancellationTokenSource>();

        private static void Main(string[] args)
        {
            var address = IPAddress.Any;
            short port = 6060;

            /*
            if (args.Length > 0)
            {
                var arg = args[0].Split(':');
                if (arg[0].Length == 0 || arg[0] != "*") address = IPAddress.Parse(arg[0]);

                port = short.Parse(arg[1]);
            }*/

            Console.WriteLine("To restart server - press R");

            GameServer singleGameServer = new GameServer(address, port);
            StartGameServer(singleGameServer);
            
            ConsoleKey key;
            do
            {
                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.R:
                        if (tokens.Count > 0)
                        {
                            StopGameServer(singleGameServer);

                            Console.WriteLine("Restarting server...");
                            Thread.Sleep(2000);
                        }

                        try
                        {
                            singleGameServer = new GameServer(address, port);
                            StartGameServer(singleGameServer);
                        }
                        catch (SocketException)
                        {
                            Console.WriteLine("Server start failed... Wait a short while and try pressing again.");
                        }
                        break;
                }
            } while (key != ConsoleKey.Q && key != ConsoleKey.Escape);
        }

        private static void StartGameServer(GameServer gameServer)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();

            gameServer.Start(tokenSource.Token);
            tokens.Push(tokenSource);
        }

        private static void StopGameServer(GameServer gameServer)
        {
            tokens.Pop().Cancel();
        }
    }
}