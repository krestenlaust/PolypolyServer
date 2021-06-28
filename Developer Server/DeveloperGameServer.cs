// <copyright file="DeveloperGameServer.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

using ClientBot;
using PolypolyGame;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Developer_Server
{
    /// <summary>
    /// Game server with debugging in mind.
    /// </summary>
    class DeveloperGameServer
    {
        private const int UpdatesPerSecondCap = 30;
        private const int Port = 6060;
        public Lobby Lobby;
        private readonly Stopwatch updateTime = new Stopwatch();
        private readonly List<Bot> bots = new List<Bot>();
        private readonly ToggleableLogger log;

        public GameLogic GameLogic { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeveloperGameServer"/> class.
        /// Starts a new server on port 6060.
        /// </summary>
        public DeveloperGameServer(bool loggingEnabled)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, Port);

            log = new ToggleableLogger(loggingEnabled);
            Lobby = new Lobby(endPoint, log);
            Lobby.GameStarted += (GameLogic gameLogic) => GameLogic = gameLogic;
        }

        /// <summary>
        /// Enables logging.
        /// </summary>
        public void EnableLogging()
        {
            Console.WriteLine("| Logging enabled |");
            log.isPrinting = true;
        }

        /// <summary>
        /// Disables logging.
        /// </summary>
        public void DisableLogging()
        {
            Console.WriteLine("| Logging disable |");
            log.isPrinting = false;
        }

        /// <summary>
        /// Instantiates a <see cref="Bot"/> and connects it to localhost.
        /// </summary>
        public void SpawnBot()
        {
            bots.Add(new Bot(IPAddress.Loopback.ToString(), Port, bots.Count == 0));
        }

        /// <summary>
        /// Start accepting players.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public void Start(CancellationToken cancellationToken)
        {
            Lobby.Start();

            Task.Run(() => NetworkLoop(cancellationToken));
        }

        private void NetworkLoop(CancellationToken token)
        {
            while (true)
            {
                double waitDuration = Math.Max(0, 1000 / UpdatesPerSecondCap - (int)updateTime.ElapsedMilliseconds);
                bool cancelled = token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(waitDuration));

                updateTime.Restart();

                if (cancelled)
                {
                    Lobby.Stop();
                    break;
                }

                foreach (var item in bots)
                {
                    item.UpdateNetwork();
                }

                Lobby.GameLoop();
            }
        }
    }
}
