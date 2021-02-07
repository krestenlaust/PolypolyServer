using System.Net.Sockets;

namespace PolypolyGameServer
{
    public enum TeamColor
    {
        Yellow = 0,
        Red = 1,
        Green = 2,
        Blue = 3
    }

    public class Player
    {
        public const string DEFAULT_NAME = "Nickname";
        public readonly TcpClient NetClient;
        public byte AvatarType;
        public TeamColor Color;
        public byte ConsecutiveDoubleDice;
        public bool hasDoubleRentCoupon;
        public bool hasJailCoupon;
        public bool isAnimationDone;
        public bool isHost;
        public bool isReady;
        public byte JailTurns;
        public int Money;
        public string Nickname = DEFAULT_NAME;

        public int Position;
        public bool? ReplyJailOffer = null;
        public bool? ReplyPropertyOffer = null;

        public Player(bool isHost)
        {
            this.isHost = isHost;
        }

        public Player(TcpClient NetClient, bool isHost, string Nickname)
        {
            this.NetClient = NetClient;
            this.isHost = isHost;
            this.Nickname = Nickname;
        }
    }
}