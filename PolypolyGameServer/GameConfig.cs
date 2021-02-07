namespace PolypolyGameServer
{
    public struct GameConfig
    {
        public int MaxPlayers { get; set; }
        public byte SentenceLength { get; set; }
        public int PassGoReward { get; set; }

        public int StartMoney { get; set; }

        // This should be the same as prison coupon worth.
        public int PrisonBailCost { get; set; }
        public bool CollectRentInPrison { get; set; }
        public int TaxAmount { get; set; }

        /// <summary>
        ///     The amount of money the <c>MoneyAdd</c> chancecard rewards.
        /// </summary>
        public int ChanceCardMoneyReward { get; set; }

        /// <summary>
        ///     The amount of money the <c>MoneyDeduct</c> chancecard deducts.
        /// </summary>
        public int ChanceCardMoneyPenalty { get; set; }

        /// <summary>
        ///     The money given instead of an extra prison coupon.
        /// </summary>
        public int ChanceCardPrisonCouponWorth { get; set; }

        public static GameConfig StandardConfig =>
            new GameConfig
            {
                MaxPlayers = 4,
                SentenceLength = 3,
                PassGoReward = 5000,
                PrisonBailCost = 5000,
                StartMoney = 30000,
                CollectRentInPrison = true,
                TaxAmount = 3000,

                ChanceCardMoneyReward = 8000,
                ChanceCardMoneyPenalty = 1500,
                ChanceCardPrisonCouponWorth = 5000
            };
    }
}