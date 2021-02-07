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

        public readonly TileProperty[] TilesProperty = new TileProperty[32];
        public readonly TileType[] TilesType = new TileType[32];

        public byte JailtileIndex;
        public byte TraintileIndex;

        public byte Size => (byte) TilesType.Length;

        public static ServerBoard GenerateStandardBoard()
        {
            // Initialize standard board
            ServerBoard standardBoard = new ServerBoard();
            standardBoard.JailtileIndex = 8;
            standardBoard.TraintileIndex = 16;

            // Tile definitions
            standardBoard.TilesType[0] = TileType.Nothing; // start
            standardBoard.TilesType[1] = TileType.Property;
            standardBoard.TilesType[2] = TileType.Property;
            standardBoard.TilesType[3] = TileType.Property;
            standardBoard.TilesType[4] = TileType.ChanceCard;
            standardBoard.TilesType[5] = TileType.Tax;
            standardBoard.TilesType[6] = TileType.Property;
            standardBoard.TilesType[7] = TileType.Property;
            standardBoard.TilesType[8] = TileType.Jail;
            standardBoard.TilesType[9] = TileType.Property;
            standardBoard.TilesType[10] = TileType.Property;
            standardBoard.TilesType[11] = TileType.Property;
            standardBoard.TilesType[12] = TileType.ChanceCard;
            standardBoard.TilesType[13] = TileType.Upkeep;
            standardBoard.TilesType[14] = TileType.Property;
            standardBoard.TilesType[15] = TileType.Property;
            standardBoard.TilesType[16] = TileType.Train;
            standardBoard.TilesType[17] = TileType.Property;
            standardBoard.TilesType[18] = TileType.Property;
            standardBoard.TilesType[19] = TileType.Property;
            standardBoard.TilesType[20] = TileType.ChanceCard;
            standardBoard.TilesType[21] = TileType.Property;
            standardBoard.TilesType[22] = TileType.Property;
            standardBoard.TilesType[23] = TileType.Property;
            standardBoard.TilesType[24] = TileType.GotoJail;
            standardBoard.TilesType[25] = TileType.Property;
            standardBoard.TilesType[26] = TileType.BigTax;
            standardBoard.TilesType[27] = TileType.Property;
            standardBoard.TilesType[28] = TileType.ChanceCard;
            standardBoard.TilesType[29] = TileType.Property;
            standardBoard.TilesType[30] = TileType.Property;
            standardBoard.TilesType[31] = TileType.Property;

            // Property tiles
            standardBoard.TilesProperty[1] = new TileProperty(3400);
            standardBoard.TilesProperty[2] = new TileProperty(2900);
            standardBoard.TilesProperty[3] = new TileProperty(2700);

            standardBoard.TilesProperty[6] = new TileProperty(5600);
            standardBoard.TilesProperty[7] = new TileProperty(5000);

            standardBoard.TilesProperty[9] = new TileProperty(8200);
            standardBoard.TilesProperty[10] = new TileProperty(6400);
            standardBoard.TilesProperty[11] = new TileProperty(7100);

            standardBoard.TilesProperty[14] = new TileProperty(11700);
            standardBoard.TilesProperty[15] = new TileProperty(10400);

            standardBoard.TilesProperty[17] = new TileProperty(5700);
            standardBoard.TilesProperty[18] = new TileProperty(4100);
            standardBoard.TilesProperty[19] = new TileProperty(6900);

            standardBoard.TilesProperty[21] = new TileProperty(6200);
            standardBoard.TilesProperty[22] = new TileProperty(6800);
            standardBoard.TilesProperty[23] = new TileProperty(8500);

            standardBoard.TilesProperty[25] = new TileProperty(13400);

            standardBoard.TilesProperty[27] = new TileProperty(15200);

            standardBoard.TilesProperty[29] = new TileProperty(7600);
            standardBoard.TilesProperty[30] = new TileProperty(8200);
            standardBoard.TilesProperty[31] = new TileProperty(7400);

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

        // regler
    }
}