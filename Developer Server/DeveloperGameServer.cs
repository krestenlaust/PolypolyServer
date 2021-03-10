using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ClientBot;
using PolypolyGameServer;

namespace Developer_Server
{
    class DeveloperGameServer
    {
        private const int FramesPerSecondCap = 30;
        public Lobby Lobby;
        public GameLogic GameLogic { get; private set; }
        private readonly Stopwatch frameTime = new Stopwatch();
        private List<Bot> bots = new List<Bot>();

        /// <summary>
        /// Starts a new server on port 6060.
        /// </summary>
        public DeveloperGameServer(bool noLogs=true)
        {
            Lobby = new Lobby(IPAddress.Any, 6060, noLogs ? null : new SimpleLogger());
            Lobby.onGameStarted += (GameLogic gameLogic) => GameLogic = gameLogic;
        }

        public void SpawnBot()
        {
            bots.Add(new Bot(IPAddress.Loopback.ToString(), 6060));
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
                double waitDuration = Math.Max(0, 1000 / FramesPerSecondCap - (int)frameTime.ElapsedMilliseconds);
                bool cancelled = token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(waitDuration));

                frameTime.Restart();

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
