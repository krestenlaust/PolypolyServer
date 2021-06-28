// <copyright file="GameBoard.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

using System;
using PolypolyGame;

namespace NetworkProtocol
{
    /// <summary>
    /// Keeps track of properties and the configuration of the board.
    /// </summary>
    public class GameBoard
    {
        private const int BoardSize = 32;
        public readonly TileProperty[] PropertyTiles = new TileProperty[BoardSize];
        public readonly TileType[] TileTypes = new TileType[BoardSize];

        public byte JailtileIndex;
        public byte TraintileIndex;

        /// <summary>
        /// Describes type of tile.
        /// </summary>
        public enum TileType
        {
            /// <summary>
            /// Default tile type. A property is purchasable.
            /// </summary>
            Property,

            /// <summary>
            /// A special tile, moves to player to <see cref="Jail"/>.
            /// </summary>
            GotoJail,

            /// <summary>
            /// A special tile, this tile doesn't do anything alone, but is the destination of <see cref="GotoJail"/>.
            /// </summary>
            Jail,

            /// <summary>
            /// A special tile. Landing on this tile triggers the drawing of a chance card.
            /// </summary>
            ChanceCard,

            /// <summary>
            /// An empty tile, sometimes referred to as 'Parking space'.
            /// </summary>
            Nothing,

            /// <summary>
            /// A special tile. Landing on this tile triggers the train-event. (Currently the treasure-cove).
            /// </summary>
            Train,

            /// <summary>
            /// A special tile. Landing on this tile subtracts a fixed amount of money, smaller than <see cref="BigTax"/>.
            /// </summary>
            Tax,

            /// <summary>
            /// A special tile. Landing on this tile subtracts an amount of money based on owned properties.
            /// </summary>
            Upkeep,

            /// <summary>
            /// A special tile. Landing on this subtracts a fixed amount of money, larger than <see cref="Tax"/>.
            /// </summary>
            BigTax,
        }

        public byte Size => (byte)TileTypes.Length;

        /// <summary>
        /// Initializes <see cref="GameBoard"/> class with standard settings.
        /// </summary>
        /// <returns><see cref="GameBoard"/> instance with default settings.</returns>
        public static GameBoard GenerateStandardBoard()
        {
            // Initialize standard board
            GameBoard standardBoard = new GameBoard
            {
                JailtileIndex = 8,
                TraintileIndex = 16
            };

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

        /// <summary>
        /// Stores the specifics of a property-tile.
        /// </summary>
        /// TODO: Maybe turn into struct.
        public sealed class TileProperty
        {
            private const float BasicRentMultiplier = 0.3f;
            private const float PerHouseAdditionalMultiplier = 0.25f;

            /// <summary>
            /// The group ID used for identifying an monopoly.
            /// </summary>
            public readonly byte GroupID;

            /// <summary>
            /// The inherent value of the property.
            /// </summary>
            public readonly int BaseCost;

            /// <summary>
            /// ID of the player, who owns this property.
            /// </summary>
            public byte Owner = byte.MaxValue;

            /// <summary>
            /// The building state of the property.
            /// </summary>
            public BuildingState BuildingLevel = BuildingState.Unpurchased;

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
            /// <param name="owner">The owner of the building.</param>
            /// <param name="buildingLevel">The state of the building.</param>
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
                /// <summary>
                /// Default property state. Describes an unpurchased property as well as a previously sold property.
                /// </summary>
                Unpurchased = 0,

                /// <summary>
                /// The state of a property that hasn't been upgraded, but is purchased.
                /// </summary>
                Level1 = 1,

                /// <summary>
                /// First upgrade of a property.
                /// </summary>
                Level2 = 2,

                /// <summary>
                /// Final upgrade of a property.
                /// </summary>
                Level3 = 3,
            }

            /// <summary>
            /// Gets the rent based on the building state and the base cost.
            /// </summary>
            public int Rent =>
                (int)Math.Round(
                    BaseCost * (
                    BasicRentMultiplier +
                    (PerHouseAdditionalMultiplier * ((int)BuildingLevel - 1))));

            public int UpgradeCost => GameConfig.StandardConfig.UpgradePropertyCost;

            /// <summary>
            /// Gets the total value of the property including upgrade costs.
            /// </summary>
            public int Value => UpgradeCost * ((byte)BuildingLevel - 1) + BaseCost;
        }
    }
}