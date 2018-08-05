// Some code from https://github.com/Voxtric/Minecraft-Level-Ripper




using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Text;

namespace YavaTool
{
    struct Vector
    {
        public float x;
        public float y;
        public float z;

        public static Vector operator +(Vector a, Vector b)
        {
            Vector res;
            res.x = a.x + b.x;
            res.y = a.y + b.y;
            res.z = a.z + b.z;
            return res;
        }

        public static Vector operator -(Vector a, Vector b)
        {
            Vector res;
            res.x = a.x - b.x;
            res.y = a.y - b.y;
            res.z = a.z - b.z;
            return res;
        }

        public static Vector operator *(Vector a, float b)
        {
            Vector res;
            res.x = a.x * b;
            res.y = a.y * b;
            res.z = a.z * b;
            return res;
        }

        public static Vector Lerp(Vector a, Vector b, float f)
        {
            return a * f + b * (1 - f);
        }
    }

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

    struct VBSP_Face
    {
        public int firstedge;
        public short numedges;
        public short texinfo;
        public short dispinfo;
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

    struct VBSP_Node
    {
        public int child_1;
        public int child_2;
    }

    struct VBSP_Leaf
    {
        //public ushort firstleafface;
        //public ushort numleaffaces;
        public ushort firstleafbrush;
        public ushort numleafbrushes;
    }

    struct VBSP_Displacement
    {
        public Vector pos;
        public int dispVertStart;
        public int power;
    }

    struct VBSP_Disp_Vert
    {
        public Vector vec;
        public float dist;
        public float alpha;
    }

    class VBSP
    {
        const int LUMP_PLANES = 1;
        const int LUMP_TEXDATA = 2;
        const int LUMP_VERTS = 3;
        const int LUMP_NODES = 5;
        const int LUMP_TEXINFO = 6;
        const int LUMP_FACES = 7;
        const int LUMP_LEAFS = 10;
        const int LUMP_EDGES = 12;
        const int LUMP_SURFEDGES = 13;
        const int LUMP_LEAFFACES = 16;
        const int LUMP_LEAFBRUSHES = 17;
        const int LUMP_BRUSHES = 18;
        const int LUMP_BRUSHSIDES = 19;
        const int LUMP_DISPINFO = 26;
        const int LUMP_DISP_VERTS = 33;
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

                // Read faces
                reader.BaseStream.Seek(lumps[LUMP_FACES].fileofs, SeekOrigin.Begin);
                int face_count = lumps[LUMP_FACES].filelen / 56;
                var faces = new VBSP_Face[face_count];
                for (int i = 0; i < face_count; i++)
                {
                    reader.ReadUInt16();
                    reader.ReadUInt16();

                    faces[i].firstedge = reader.ReadInt32();
                    faces[i].numedges = reader.ReadInt16();
                    faces[i].texinfo = reader.ReadInt16();
                    faces[i].dispinfo = reader.ReadInt16(); // 10

                    reader.ReadInt16();
                    reader.ReadInt32();

                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();

                    reader.ReadInt32();
                    reader.ReadInt32();
                }

                // Read surfedges
                reader.BaseStream.Seek(lumps[LUMP_SURFEDGES].fileofs, SeekOrigin.Begin);
                int surfedge_count = lumps[LUMP_SURFEDGES].filelen / 4;
                var surfedges = new int[surfedge_count];
                for (int i = 0; i < surfedge_count; i++)
                {
                    surfedges[i] = reader.ReadInt32();
                }

                // Read edges
                reader.BaseStream.Seek(lumps[LUMP_EDGES].fileofs, SeekOrigin.Begin);
                int edge_count = lumps[LUMP_EDGES].filelen / 4;
                var edges = new (ushort, ushort)[edge_count];
                for (int i = 0; i < edge_count; i++)
                {
                    edges[i].Item1 = reader.ReadUInt16();
                    edges[i].Item2 = reader.ReadUInt16();
                }

                // Read vertices
                reader.BaseStream.Seek(lumps[LUMP_VERTS].fileofs, SeekOrigin.Begin);
                int vert_count = lumps[LUMP_VERTS].filelen / 12;
                var verts = new Vector[vert_count];
                for (int i = 0; i < vert_count; i++)
                {
                    verts[i].x = reader.ReadSingle();
                    verts[i].y = reader.ReadSingle();
                    verts[i].z = reader.ReadSingle();
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

                // Read nodes
                reader.BaseStream.Seek(lumps[LUMP_NODES].fileofs, SeekOrigin.Begin);
                int node_count = lumps[LUMP_NODES].filelen / 32;
                var nodes = new VBSP_Node[node_count];
                for (int i = 0; i < node_count; i++)
                {
                    reader.ReadInt32();

                    nodes[i].child_1 = reader.ReadInt32();
                    nodes[i].child_2 = reader.ReadInt32();

                    for (int j=0;j<6;j++)
                        reader.ReadInt16();

                    reader.ReadUInt16();
                    reader.ReadUInt16();
                    reader.ReadUInt16();
                    reader.ReadUInt16();
                }

                // Read leaves
                reader.BaseStream.Seek(lumps[LUMP_LEAFS].fileofs, SeekOrigin.Begin);
                int leaf_count = lumps[LUMP_LEAFS].filelen / 32;
                var leaves = new VBSP_Leaf[leaf_count];
                for (int i = 0; i < leaf_count; i++)
                {
                    reader.ReadInt32(); // 4

                    reader.ReadInt16();
                    reader.ReadInt16(); // 4

                    for (int j = 0; j < 6; j++) // 12
                        reader.ReadInt16();

                    /*leaves[i].firstleafface =*/ reader.ReadUInt16();
                    /*leaves[i].numleaffaces =*/ reader.ReadUInt16();
                    leaves[i].firstleafbrush = reader.ReadUInt16();
                    leaves[i].numleafbrushes = reader.ReadUInt16();

                    reader.ReadInt16(); // 2

                    // PADDING
                    reader.ReadInt16();
                }

                // Read leafBrushes
                reader.BaseStream.Seek(lumps[LUMP_LEAFBRUSHES].fileofs, SeekOrigin.Begin);
                int leafbrush_count = lumps[LUMP_LEAFBRUSHES].filelen / 2;
                var leafbrushes = new ushort[leafbrush_count];
                for (int i = 0; i < leafbrush_count; i++)
                {
                    leafbrushes[i] = reader.ReadUInt16();
                }

                // Read dispInfo
                reader.BaseStream.Seek(lumps[LUMP_DISPINFO].fileofs, SeekOrigin.Begin);
                int disp_count = lumps[LUMP_DISPINFO].filelen / 176;
                var disp_info = new VBSP_Displacement[disp_count];
                for (int i = 0; i < disp_count; i++)
                {
                    // VECTOR START (12)
                    disp_info[i].pos.x = reader.ReadSingle();
                    disp_info[i].pos.y = reader.ReadSingle();
                    disp_info[i].pos.z = reader.ReadSingle();

                    disp_info[i].dispVertStart = reader.ReadInt32();
                    reader.ReadInt32(); // --
                    disp_info[i].power = reader.ReadInt32();

                    for (int j = 0; j < 152; j++)
                        reader.ReadByte();
                }

                // Read dispVerts
                reader.BaseStream.Seek(lumps[LUMP_DISP_VERTS].fileofs, SeekOrigin.Begin);
                int disp_vert_count = lumps[LUMP_DISP_VERTS].filelen / 20;
                var disp_verts = new VBSP_Disp_Vert[disp_vert_count];
                for (int i = 0; i < disp_vert_count; i++)
                {
                    disp_verts[i].vec.x = reader.ReadSingle();
                    disp_verts[i].vec.y = reader.ReadSingle();
                    disp_verts[i].vec.z = reader.ReadSingle();

                    disp_verts[i].dist = reader.ReadSingle();
                    disp_verts[i].alpha = reader.ReadSingle();
                }


                // TODO move this cruft to it's own function
                Console.WriteLine("Voxelizing displacements...");
                foreach (var face in faces)
                {
                    if (face.dispinfo != -1)
                    {
                        var disp = disp_info[face.dispinfo];
                        var low_base = disp.pos;

                        if (face.numedges != 4)
                            throw new Exception("Bad displacement.");

                        // Get vertices.
                        var face_verts = new Vector[4];
                        int base_i = -1;
                        float base_dist = Single.PositiveInfinity;

                        for (int i = 0; i < 4; i++)
                        {
                            int surfedge = surfedges[face.firstedge + i];
                            int vert_i;
                            if (surfedge < 0)
                            {
                                vert_i = edges[-surfedge].Item2;
                            }
                            else
                            {
                                vert_i = edges[surfedge].Item1;
                            }

                            face_verts[i] = verts[vert_i];

                            float this_dist =
                                Math.Abs(verts[vert_i].x - low_base.x) +
                                Math.Abs(verts[vert_i].y - low_base.y) +
                                Math.Abs(verts[vert_i].z - low_base.z);

                            if (this_dist < base_dist)
                            {
                                base_dist = this_dist;
                                base_i = i;
                            }
                        }

                        if (base_i == -1)
                            throw new Exception("Bad displacement.");

                        var high_base = face_verts[(base_i + 3) % 4];
                        var high_ray = face_verts[(base_i + 2) % 4] - high_base;
                        var low_ray = face_verts[(base_i + 1) % 4] - low_base;

                        int quads_wide = (2 << (disp.power - 1));
                        int verts_wide = quads_wide + 1;

                        const int XYZZY = 256;

                        for (int y = 0; y < XYZZY; y++)
                        {
                            float fy = y / (float)XYZZY;
                            int qy = (int)(fy / (1.0 / quads_wide));

                            var mid_base = low_base + low_ray * fy;
                            var mid_ray = high_base + high_ray * fy - mid_base;

                            for (int x = 0; x < XYZZY; x++)
                            {
                                float fx = x / (float)XYZZY;
                                int qx = (int)(fx / (1.0 / quads_wide));

                                var vert_base = disp_verts[disp.dispVertStart + (qx + qy * verts_wide)];
                                var vert_x = disp_verts[disp.dispVertStart + (qx + 1 + qy * verts_wide)];
                                var vert_y = disp_verts[disp.dispVertStart + (qx + (qy + 1) * verts_wide)];
                                var vert_xy = disp_verts[disp.dispVertStart + (qx + 1 + (qy + 1) * verts_wide)];

                                var pos_base = vert_base.vec * vert_base.dist;
                                var pos_x = vert_x.vec * vert_x.dist;
                                var pos_y = vert_y.vec * vert_y.dist;
                                var pos_xy = vert_xy.vec * vert_xy.dist;

                                var disp_fx = 1 - (float)(fx % (1.0 / quads_wide) * quads_wide);
                                var disp_fy = 1 - (float)(fy % (1.0 / quads_wide) * quads_wide);

                                var pos_lerp_x1 = Vector.Lerp(pos_base, pos_x, disp_fx);
                                var pos_lerp_x2 = Vector.Lerp(pos_y, pos_xy, disp_fx);

                                var pos_lerp_y = Vector.Lerp(pos_lerp_x1, pos_lerp_x2, disp_fy);

                                var pos = (mid_base + mid_ray * fx + pos_lerp_y) * (1 / world.scale);

                                var pos_int = ((int)Math.Round(pos.x), (int)Math.Round(pos.y), (int)Math.Round(pos.z));

                                try_add_block(pos_int, world, "grass");
                            }
                        }

                    }
                }

                var map_brushes = new HashSet<VBSP_Brush>();

                var start_node = nodes[0];

                // Voxelize
                Action<VBSP_Leaf> handle_leaf = (VBSP_Leaf leaf) =>
                {
                    for (int i = leaf.firstleafbrush; i < leaf.firstleafbrush + leaf.numleafbrushes; i++)
                    {
                        map_brushes.Add(brushes[leafbrushes[i]]);
                    }
                };

                Action<VBSP_Node> traverse = null;
                traverse = (VBSP_Node node) => {
                    if (node.child_1 >= 0)
                        traverse(nodes[node.child_1]);
                    else
                        handle_leaf(leaves[-1 - node.child_1]);

                    if (node.child_2 >= 0)
                        traverse(nodes[node.child_2]);
                    else
                        handle_leaf(leaves[-1 - node.child_2]);
                };

                Console.WriteLine("Gathering brushes...");
                traverse(start_node);

                Console.WriteLine("Voxelizing brushes...");

                int handled_count = 0;
                foreach (var brush in map_brushes)
                {
                    handled_count++;
                    Console.WriteLine("Voxelizing brush " + handled_count + " / " + map_brushes.Count);

                    var materials = extract_materials(brush, sides, texinfo, texdata, texstrings);

                    var brush_planes = extract_planes(brush, sides, planes);

                    process_planes(brush_planes, world, materials);
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

        static string[] extract_materials(VBSP_Brush brush, VBSP_Side[] sides, int[] texinfo, int[] texdata, string[] texstrings)
        {
            var materials = new List<string>();

            for (int i = 0; i < brush.numsides; i++)
            {
                var side = sides[brush.firstside + i];
                int info_idx = texinfo[side.texinfo];
                int data_idx = texdata[info_idx];
                string data_str = texstrings[data_idx];

                string mat = get_material_from_texture_name(data_str);
                materials.Add(mat);
            }

            return materials.ToArray();
        }

        static string get_material_from_texture_name(string str)
        {
            str = str.ToUpper();

            if (str.StartsWith("TOOLS/"))
                return null;

            //if (str.StartsWith("TOOLS/"))
            //    return null;

            if (str.Contains("CONCRETE"))
                return "rock";

            if (str.Contains("BRICK"))
                return "red";

            if (str.Contains("WOOD"))
                return "wood";

            if (str.Contains("BUILDING_TEMPLATE"))
                return "light";

            if (str.Contains("PLASTIC"))
                return "dark";

            if (str.Contains("METAL"))
                return "dark";

            if (str.Contains("GRASS"))
                return "grass";

            if (str.Contains("NATURE"))
                return "dirt";

            if (str.Contains("PLASTER"))
                return "light";

            if (str.Contains("RED"))
                return "red";

            if (str.Contains("BLUE"))
                return "water";

            if (str.Contains("WATER"))
                return null;//"water";

            // =>
            if (str == "GM_CONSTRUCT/WALL_TOP" || str == "GM_CONSTRUCT/WALL_BOTTOM")
                return "dark";

            Console.WriteLine(">>> "+ str);
            return "orange";
        }

        static void process_planes(VBSP_Plane[] planes, World world, string[] materials)
        {
            HashSet<(int, int, int)> seen = new HashSet<(int, int, int)>();
            Queue<(int, int, int)> todo = new Queue<(int, int, int)>();

            for (int i = 0; i < planes.Length; i++)
            {
                planes[i].d /= world.scale;
            }

            // Setup start pos
            {
                float start_x = 0;
                float start_y = 0;
                float start_z = 0;

                bool retry = false;
                for (int i = 0; i < 10; i++)
                {
                    retry = false;

                    foreach (var plane in planes)
                    {
                        float m = plane.x * start_x + plane.y * start_y + plane.z * start_z - plane.d;
                        if (m > 0)
                        {
                            retry = true;
                            start_x -= plane.x * m * 1.1f;
                            start_y -= plane.y * m * 1.1f;
                            start_z -= plane.z * m * 1.1f;
                        }
                    }
                    if (!retry)
                        break;
                }

                var start = ((int)Math.Round(start_x), (int)Math.Round(start_y), (int)Math.Round(start_z));
                //var start = ((int)start_x, (int)start_y, (int)start_z);

                seen.Add(start);
                todo.Enqueue(start);
            }

            while (todo.Count > 0)
            {
                var pos = todo.Dequeue();

                int x = pos.Item1;
                int y = pos.Item2;
                int z = pos.Item3;

                string material = null;
                float nearest_dist = Single.PositiveInfinity;

                for (int i = 0; i < planes.Length; i++)
                {
                    var plane = planes[i];
                    var mat = materials[i];

                    float threshold = 0;

                    if (Math.Abs(planes[i].x) > .5)
                        threshold += .5f;

                    if (Math.Abs(planes[i].y) > .5)
                        threshold += .5f;

                    if (Math.Abs(planes[i].z) > .5)
                        threshold += .5f;

                    float m = plane.x * x + plane.y * y + plane.z * z - plane.d;

                    if (m > threshold) // sqrt(3*(.5^2))
                    {
                        material = null;
                        break;
                    }
                    if (Math.Abs(m) < nearest_dist && mat != null)
                    {
                        nearest_dist = Math.Abs(m);
                        material = mat;
                    }
                }
                if (material != null)
                {
                    // Select the nearest plane/material


                    try_add_block(pos, world, material);

                    (int, int, int)[] near = {
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
                } /*else if (seen.Count == 1) {
                    Console.WriteLine("RER");
                    for (int i = 0; i < planes.Length; i++)
                    {
                        var plane = planes[i];

                        Console.WriteLine(":: " + plane.x + " " + plane.y + " " + plane.z + " :: " + plane.d + " :: " + materials[i]);
                    }
                    try_add_block(pos, world, "test");
                }*/
            }

            //seen.Add(current_pos);


            //try_add_block(current_pos, world);


        }

        static void try_add_block((int,int,int) pos, World world, string material) {
            int offset = (int)(12800 / world.scale);

            int x = pos.Item1 + offset;
            int y = pos.Item2 + offset;
            int z = pos.Item3 + offset;

            int maxs = (int)(25600 / world.scale);

            if (x >= 0 && y >= 0 && z >= 0 && x < maxs && y < maxs && z < maxs)
            {
                world.set(x, y, z, material);
            }
        }
    }
}
