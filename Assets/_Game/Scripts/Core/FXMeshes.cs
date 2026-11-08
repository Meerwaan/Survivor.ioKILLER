using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Tiny flat-shaded meshes for the chunks that fly off dead Ambrés: crust plates, a wedge and a shard
    /// (8 to 16 triangles each, ~1 m across before the particle size scales them). Built once at startup.
    /// </summary>
    public static class FXMeshes
    {
        public static Mesh[] Chunks()
        {
            var rng = new System.Random(1207);
            return new[]
            {
                Plate(rng, 5, 0.22f, "Gib Plate 5"),
                Plate(rng, 4, 0.3f, "Gib Plate 4"),
                Wedge(rng),
                Shard(rng),
            };
        }

        static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>Irregular polygon slab (a flake of crust).</summary>
        static Mesh Plate(System.Random rng, int sides, float thickness, string name)
        {
            var top = new Vector3[sides];
            var bottom = new Vector3[sides];
            float start = R(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < sides; i++)
            {
                float a = start + Mathf.PI * 2f * i / sides + R(rng, -0.25f, 0.25f);
                float r = R(rng, 0.35f, 0.55f);
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                top[i] = p + Vector3.up * (thickness * 0.5f + R(rng, -0.05f, 0.05f));
                bottom[i] = p * R(rng, 0.8f, 0.95f) - Vector3.up * thickness * 0.5f;
            }
            var tris = new List<Vector3>();
            for (int i = 1; i < sides - 1; i++)
            {
                tris.Add(top[0]); tris.Add(top[i]); tris.Add(top[i + 1]);
                tris.Add(bottom[0]); tris.Add(bottom[i]); tris.Add(bottom[i + 1]);
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                tris.Add(top[i]); tris.Add(bottom[i]); tris.Add(top[j]);
                tris.Add(top[j]); tris.Add(bottom[i]); tris.Add(bottom[j]);
            }
            return Build(name, tris);
        }

        /// <summary>Triangular prism, a bit twisted (a broken corner of crust).</summary>
        static Mesh Wedge(System.Random rng)
        {
            var a = new Vector3[3];
            var b = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                float ang = Mathf.PI * 2f * i / 3f + R(rng, -0.3f, 0.3f);
                a[i] = new Vector3(Mathf.Cos(ang) * R(rng, 0.35f, 0.5f), 0.22f, Mathf.Sin(ang) * R(rng, 0.35f, 0.5f));
                b[i] = new Vector3(a[i].x * 0.7f + R(rng, -0.08f, 0.08f), -0.22f, a[i].z * 0.7f + R(rng, -0.08f, 0.08f));
            }
            var tris = new List<Vector3> { a[0], a[1], a[2], b[0], b[1], b[2] };
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                tris.Add(a[i]); tris.Add(b[i]); tris.Add(a[j]);
                tris.Add(a[j]); tris.Add(b[i]); tris.Add(b[j]);
            }
            return Build("Gib Wedge", tris);
        }

        /// <summary>Elongated bipyramid (a splinter of hardened sap).</summary>
        static Mesh Shard(System.Random rng)
        {
            var ring = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                float ang = Mathf.PI * 2f * i / 3f + R(rng, -0.2f, 0.2f);
                ring[i] = new Vector3(Mathf.Cos(ang) * R(rng, 0.16f, 0.24f), R(rng, -0.05f, 0.05f), Mathf.Sin(ang) * R(rng, 0.16f, 0.24f));
            }
            var tip = new Vector3(R(rng, -0.08f, 0.08f), 0.6f, R(rng, -0.08f, 0.08f));
            var foot = new Vector3(R(rng, -0.05f, 0.05f), -0.3f, R(rng, -0.05f, 0.05f));
            var tris = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                tris.Add(ring[i]); tris.Add(ring[j]); tris.Add(tip);
                tris.Add(ring[i]); tris.Add(ring[j]); tris.Add(foot);
            }
            return Build("Gib Shard", tris);
        }

        /// <summary>One vertex per corner (flat shading); every triangle is turned to face outward.</summary>
        static Mesh Build(string name, List<Vector3> tris)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in tris) center += p;
            center /= tris.Count;

            var verts = new Vector3[tris.Count];
            var normals = new Vector3[tris.Count];
            var indices = new int[tris.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = tris[i] - center, b = tris[i + 1] - center, c = tris[i + 2] - center;
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                n.Normalize();
                verts[i] = a;
                verts[i + 1] = b;
                verts[i + 2] = c;
                normals[i] = normals[i + 1] = normals[i + 2] = n;
                indices[i] = i;
                indices[i + 1] = i + 1;
                indices[i + 2] = i + 2;
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = indices;
            mesh.RecalculateBounds(); // stays readable: CPU-batched mesh particles read it
            return mesh;
        }
    }
}
