namespace PolypolyGame
{
    /// <summary>
    /// The configurations of a game.
    /// </summary>
    public struct GameConfig
    {
        /// <summary>
        /// Gets a new instance of <c>GameConfig</c> with values configured to default settings.
        /// </summary>
        public static GameConfig StandardConfig =>
            new GameConfig
            {
                MaxPlayers = 4,
                StartMoney = 20000,
                SentenceDuration = 3,
                PassGoReward = 3000, // Previously 5000
                CollectRentInPrison = true,
                TaxAmount = 3000,
                TressureTileReward = 6000,
                ChanceCardMoneyReward = 5000,
                ChanceCardMoneyPenalty = 1500,
                ChanceCardPrisonCouponWorth = 4000,
                UpgradePropertyCost = 5000,
            };

        /// <summary>
        /// Gets or sets the maximum amount of players allowed in a lobby.
        /// </summary>
        /// TODO: Is this value enforced correctly?
        public int MaxPlayers { get; set; }

        /// <summary>
        /// Gets or sets the duration of a prison sentence.
        /// </summary>
        public byte SentenceDuration { get; set; }

        /// <summary>
        /// Gets or sets the amount of money passing go rewards.
        /// </summary>
        public int PassGoReward { get; set; }

        /// <summary>
        /// Gets or sets the amount of money players are given in the first round.
        /// </summary>
        public int StartMoney { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a player can collect rent while in prison.
        /// </summary>
        public bool CollectRentInPrison { get; set; }

        /// <summary>
        /// Gets or sets the amount of money to be paid when landing on tax.
        /// </summary>
        public int TaxAmount { get; set; }

        /// <summary>
        /// Gets or sets the amount of money the <c>MoneyAdd</c> chancecard rewards.
        /// </summary>
        public int ChanceCardMoneyReward { get; set; }

        /// <summary>
        /// Gets or sets the amount of money the <c>MoneyDeduct</c> chancecard deducts.
        /// </summary>
        public int ChanceCardMoneyPenalty { get; set; }

        /// <summary>
        /// Gets or sets the money given instead of an extra prison coupon.
        /// </summary>
        public int ChanceCardPrisonCouponWorth { get; set; }

        /// <summary>
        /// Gets or sets the amount of money the treasure tile rewards.
        /// </summary>
        public int TressureTileReward { get; set; }

        /// <summary>
        /// Gets or sets the amount of money upgrading a property costs.
        /// Possibly legacy value??.
        /// </summary>
        public int UpgradePropertyCost { get; set; }
    }
}