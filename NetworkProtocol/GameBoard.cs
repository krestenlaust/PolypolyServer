using System;
using PolypolyGame;

namespace NetworkProtocol
{
    public class GameBoard
    {
        public readonly TileProperty[] PropertyTiles = new TileProperty[32];
        public readonly TileType[] TileTypes = new TileType[32];

        public byte JailtileIndex;
        public byte TraintileIndex;

        public enum TileType
        {
            Property,
            GotoJail,
            Jail,
            ChanceCard,
            Nothing,
            Train,
            Tax,
            Upkeep,
            BigTax
        }

        public byte Size => (byte)TileTypes.Length;

        public static GameBoard GenerateStandardBoard()
        {
            // Initialize standard board
            GameBoard standardBoard = new GameBoard();
            standardBoard.JailtileIndex = 8;
            standardBoard.TraintileIndex = 16;

            // Tile definitions
            standardBoard.TileTypes[0] = TileType.Nothing; // start
            standardBoard.TileTypes[1] = TileType.Property;
            standardBoard.TileTypes[2] = TileType.Property;
            standardBoard.TileTypes[3] = TileType.Property;
            standardBoard.TileTypes[4] = TileType.ChanceCard;
            standardBoard.TileTypes[5] = TileType.Tax;
            standardBoard.TileTypes[6] = TileType.Property;
            standardBoard.TileTypes[7] = TileType.Property;
            standardBoard.TileTypes[8] = TileType.Jail;
            standardBoard.TileTypes[9] = TileType.Property;
            standardBoard.TileTypes[10] = TileType.Property;
            standardBoard.TileTypes[11] = TileType.Property;
            standardBoard.TileTypes[12] = TileType.ChanceCard;
            standardBoard.TileTypes[13] = TileType.Upkeep;
            standardBoard.TileTypes[14] = TileType.Property;
            standardBoard.TileTypes[15] = TileType.Property;
            standardBoard.TileTypes[16] = TileType.Train;
            standardBoard.TileTypes[17] = TileType.Property;
            standardBoard.TileTypes[18] = TileType.Property;
            standardBoard.TileTypes[19] = TileType.Property;
            standardBoard.TileTypes[20] = TileType.ChanceCard;
            standardBoard.TileTypes[21] = TileType.Property;
            standardBoard.TileTypes[22] = TileType.Property;
            standardBoard.TileTypes[23] = TileType.Property;
            standardBoard.TileTypes[24] = TileType.GotoJail;
            standardBoard.TileTypes[25] = TileType.Property;
            standardBoard.TileTypes[26] = TileType.BigTax;
            standardBoard.TileTypes[27] = TileType.Property;
            standardBoard.TileTypes[28] = TileType.ChanceCard;
            standardBoard.TileTypes[29] = TileType.Property;
            standardBoard.TileTypes[30] = TileType.Property;
            standardBoard.TileTypes[31] = TileType.Property;

            // Property tiles
            standardBoard.PropertyTiles[1] = new TileProperty(3400, 0);
            standardBoard.PropertyTiles[2] = new TileProperty(2900, 0);
            standardBoard.PropertyTiles[3] = new TileProperty(2700, 0);

            standardBoard.PropertyTiles[6] = new TileProperty(5600, 1);
            standardBoard.PropertyTiles[7] = new TileProperty(5000, 1);


            standardBoard.PropertyTiles[9] = new TileProperty(8200, 2);
            standardBoard.PropertyTiles[10] = new TileProperty(6400, 2);
            standardBoard.PropertyTiles[11] = new TileProperty(7100, 2);

            standardBoard.PropertyTiles[14] = new TileProperty(9000, 3);
            standardBoard.PropertyTiles[15] = new TileProperty(8400, 3);


            standardBoard.PropertyTiles[17] = new TileProperty(5700, 4);
            standardBoard.PropertyTiles[18] = new TileProperty(4100, 4);
            standardBoard.PropertyTiles[19] = new TileProperty(6900, 4);

            standardBoard.PropertyTiles[21] = new TileProperty(6200, 5);
            standardBoard.PropertyTiles[22] = new TileProperty(6800, 5);
            standardBoard.PropertyTiles[23] = new TileProperty(8500, 5);


            standardBoard.PropertyTiles[25] = new TileProperty(9500, 6);
            standardBoard.PropertyTiles[27] = new TileProperty(11000, 6);

            standardBoard.PropertyTiles[29] = new TileProperty(7600, 7);
            standardBoard.PropertyTiles[30] = new TileProperty(8200, 7);
            standardBoard.PropertyTiles[31] = new TileProperty(7400, 7);

            return standardBoard;
        }

        public sealed class TileProperty
        {
            private const float BasicRentMultiplier = 0.3f;
            private const float PerHouseAdditionalMultiplier = 0.25f;

            public readonly byte GroupID;
            public BuildingState BuildingLevel = BuildingState.Unpurchased;
            public byte Owner = byte.MaxValue;

            public int BaseCost { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="TileProperty"/> class.
            /// </summary>
            /// <param name="cost">The amount of money required to purchase the property.</param>
            /// <param name="groupID">The group ID it belongs to.</param>
            public TileProperty(int cost, byte groupID)
            {
                BaseCost = cost;
                GroupID = groupID;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="TileProperty"/> class.
            /// </summary>
            /// <param name="cost">The amount of money required to purchase the property.</param>
            /// <param name="groupID">The group ID it belongs to.</param>
            /// <param name="owner"></param>
            /// <param name="buildingLevel"></param>
            public TileProperty(int cost, byte groupID, byte owner, BuildingState buildingLevel)
            {
                BaseCost = cost;
                Owner = owner;
                BuildingLevel = buildingLevel;
                GroupID = groupID;
            }

            /// <summary>
            /// Describes the state of a building.
            /// </summary>
            public enum BuildingState : byte
            {
                Unpurchased = 0,
                Level1 = 1,
                Level2 = 2,
                Level3 = 3
            }

            public int Rent =>
                (int)Math.Round(
                    BaseCost * (BasicRentMultiplier +
                    PerHouseAdditionalMultiplier * ((int)BuildingLevel - 1)));

            public int UpgradeCost => GameConfig.StandardConfig.UpgradePropertyCost;

            public int Value => BaseCost + UpgradeCost * ((byte)BuildingLevel - 1);

        }
    }
}