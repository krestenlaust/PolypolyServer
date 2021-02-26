namespace PolypolyGameServer
{
    public struct GameConfig
    {
        public int MaxPlayers { get; set; }
        public byte SentenceDuration { get; set; }
        public int PassGoReward { get; set; }

        public int StartMoney { get; set; }

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

        public int TressureTileReward { get; set; }

        public int UpgradePropertyCost { get; set; }

        public static GameConfig StandardConfig =>
            new GameConfig
            {
                MaxPlayers = 4,
                StartMoney = 30000,
                SentenceDuration = 3,
                PassGoReward = 5000,
                CollectRentInPrison = true,
                TaxAmount = 3000,
                TressureTileReward = 8000,
                ChanceCardMoneyReward = 5000,
                ChanceCardMoneyPenalty = 1500,
                ChanceCardPrisonCouponWorth = 4000,
                UpgradePropertyCost = 5000,
            };
    }
}