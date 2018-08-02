// Some code from https://github.com/Voxtric/Minecraft-Level-Ripper




using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Text;

namespace YavaTool
{
    struct VBSP_Lump
    {
        public int fileofs;
        public int filelen;
        public int version;
        public char[] fourCC;
    }

    struct VBSP_Brush
    {
        public int firstside;
        public int numsides;
        public int contents;
    }

    struct VBSP_Side
    {
        public ushort planenum;
        public short texinfo;
        public short dispinfo;
        public short bevel;
    }

    struct VBSP_Plane
    {
        public float x;
        public float y;
        public float z;
        public float d;
        public int type;
    }

    class VBSP
    {
        const int LUMP_PLANES = 1;
        const int LUMP_TEXDATA = 2;
        const int LUMP_TEXINFO = 6;
        const int LUMP_BRUSHES = 18;
        const int LUMP_BRUSHSIDES = 19;
        const int LUMP_TEXDATA_STRING_DATA = 43;
        const int LUMP_TEXDATA_STRING_TABLE = 44;

        public static World load(string filename, float scale)
        {
            var world = new World();
            world.scale = scale;

            using (var reader = new BinaryReader(File.OpenRead(filename)))
            {
                var ident = new string(reader.ReadChars(4));
                if (ident != "VBSP")
                    throw new Exception("Not a VBSP");

                int version = reader.ReadInt32();
                if (version != 20)
                    throw new Exception("Unsupported VBSP version: " + version);

                // Read lumps
                var lumps = new VBSP_Lump[64];
                for (int i = 0; i < 64; i++)
                {
                    lumps[i].fileofs = reader.ReadInt32();
                    lumps[i].filelen = reader.ReadInt32();
                    lumps[i].version = reader.ReadInt32();
                    lumps[i].fourCC = reader.ReadChars(4);
                }

                // Read brushes
                reader.BaseStream.Seek(lumps[LUMP_BRUSHES].fileofs, SeekOrigin.Begin);
                int brush_count = lumps[LUMP_BRUSHES].filelen / 12;
                var brushes = new VBSP_Brush[brush_count];
                for (int i = 0; i < brush_count; i++) {
                    brushes[i].firstside = reader.ReadInt32();
                    brushes[i].numsides = reader.ReadInt32();
                    brushes[i].contents = reader.ReadInt32();
                }

                // Read sides
                reader.BaseStream.Seek(lumps[LUMP_BRUSHSIDES].fileofs, SeekOrigin.Begin);
                int side_count = lumps[LUMP_BRUSHSIDES].filelen / 8;
                var sides = new VBSP_Side[side_count];
                for (int i = 0; i < side_count; i++)
                {
                    sides[i].planenum = reader.ReadUInt16();
                    sides[i].texinfo = reader.ReadInt16();
                    sides[i].dispinfo = reader.ReadInt16();
                    sides[i].bevel = reader.ReadInt16();
                }

                // Read planes
                reader.BaseStream.Seek(lumps[LUMP_PLANES].fileofs, SeekOrigin.Begin);
                int plane_count = lumps[LUMP_PLANES].filelen / 20;
                var planes = new VBSP_Plane[plane_count];
                for (int i = 0; i < plane_count; i++)
                {
                    planes[i].x = reader.ReadSingle();
                    planes[i].y = reader.ReadSingle();
                    planes[i].z = reader.ReadSingle();
                    planes[i].d = reader.ReadSingle();
                    planes[i].type = reader.ReadInt32();
                }

                // Read texinfo
                reader.BaseStream.Seek(lumps[LUMP_TEXINFO].fileofs, SeekOrigin.Begin);
                int texinfo_count = lumps[LUMP_TEXINFO].filelen / 72;
                var texinfo = new int[texinfo_count];
                for (int i = 0; i < texinfo_count; i++)
                {
                    for (int j = 0; j < 16; j++)
                        reader.ReadSingle();
                    reader.ReadInt32();
                    texinfo[i] = reader.ReadInt32();
                }

                // Read texdata
                reader.BaseStream.Seek(lumps[LUMP_TEXDATA].fileofs, SeekOrigin.Begin);
                int texdata_count = lumps[LUMP_TEXDATA].filelen / 32;
                var texdata = new int[texdata_count];
                for (int i = 0; i < texdata_count; i++)
                {
                    for (int j = 0; j < 3; j++)
                        reader.ReadSingle();
                    texdata[i] = reader.ReadInt32();
                    for (int j = 0; j < 4; j++)
                        reader.ReadInt32();
                }

                // Read texdata strings
                reader.BaseStream.Seek(lumps[LUMP_TEXDATA_STRING_TABLE].fileofs, SeekOrigin.Begin);
                int texstring_count = lumps[LUMP_TEXDATA_STRING_TABLE].filelen / 4;
                var texstring_offsets = new int[texstring_count];
                for (int i = 0; i < texdata_count; i++)
                {
                    texstring_offsets[i] = reader.ReadInt32();
                }

                var texstrings = new string[texstring_count];
                for (int i = 0; i < texdata_count; i++)
                {
                    int offset = texstring_offsets[i];
                    reader.BaseStream.Seek(lumps[LUMP_TEXDATA_STRING_DATA].fileofs + offset, SeekOrigin.Begin);
                    var builder = new StringBuilder();

                    for (; ; )
                    {
                        byte b = reader.ReadByte();
                        if (b == 0)
                            break;
                        builder.Append((char)b);
                    }

                    texstrings[i] = builder.ToString();
                }

                for (int i = 0; i < brushes.Length; i++) {
                    //Console.WriteLine("Voxelizing Brush " + (i + 1) + " / " + brushes.Length);

                    var brush = brushes[i];

                    if ((brush.contents & 1) != 0)
                    {
                        var material = extract_material(brush, sides, texinfo, texdata, texstrings);

                        if (material != null)
                        {
                            var brush_planes = extract_planes(brush, sides, planes);

                            process_planes(brush_planes, world, material);
                        }
                    }
                }

                return world;
            }
        }

        static VBSP_Plane[] extract_planes(VBSP_Brush brush, VBSP_Side[] sides, VBSP_Plane[] planes)
        {
            VBSP_Plane[] my_planes = new VBSP_Plane[brush.numsides];

            for (int i = 0; i < brush.numsides; i++)
            {
                var side = sides[brush.firstside + i];

                my_planes[i] = planes[side.planenum];
            }

            return my_planes;
        }

        static string extract_material(VBSP_Brush brush, VBSP_Side[] sides, int[] texinfo, int[] texdata, string[] texstrings)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < brush.numsides; i++)
            {
                var side = sides[brush.firstside + i];
                int info_idx = texinfo[side.texinfo];
                int data_idx = texdata[info_idx];
                string data_str = texstrings[data_idx];

                string mat = get_material_from_texture_name(data_str);
                if (mat != null)
                {
                    counts[mat] = counts.GetValueOrDefault(mat, 0) + 1;
                }
            }
            var winner = counts.FirstOrDefault(x => x.Value == counts.Values.Max()).Key;

            if (winner == "void")
                return null;
            else if (winner == null)
                return "orange";

            return winner;
        }

        static string get_material_from_texture_name(string str)
        {
            str = str.ToUpper();

            if (str == "TOOLS/TOOLSSKYBOX" || str == "TOOLS/TOOLSTRIGGER")
                return "void";

            if (str == "TOOLS/TOOLSNODRAW")
                return null;

            if (str.Contains("CONCRETE"))
                return "rock";

            if (str.Contains("BRICK"))
                return "red";

            if (str.Contains("BUILDING_TEMPLATE"))
                return "light";

            if (str.Contains("PLASTIC"))
                return "dark";

            if (str.Contains("METAL"))
                return "dark";

            if (str.Contains("GRASS"))
                return "grass";

            if (str.Contains("PLASTER"))
                return "light";

            if (str == "GM_CONSTRUCT/WALL_TOP" || str == "GM_CONSTRUCT/WALL_BOTTOM")
                return "dark";

            Console.WriteLine(">>> "+ str);
            return null;
        }

        static void process_planes(VBSP_Plane[] planes, World world, string material)
        {
            HashSet<(int, int, int)> seen = new HashSet<(int, int, int)>();
            Queue<(int, int, int)> todo = new Queue<(int, int, int)>();

            {
                float start_x = 0;
                float start_y = 0;
                float start_z = 0;

                float nx = 0;
                float ny = 0;
                float nz = 0;

                for (int i = 0; i < planes.Length; i++)
                {
                    planes[i].d /= world.scale;

                    var plane = planes[i];

                    start_x += plane.x * plane.d;
                    start_y += plane.y * plane.d;
                    start_z += plane.z * plane.d;

                    nx += Math.Abs(plane.x);
                    ny += Math.Abs(plane.y);
                    nz += Math.Abs(plane.z);
                }

                start_x /= nx;
                start_y /= ny;
                start_z /= nz;

                var start = ((int)start_x, (int)start_y, (int)start_z);
                seen.Add(start);
                todo.Enqueue(start);
            }

            while (todo.Count > 0)
            {
                var pos = todo.Dequeue();

                int x = pos.Item1;
                int y = pos.Item2;
                int z = pos.Item3;

                bool good = true;
                foreach (var plane in planes)
                {
                    float m = plane.x * x + plane.y * y + plane.z * z - plane.d;
                    if (m > .5)
                    {
                        good = false;
                        break;
                    }
                }
                if (good)
                {
                    try_add_block(pos, world, material);

                    (int,int,int)[] near = {
                        (x + 1, y, z),
                        (x - 1, y, z),
                        (x, y + 1, z),
                        (x, y - 1, z),
                        (x, y, z + 1),
                        (x, y, z - 1)
                    };

                    foreach (var near_pos in near) {
                        if (!seen.Contains(near_pos))
                        {
                            seen.Add(near_pos);
                            todo.Enqueue(near_pos);
                        }
                    }
                }
            }

            //seen.Add(current_pos);


            //try_add_block(current_pos, world);


        }

        static void try_add_block((int,int,int) pos, World world, string material) {
            int x = pos.Item1 + 400;
            int y = pos.Item2 + 400;
            int z = pos.Item3 + 40;

            if (x >= 0 && y >= 0 && z >= 0)
            {
                world.set(x, y, z, material);
            }
            else
            {
                Console.WriteLine("SKIP " + x + " " + y + " " + z);
            }
        }
    }
}
