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
        public bool isBankrupt;
        public byte JailTurns;
        public int Money;
        public string Nickname = DEFAULT_NAME;
        public int Position;

        /// <summary>
        /// Whether the player wants to use a prison card or not, if null then no reply.
        /// </summary>
        public bool? ReplyJailOffer = null;
        /// <summary>
        /// Whether the player wants to buy a property or not, if null then no reply.
        /// </summary>
        public bool? ReplyPropertyOffer = null;
        /// <summary>
        /// Describes the index of property to auction, if null then no reply.
        /// </summary>
        public byte? ReplyAuctionIndex = null;

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