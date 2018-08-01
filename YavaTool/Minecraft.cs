// Some code from https://github.com/Voxtric/Minecraft-Level-Ripper




using System;

using fNbt;

using System.IO;
using System.IO.Compression;

namespace YavaTool
{
    class Minecraft
    {
        public static World load(string filename)
        {
            var world = new World();

            var chunk_data = readRegion(filename);

            foreach (var cd in chunk_data)
            {
                addChunk(cd, world);
            }

            return world;
        }

        static void addChunk(byte[] data, World world)
        {
            if (data == null)
                return;

            var nbt = new NbtFile();
            nbt.LoadFromBuffer(data, 0, data.Length, NbtCompression.AutoDetect);

            var level = nbt.RootTag.Get<NbtCompound>("Level");

            int chunk_x = level.Get("xPos").IntValue;
            int chunk_y = level.Get("zPos").IntValue;

            if (chunk_x >= 40 || chunk_y >= 40)
                return;

            for (int z = 0; z < 256; z += 32)
            {
                world.set(chunk_x * 16, chunk_y * 16, z, "void");
            }

            var sections = level.Get<NbtList>("Sections");

            foreach (NbtCompound section in sections)
            {
                byte[] blocks = section.Get("Blocks").ByteArrayValue;
                int chunk_z = section.Get("Y").ByteValue;
                Console.WriteLine(">>>" + chunk_x + " " + chunk_y + " " + chunk_z);

                for (int z = 0; z < 16; z++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        for (int x = 0; x < 16; x++)
                        {
                            byte d = blocks[z * 256 + y * 16 + x];

                            world.set(x + chunk_x * 16, y + chunk_y * 16, z + chunk_z * 16, getBlockName(d));
                        }
                    }
                }
            }
        }

        public static string getBlockName(byte id)
        {
            switch (id)
            {
                case 0:
                    return "void";
                case 1:
                    return "rock";
                case 2:
                    return "grass";
                case 3:
                    return "dirt";
                case 4:
                    return "rock"; // COBBLE
                case 5:
                    return "wood";
                case 6:
                    return "void"; // SAPLING
                case 7:
                    return "rock"; // BEDROCK
                case 8:
                case 9:
                    return "water";
                case 10:
                case 11:
                    return "orange"; // LAVA
                case 12:
                    return "sand";
                case 17:
                case 162:
                    return "tree";
                case 18:
                case 161:
                    return "leaves";
            }
            return "rock";
        }

        /*
            The following function was taken from
            https://github.com/Voxtric/Minecraft-Level-Ripper
            which is licensed as follows:

            MIT License

            Copyright (c) 2017 Benjamin James Drury

            Permission is hereby granted, free of charge, to any person obtaining a copy
            of this software and associated documentation files (the "Software"), to deal
            in the Software without restriction, including without limitation the rights
            to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
            copies of the Software, and to permit persons to whom the Software is
            furnished to do so, subject to the following conditions:

            The above copyright notice and this permission notice shall be included in all
            copies or substantial portions of the Software.

            THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
            IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
            FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
            AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
            LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
            OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
            SOFTWARE.
        */
        static byte[][] readRegion(string filePath)
        {
            const uint REGION_DIMENSIONS = 32;

            const uint SECTOR_SIZE = 4096;

            byte[][] nbtChunkData = new byte[REGION_DIMENSIONS * REGION_DIMENSIONS][];
            byte[] bytes = File.ReadAllBytes(filePath);

            uint chunkIndex = 0;
            for (uint byteIndex = 0; byteIndex < REGION_DIMENSIONS * REGION_DIMENSIONS * 4; byteIndex += 4)
            {
                //Reads the header of the file for the chunks location.
                ulong chunkStart = (ulong)((bytes[byteIndex] << 16) |
                  (bytes[byteIndex + 1] << 8) | bytes[byteIndex + 2]) * SECTOR_SIZE;
                if (chunkStart > 0)
                {
                    uint chunkSize = (uint)((bytes[chunkStart] << 24) | (bytes[chunkStart + 1] << 16) |
                      (bytes[chunkStart + 2] << 8) | bytes[chunkStart + 3]) - 1;

                    //Transfers the chunk specific data out of the byte array of the entire file.
                    byte[] chunkData = new byte[chunkSize];
                    for (uint i = 0; i < chunkSize; ++i)
                    {
                        chunkData[i] = bytes[chunkStart + 5 + i];
                    }

                    //Decompresses the chunk data into its raw NBT format.
                    using (MemoryStream outputStream = new MemoryStream())
                    {
                        MemoryStream memInputStream = new MemoryStream(chunkData);
                        // RFC1950 zlib stream has 2 bytes at beginning which we ignore (CMF and FLG) - thanks http://george.chiramattel.com/blog/2007/09/deflatestream-block-length-does-not-match.html
                        memInputStream.Seek(2, SeekOrigin.Begin);
                        using (DeflateStream inputStream = new DeflateStream(memInputStream, CompressionMode.Decompress))
                        {
                            inputStream.CopyTo(outputStream);
                        }
                        nbtChunkData[chunkIndex] = outputStream.ToArray();
                    }
                }
                ++chunkIndex;
            }
            return nbtChunkData;
        }
    }
}
