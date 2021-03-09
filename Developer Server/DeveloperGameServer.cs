using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PolypolyGameServer;

namespace Developer_Server
{
    class DeveloperGameServer
    {
        private const int FramesPerSecondCap = 30;
        public Lobby Lobby;
        public GameLogic GameLogic { get; private set; }
        private readonly Stopwatch frameTime = new Stopwatch();

        /// <summary>
        /// Starts a new server on port 6060.
        /// </summary>
        public DeveloperGameServer(bool noLogs=true)
        {
            Lobby = new Lobby(IPAddress.Any, 6060, noLogs ? null : new SimpleLogger());
            Lobby.onGameStarted += (GameLogic gameLogic) => GameLogic = gameLogic;
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

                Lobby.GameLoop();
            }
        }
    }
}
