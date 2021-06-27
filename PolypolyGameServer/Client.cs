using System.Net.Sockets;

namespace PolypolyGame
{
    /// <summary>
    /// Describes color of avatar, and teams.
    /// </summary>
    public enum TeamColor : byte
    {
        Yellow = 0,
        Red = 1,
        Green = 2,
        Blue = 3
    }

    /// <summary>
    /// Represents a networked player per lobby.
    /// </summary>
    public class Client
    {
        /// <summary>
        /// Default nickname.
        /// </summary>
        public const string DefaultName = "Nickname";
        public readonly TcpClient NetClient;

        /// <summary>
        /// The type of avatar the player chooses. Placeholder if such a feature should exist in the future.
        /// </summary>
        /// TODO: Possibly replace with enum.
        public byte AvatarType;

        /// <summary>
        /// The color of the avatar and the team of the client.
        /// </summary>
        public TeamColor Color;

        /// <summary>
        /// Whether the client is the host.
        /// </summary>
        public bool isHost;

        /// <summary>
        /// Whether the client has signaled 'ready' in the lobby.
        /// </summary>
        public bool isReady;

        /// <summary>
        /// The nickname that is displayed in the lobby.
        /// </summary>
        public string Nickname = DefaultName;

        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class, defined by its tcpclient.
        /// </summary>
        /// <param name="client">The accepted TcpClient.</param>
        public Client(TcpClient client)
        {
            NetClient = client;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class, defined by its tcpclient and nickname.
        /// </summary>
        /// <param name="client">The accepted TcpClient.</param>
        /// <param name="nickname">The nickname of the client.</param>
        public Client(TcpClient client, string nickname)
        {
            NetClient = client;
            Nickname = nickname;
        }
    }
}
