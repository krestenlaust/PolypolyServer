using System;

namespace PolypolyGameServer
{
    public class ServerBoard
    {
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

        public readonly TileProperty[] PropertyTiles = new TileProperty[32];
        public readonly TileType[] TileTypes = new TileType[32];

        public byte JailtileIndex;
        public byte TraintileIndex;

        public byte Size => (byte) TileTypes.Length;

        public static ServerBoard GenerateStandardBoard()
        {
            // Initialize standard board
            ServerBoard standardBoard = new ServerBoard();
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
            standardBoard.PropertyTiles[1] = new TileProperty(3400);
            standardBoard.PropertyTiles[2] = new TileProperty(2900);
            standardBoard.PropertyTiles[3] = new TileProperty(2700);

            standardBoard.PropertyTiles[6] = new TileProperty(5600);
            standardBoard.PropertyTiles[7] = new TileProperty(5000);

            standardBoard.PropertyTiles[9] = new TileProperty(8200);
            standardBoard.PropertyTiles[10] = new TileProperty(6400);
            standardBoard.PropertyTiles[11] = new TileProperty(7100);

            standardBoard.PropertyTiles[14] = new TileProperty(11700);
            standardBoard.PropertyTiles[15] = new TileProperty(10400);

            standardBoard.PropertyTiles[17] = new TileProperty(5700);
            standardBoard.PropertyTiles[18] = new TileProperty(4100);
            standardBoard.PropertyTiles[19] = new TileProperty(6900);

            standardBoard.PropertyTiles[21] = new TileProperty(6200);
            standardBoard.PropertyTiles[22] = new TileProperty(6800);
            standardBoard.PropertyTiles[23] = new TileProperty(8500);

            standardBoard.PropertyTiles[25] = new TileProperty(13400);

            standardBoard.PropertyTiles[27] = new TileProperty(15200);

            standardBoard.PropertyTiles[29] = new TileProperty(7600);
            standardBoard.PropertyTiles[30] = new TileProperty(8200);
            standardBoard.PropertyTiles[31] = new TileProperty(7400);

            return standardBoard;
        }

        public sealed class TileProperty
        {
            public enum BuildingState : byte
            {
                Unpurchased = 0,
                Level1 = 1,
                Level2 = 2,
                Level3 = 3
            }

            public BuildingState BuildingLevel = BuildingState.Unpurchased;
            public byte Owner = byte.MaxValue;
            public int BaseCost { get; }

            public int Rent =>
                (int)Math.Round(
                    BaseCost * (0.5f +
                    0.25f * ((int)BuildingLevel - 1))
                );

            public TileProperty(int cost)
            {
                BaseCost = cost;
            }

            public TileProperty(int cost, byte owner, BuildingState buildingLevel)
            {
                BaseCost = cost;
                Owner = owner;
                BuildingLevel = buildingLevel;
            }
        }
    }
}