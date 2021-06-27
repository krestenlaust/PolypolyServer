using PolypolyGame;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Standalone_Server
{
    public class StandaloneGameServer
    {
        private const int UpdatesPerSecondCap = 30;
        private Lobby lobby;
        private readonly Stopwatch frameTime = new Stopwatch();

        public StandaloneGameServer(IPAddress address, short port)
        {
            lobby = new Lobby(new IPEndPoint(address, port));
        }

        /// <summary>
        /// Start accepting players.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public void Start(CancellationToken cancellationToken)
        {
            lobby.Start();

            Task.Run(() => NetworkLoop(cancellationToken));
        }

        private void NetworkLoop(CancellationToken token)
        {
            while (true)
            {
                double waitDuration = Math.Max(0, 1000 / UpdatesPerSecondCap - (int)frameTime.ElapsedMilliseconds);
                bool cancelled = token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(waitDuration));

                frameTime.Restart();

                if (cancelled)
                {
                    lobby.Stop();
                    break;
                }

                lobby.GameLoop();
            }
        }
    }
}
