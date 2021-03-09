namespace PolypolyGameServer
{
    public class Player
    {
        public byte ConsecutiveDoubleDice;
        public bool hasDoubleRentCoupon;
        public bool hasJailCoupon;
        public bool isBankrupt;
        public byte JailTurns;
        public int Money;
        public int Position;
        public bool isAnimationDone;
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
    }
}