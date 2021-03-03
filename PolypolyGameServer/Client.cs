using System.Net.Sockets;

namespace PolypolyGameServer
{
    public enum TeamColor : byte
    {
        Yellow = 0,
        Red = 1,
        Green = 2,
        Blue = 3
    }

    public class Client
    {
        public const string DEFAULT_NAME = "Nickname";
        public readonly TcpClient NetClient;
        public byte AvatarType;
        public TeamColor Color;
        public bool isHost;
        public bool isReady;
        public string Nickname = DEFAULT_NAME;

        public Client(TcpClient client)
        {
            NetClient = client;
        }

        public Client(TcpClient client, string nickname)
        {
            NetClient = client;
            Nickname = nickname;
        }
    }
}
