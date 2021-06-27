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
    class DeveloperGameServer
    {
        private const int FramesPerSecondCap = 30;
        public Lobby Lobby;
        public GameLogic GameLogic { get; private set; }
        private readonly Stopwatch frameTime = new Stopwatch();
        private List<Bot> bots = new List<Bot>();
        private ToggleableLogger logger;

        /// <summary>
        /// Starts a new server on port 6060.
        /// </summary>
        public DeveloperGameServer(bool loggingEnabled)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 6060);

            logger = new ToggleableLogger(loggingEnabled);
            Lobby = new Lobby(endPoint, logger);
            Lobby.GameStarted += (GameLogic gameLogic) => GameLogic = gameLogic;
        }

        public void EnableLogging()
        {
            Console.WriteLine("| Logging enabled |");
            logger.isPrinting = true;
        }

        public void DisableLogging()
        {
            Console.WriteLine("| Logging disable |");
            logger.isPrinting = false;
        }

        public void SpawnBot()
        {
            bots.Add(new Bot(IPAddress.Loopback.ToString(), 6060, bots.Count == 0));
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
