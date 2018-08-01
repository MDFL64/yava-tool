using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

using fNbt;

using SDL2;

namespace YavaTool
{
    class Program
    {
        static void Main(string[] args)
        {
            /*var world = new World();

            for (int x = 0; x < 100; x++) {
                for (int y = 0; y < 100; y++)
                {
                    world.set(x, y, 0, "rock");
                }
            }*/

            //Fortblox.load(@"C:\Users\cogg\Desktop\fortblox\data\maps\test.map");

            var world = Minecraft.load(@"C:\Users\cogg\AppData\Roaming\.minecraft\saves\MEME\region\r.0.0.mca");

            world.save(@"C:\Program Files (x86)\Steam\steamapps\common\GarrysMod\garrysmod\data\yava\testbed\testout.yava.dat");
            Console.ReadKey();
        }
    }

    class World
    {
        Dictionary<(int, int, int), Chunk> chunks = new Dictionary<(int, int, int), Chunk>();
        Dictionary<string, ushort> names_to_ids = new Dictionary<string, ushort>();
        List<string> ids_to_names = new List<string>();

        public World()
        {
            ids_to_names.Add("void");
            names_to_ids.Add("void", 0);
        }

        public float scale = 40;

        public void set(int x, int y, int z, string name)
        {
            if (x < 0 || y < 0 || z < 0)
                throw new Exception("Bad coordinate.");

            // Get ID
            ushort id;
            if (!names_to_ids.TryGetValue(name, out id))
            {
                id = (ushort)ids_to_names.Count;
                ids_to_names.Add(name);
                names_to_ids.Add(name, id);
            }

            // Get chunk
            var chunk_coords = (x >> 5, y >> 5, z >> 5);
            Chunk chunk;
            if (!chunks.TryGetValue(chunk_coords, out chunk))
            {
                chunk = new Chunk();
                chunks.Add(chunk_coords, chunk);
            }

            chunk.set(x & 0x1F, y & 0x1F, z & 0x1F, id);
        }

        public void save(string filename)
        {
            using (var writer = new BinaryWriter(File.OpenWrite(filename)))
            {
                // Header
                writer.Write("YAVA1\n".ToCharArray());
                // IDS
                writer.Write((ushort)ids_to_names.Count);
                foreach (var name in ids_to_names) {
                    writer.Write((name+"\n").ToCharArray());
                }
                writer.Write(scale);
                writer.Write((ushort)chunks.Count);
                foreach (var pair in chunks)
                {
                    writer.Write((ushort)pair.Key.Item1);
                    writer.Write((ushort)pair.Key.Item2);
                    writer.Write((ushort)pair.Key.Item3);
                    Console.WriteLine("Writing chunk: " + pair.Key.Item1 + " " + pair.Key.Item2 + " " + pair.Key.Item3);

                    pair.Value.writeTo(writer);
                }
            }
        }
    }

    class Chunk
    {
        ushort[,,] data = new ushort[32, 32, 32];

        public void set(int x, int y, int z, ushort d)
        {
            data[z, y, x] = d;
        }

        public ushort get(int x, int y, int z)
        {
            return data[z, y, x];
        }

        public void writeTo(BinaryWriter writer)
        {
            ushort type = 0;
            ushort count = 0;
            for (int z = 0; z < 32; z++)
            {
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        if (count == 0)
                        {
                            type = data[z, y, x];
                            count = 1;
                        }
                        else if (type != data[z, y, x])
                        {
                            writer.Write(type);
                            writer.Write(count);

                            type = data[z, y, x];
                            count = 1;
                        }
                        else
                        {
                            count++;
                        }
                    }
                }
            }

            writer.Write(type);
            writer.Write(count);
        }
    }

    class Fortblox
    {
        public static World load(string filename)
        {
            using (var reader = new BinaryReader(new GZipStream(File.OpenRead(filename), CompressionMode.Decompress)))
            {
                var world = new World();
                world.scale = 25;

                int size_x = reader.ReadInt32();
                int size_z = reader.ReadInt32();
                int size_y = reader.ReadInt32();

                reader.ReadUInt32();

                for (int z = 0; z < size_z; z++)
                {
                    for (int y = 0; y < size_y; y++)
                    {
                        for (int x = 0; x < size_x; x++)
                        {
                            world.set(size_x - x, y, z, getBlockName(reader.ReadByte()));
                        }
                    }
                }

                for (int y = 0; y < size_y; y++)
                {
                    for (int x = 0; x < size_x; x++)
                    {
                        world.set(x, y, size_z, "void");
                    }
                }

                return world;
            }
        }

        public static string getBlockName(byte id)
        {
            switch (id) {
                case 0:
                    return "void";
                case 1:
                    return "rock";
                case 2:
                    return "dark";
                case 4:
                    return "light";
                case 5:
                    return "red";
                case 6:
                    return "orange";
                case 10:
                    return "green";
                case 128:
                    return "dirt";
            }
            return "rock";
        }
    }

}
