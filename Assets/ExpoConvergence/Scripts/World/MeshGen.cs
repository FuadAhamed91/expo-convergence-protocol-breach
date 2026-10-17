using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Procedural meshes for the courier, security devices and modular architecture.
    /// Every mesh is cached by its parameters so repeated props share one mesh (batching friendly).
    /// </summary>
    public static class MeshGen
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => Cache.Clear();

        static Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = build();
            m.name = key;
            Cache[key] = m;
            return m;
        }

        static Mesh Finish(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, bool recalcNormals = false)
        {
            var mesh = new Mesh();
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            if (uv != null && uv.Count == v.Count) mesh.SetUVs(0, uv);
            if (recalcNormals || n == null || n.Count != v.Count) mesh.RecalculateNormals();
            else mesh.SetNormals(n);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // ------------------------------------------------------------------ lathe

        /// <summary>Revolves a profile (x = radius, y = height, ordered bottom to top) around the Y axis.</summary>
        public static Mesh Lathe(string key, Vector2[] profile, int segments)
        {
            return Cached("lathe_" + key + "_" + segments, () =>
            {
                int rings = profile.Length;
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                var profNormals = new Vector2[rings];
                for (int i = 0; i < rings; i++)
                {
                    Vector2 prev = profile[Mathf.Max(0, i - 1)];
                    Vector2 next = profile[Mathf.Min(rings - 1, i + 1)];
                    Vector2 tan = (next - prev);
                    if (tan.sqrMagnitude < 1e-8f) tan = Vector2.up;
                    tan.Normalize();
                    profNormals[i] = new Vector2(tan.y, -tan.x);
                }
                float total = 0f; var lengths = new float[rings];
                for (int i = 1; i < rings; i++) { total += Vector2.Distance(profile[i], profile[i - 1]); lengths[i] = total; }
                for (int i = 0; i < rings; i++)
                {
                    for (int j = 0; j <= segments; j++)
                    {
                        float a = (float)j / segments * Mathf.PI * 2f;
                        float c = Mathf.Cos(a), s = Mathf.Sin(a);
                        v.Add(new Vector3(profile[i].x * c, profile[i].y, profile[i].x * s));
                        n.Add(new Vector3(profNormals[i].x * c, profNormals[i].y, profNormals[i].x * s).normalized);
                        uv.Add(new Vector2((float)j / segments, total > 0f ? lengths[i] / total : 0f));
                    }
                }
                int stride = segments + 1;
                for (int i = 0; i < rings - 1; i++)
                {
                    for (int j = 0; j < segments; j++)
                    {
                        int a = i * stride + j, b = a + 1, c = a + stride, d = c + 1;
                        t.Add(a); t.Add(c); t.Add(d);
                        t.Add(a); t.Add(d); t.Add(b);
                    }
                }
                return Finish(v, n, uv, t);
            });
        }

        /// <summary>A limb segment hanging down from its joint: from y = 0 to y = -length.</summary>
        public static Mesh Limb(float length, float rTop, float rBottom, int segments = 12)
        {
            string key = $"limb_{length:F3}_{rTop:F3}_{rBottom:F3}";
            var pts = new List<Vector2>();
            const int cap = 4;
            for (int i = 0; i <= cap; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / cap;
                pts.Add(new Vector2(Mathf.Cos(a) * rBottom, -length + Mathf.Sin(a) * rBottom));
            }
            for (int i = 0; i <= cap; i++)
            {
                float a = Mathf.PI * 0.5f * i / cap;
                pts.Add(new Vector2(Mathf.Cos(a) * rTop, Mathf.Sin(a) * rTop));
            }
            return Lathe(key, pts.ToArray(), segments);
        }

        /// <summary>An upward tapered capsule from y = 0 to y = length.</summary>
        public static Mesh UpCapsule(float length, float rBottom, float rTop, int segments = 14)
        {
            string key = $"upcap_{length:F3}_{rBottom:F3}_{rTop:F3}";
            var pts = new List<Vector2>();
            const int cap = 4;
            for (int i = 0; i <= cap; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / cap;
                pts.Add(new Vector2(Mathf.Cos(a) * rBottom, Mathf.Sin(a) * rBottom));
            }
            for (int i = 0; i <= cap; i++)
            {
                float a = Mathf.PI * 0.5f * i / cap;
                pts.Add(new Vector2(Mathf.Cos(a) * rTop, length + Mathf.Sin(a) * rTop));
            }
            return Lathe(key, pts.ToArray(), segments);
        }

        public static Mesh Sphere(float radius, int segments = 16, int rings = 10)
        {
            var pts = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * i / rings;
                pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            pts[0].x = 0f; pts[rings].x = 0f;
            return Lathe($"sphere_{radius:F3}_{rings}", pts, segments);
        }

        /// <summary>Upper hemisphere shell (y >= 0).</summary>
        public static Mesh Hemisphere(float radius, int segments = 24, int rings = 10)
        {
            var pts = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * 0.5f * i / rings;
                pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            pts[rings].x = 0f;
            return Lathe($"hemi_{radius:F3}_{rings}", pts, segments);
        }

        /// <summary>A slice of a sphere between two longitudes (degrees, from +X towards +Z) and latitudes.</summary>
        public static Mesh SphereSector(float radius, float fromDeg, float toDeg, int segments = 24, int rings = 12, float latFrom = 0f, float latTo = 90f)
        {
            string key = $"sector_{radius:F2}_{fromDeg:F0}_{toDeg:F0}_{segments}_{rings}_{latFrom:F0}_{latTo:F0}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                for (int i = 0; i <= rings; i++)
                {
                    float lat = Mathf.Lerp(latFrom, latTo, (float)i / rings) * Mathf.Deg2Rad;
                    for (int j = 0; j <= segments; j++)
                    {
                        float lon = Mathf.Lerp(fromDeg, toDeg, (float)j / segments) * Mathf.Deg2Rad;
                        Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                        v.Add(d * radius);
                        n.Add(d);
                        uv.Add(new Vector2((float)j / segments, (float)i / rings));
                    }
                }
                int stride = segments + 1;
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < segments; j++)
                    {
                        int a = i * stride + j, b = a + 1, c = a + stride, d = c + 1;
                        t.Add(a); t.Add(c); t.Add(d);
                        t.Add(a); t.Add(d); t.Add(b);
                    }
                return Finish(v, n, uv, t);
            });
        }

        // ------------------------------------------------------------------ prisms

        /// <summary>Cylinder or n-gon prism from y = 0 to y = height. Low segment counts get flat faces.</summary>
        public static Mesh Cylinder(float rBottom, float rTop, float height, int segments = 16, bool caps = true)
        {
            string key = $"cyl_{rBottom:F3}_{rTop:F3}_{height:F3}_{segments}_{caps}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                bool flat = segments <= 8;
                float circumference = Mathf.PI * 2f * Mathf.Max(rBottom, rTop);
                for (int j = 0; j < segments; j++)
                {
                    float a0 = (float)j / segments * Mathf.PI * 2f, a1 = (float)(j + 1) / segments * Mathf.PI * 2f;
                    Vector3 b0 = new Vector3(Mathf.Cos(a0) * rBottom, 0, Mathf.Sin(a0) * rBottom);
                    Vector3 b1 = new Vector3(Mathf.Cos(a1) * rBottom, 0, Mathf.Sin(a1) * rBottom);
                    Vector3 t0 = new Vector3(Mathf.Cos(a0) * rTop, height, Mathf.Sin(a0) * rTop);
                    Vector3 t1 = new Vector3(Mathf.Cos(a1) * rTop, height, Mathf.Sin(a1) * rTop);
                    float slope = (rBottom - rTop) / Mathf.Max(0.0001f, height);
                    Vector3 n0 = new Vector3(Mathf.Cos(a0), slope, Mathf.Sin(a0)).normalized;
                    Vector3 n1 = new Vector3(Mathf.Cos(a1), slope, Mathf.Sin(a1)).normalized;
                    if (flat)
                    {
                        float am = (a0 + a1) * 0.5f;
                        n0 = n1 = new Vector3(Mathf.Cos(am), slope, Mathf.Sin(am)).normalized;
                    }
                    int i0 = v.Count;
                    v.Add(b0); v.Add(b1); v.Add(t0); v.Add(t1);
                    n.Add(n0); n.Add(n1); n.Add(n0); n.Add(n1);
                    float u0 = (float)j / segments * circumference, u1 = (float)(j + 1) / segments * circumference;
                    uv.Add(new Vector2(u0, 0)); uv.Add(new Vector2(u1, 0)); uv.Add(new Vector2(u0, height)); uv.Add(new Vector2(u1, height));
                    t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
                    t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 1);
                }
                if (caps)
                {
                    AddDisc(v, n, uv, t, height, rTop, segments, true);
                    AddDisc(v, n, uv, t, 0f, rBottom, segments, false);
                }
                return Finish(v, n, uv, t);
            });
        }

        static void AddDisc(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, float y, float r, int segments, bool up)
        {
            if (r <= 0.0001f) return;
            int center = v.Count;
            v.Add(new Vector3(0, y, 0)); n.Add(up ? Vector3.up : Vector3.down); uv.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= segments; j++)
            {
                float a = (float)j / segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                n.Add(up ? Vector3.up : Vector3.down);
                uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int j = 0; j < segments; j++)
            {
                if (up) { t.Add(center); t.Add(center + j + 2); t.Add(center + j + 1); }
                else { t.Add(center); t.Add(center + j + 1); t.Add(center + j + 2); }
            }
        }

        /// <summary>Box centred on the origin. UVs are in meters so tiling textures stay consistent.</summary>
        public static Mesh Box(Vector3 size)
        {
            string key = $"box_{size.x:F3}_{size.y:F3}_{size.z:F3}";
            return Cached(key, () => TaperedBoxMesh(size, new Vector2(size.x, size.z), 0f, true));
        }

        /// <summary>Box from y = 0 to y = height whose top face is scaled to topSize (x, z).</summary>
        public static Mesh TaperedBox(Vector2 bottomSize, Vector2 topSize, float height)
        {
            string key = $"tbox_{bottomSize.x:F3}_{bottomSize.y:F3}_{topSize.x:F3}_{topSize.y:F3}_{height:F3}";
            return Cached(key, () => TaperedBoxMesh(new Vector3(bottomSize.x, height, bottomSize.y), topSize, 0f, false));
        }

        static Mesh TaperedBoxMesh(Vector3 size, Vector2 top, float unused, bool centered)
        {
            float hx = size.x * 0.5f, hz = size.z * 0.5f, tx = top.x * 0.5f, tz = top.y * 0.5f;
            float y0 = centered ? -size.y * 0.5f : 0f, y1 = centered ? size.y * 0.5f : size.y;
            Vector3[] c =
            {
                new Vector3(-hx, y0, -hz), new Vector3(hx, y0, -hz), new Vector3(hx, y0, hz), new Vector3(-hx, y0, hz),
                new Vector3(-tx, y1, -tz), new Vector3(tx, y1, -tz), new Vector3(tx, y1, tz), new Vector3(-tx, y1, tz),
            };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            // Corners are passed bottom-left, top-left, top-right, bottom-right as seen from outside,
            // which gives Unity's clockwise front-face winding.
            void Face(int bl, int tl, int tr, int br, float w, float h)
            {
                int i = v.Count;
                v.Add(c[bl]); v.Add(c[tl]); v.Add(c[tr]); v.Add(c[br]);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, h)); uv.Add(new Vector2(w, h)); uv.Add(new Vector2(w, 0));
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            Face(0, 4, 5, 1, size.x, size.y);   // -Z
            Face(1, 5, 6, 2, size.z, size.y);   // +X
            Face(2, 6, 7, 3, size.x, size.y);   // +Z
            Face(3, 7, 4, 0, size.z, size.y);   // -X
            Face(4, 7, 6, 5, size.x, size.z);   // top
            Face(1, 2, 3, 0, size.x, size.z);   // bottom
            return Finish(v, null, uv, t, true);
        }

        public static Mesh Torus(float major, float minor, int majorSegments = 32, int minorSegments = 8)
        {
            string key = $"torus_{major:F3}_{minor:F3}_{majorSegments}_{minorSegments}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                for (int i = 0; i <= majorSegments; i++)
                {
                    float a = (float)i / majorSegments * Mathf.PI * 2f;
                    Vector3 center = new Vector3(Mathf.Cos(a) * major, 0, Mathf.Sin(a) * major);
                    Vector3 outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    for (int j = 0; j <= minorSegments; j++)
                    {
                        float b = (float)j / minorSegments * Mathf.PI * 2f;
                        Vector3 dir = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        v.Add(center + dir * minor);
                        n.Add(dir);
                        uv.Add(new Vector2((float)i / majorSegments, (float)j / minorSegments));
                    }
                }
                int stride = minorSegments + 1;
                for (int i = 0; i < majorSegments; i++)
                    for (int j = 0; j < minorSegments; j++)
                    {
                        int a = i * stride + j, b = a + stride, c = a + 1, d = b + 1;
                        t.Add(a); t.Add(c); t.Add(d);
                        t.Add(a); t.Add(d); t.Add(b);
                    }
                return Finish(v, n, uv, t);
            });
        }

        /// <summary>Open vision cone: apex at the origin, opening along +Z. UV.y runs 0 (apex) to 1 (far end).</summary>
        public static Mesh Cone(float length, float halfAngleDeg, int segments = 16, float aspect = 1f)
        {
            string key = $"cone_{length:F2}_{halfAngleDeg:F1}_{segments}_{aspect:F2}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                const int steps = 8;
                float tan = Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
                for (int k = 0; k <= steps; k++)
                {
                    float z = length * k / steps;
                    float r = z * tan;
                    for (int j = 0; j <= segments; j++)
                    {
                        float a = (float)j / segments * Mathf.PI * 2f + (segments == 4 ? Mathf.PI * 0.25f : 0f);
                        float cx = Mathf.Cos(a), sy = Mathf.Sin(a);
                        v.Add(new Vector3(cx * r * aspect, sy * r, z));
                        n.Add(new Vector3(cx, sy, -tan).normalized);
                        uv.Add(new Vector2((float)j / segments, (float)k / steps));
                    }
                }
                int stride = segments + 1;
                for (int k = 0; k < steps; k++)
                    for (int j = 0; j < segments; j++)
                    {
                        int a = k * stride + j, b = a + 1, c = a + stride, d = c + 1;
                        t.Add(a); t.Add(d); t.Add(c);
                        t.Add(a); t.Add(b); t.Add(d);
                    }
                return Finish(v, n, uv, t);
            });
        }

        /// <summary>Flat ring on the XZ plane facing up. UV.x runs around, UV.y from inner (0) to outer (1).</summary>
        public static Mesh Annulus(float inner, float outer, int segments = 48, float fromDeg = 0f, float toDeg = 360f)
        {
            string key = $"ring_{inner:F3}_{outer:F3}_{segments}_{fromDeg:F0}_{toDeg:F0}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                for (int j = 0; j <= segments; j++)
                {
                    float a = Mathf.Lerp(fromDeg, toDeg, (float)j / segments) * Mathf.Deg2Rad;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(d * inner); v.Add(d * outer);
                    n.Add(Vector3.up); n.Add(Vector3.up);
                    uv.Add(new Vector2((float)j / segments, 0)); uv.Add(new Vector2((float)j / segments, 1));
                }
                for (int j = 0; j < segments; j++)
                {
                    int i0 = j * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3;
                    t.Add(i0); t.Add(o1); t.Add(o0);
                    t.Add(i0); t.Add(i1); t.Add(o1);
                }
                return Finish(v, n, uv, t);
            });
        }

        /// <summary>Vertical quad on the XY plane, bottom edge on y = 0, centred on x. UV 0..1.</summary>
        public static Mesh Quad(float width, float height)
        {
            string key = $"quad_{width:F3}_{height:F3}";
            return Cached(key, () =>
            {
                var v = new List<Vector3> { new Vector3(-width * 0.5f, 0, 0), new Vector3(width * 0.5f, 0, 0), new Vector3(width * 0.5f, height, 0), new Vector3(-width * 0.5f, height, 0) };
                var n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                var t = new List<int> { 0, 2, 1, 0, 3, 2 };
                return Finish(v, n, uv, t);
            });
        }

        /// <summary>Vertical beam quad rising from y = 0. UV.x runs along the beam, UV.y across it (Expo/Beam layout).</summary>
        public static Mesh BeamQuad(float width, float length)
        {
            string key = $"beamquad_{width:F3}_{length:F3}";
            return Cached(key, () =>
            {
                var v = new List<Vector3> { new Vector3(-width * 0.5f, 0, 0), new Vector3(width * 0.5f, 0, 0), new Vector3(width * 0.5f, length, 0), new Vector3(-width * 0.5f, length, 0) };
                var n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                var t = new List<int> { 0, 2, 1, 0, 3, 2 };
                return Finish(v, n, uv, t);
            });
        }

        /// <summary>Horizontal quad on the XZ plane facing up, centred. UV in meters when metric is true.</summary>
        public static Mesh FloorQuad(float width, float depth, bool metric = true)
        {
            string key = $"fquad_{width:F3}_{depth:F3}_{metric}";
            return Cached(key, () =>
            {
                float hx = width * 0.5f, hz = depth * 0.5f;
                float u = metric ? width : 1f, w = metric ? depth : 1f;
                var v = new List<Vector3> { new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz) };
                var n = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(0, w), new Vector2(u, w), new Vector2(u, 0) };
                var t = new List<int> { 0, 1, 2, 0, 2, 3 };
                return Finish(v, n, uv, t);
            });
        }

        /// <summary>A curved beam following an arc in the XY plane (around +Z), depth along Z. Used for arches.</summary>
        public static Mesh ArcBeam(float radius, float width, float depth, float fromDeg, float toDeg, int segments = 16)
        {
            string key = $"arc_{radius:F3}_{width:F3}_{depth:F3}_{fromDeg:F0}_{toDeg:F0}_{segments}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                float rIn = radius - width * 0.5f, rOut = radius + width * 0.5f, hz = depth * 0.5f;
                for (int j = 0; j <= segments; j++)
                {
                    float a = Mathf.Lerp(fromDeg, toDeg, (float)j / segments) * Mathf.Deg2Rad;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    v.Add(new Vector3(d.x * rIn, d.y * rIn, -hz));
                    v.Add(new Vector3(d.x * rOut, d.y * rOut, -hz));
                    v.Add(new Vector3(d.x * rOut, d.y * rOut, hz));
                    v.Add(new Vector3(d.x * rIn, d.y * rIn, hz));
                    float u = (float)j / segments * radius * Mathf.Abs(toDeg - fromDeg) * Mathf.Deg2Rad;
                    uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, width)); uv.Add(new Vector2(u, width + depth)); uv.Add(new Vector2(u, width * 2 + depth));
                }
                for (int j = 0; j < segments; j++)
                {
                    int b = j * 4, nx = b + 4;
                    // front (-Z), outer, back (+Z), inner
                    Quad4(t, b + 0, b + 1, nx + 1, nx + 0);
                    Quad4(t, b + 1, b + 2, nx + 2, nx + 1);
                    Quad4(t, b + 2, b + 3, nx + 3, nx + 2);
                    Quad4(t, b + 3, b + 0, nx + 0, nx + 3);
                }
                return Finish(v, null, uv, t, true);
            });
        }

        static void Quad4(List<int> t, int a, int b, int c, int d)
        {
            t.Add(a); t.Add(c); t.Add(b);
            t.Add(a); t.Add(d); t.Add(c);
        }

        /// <summary>Lightweight lattice dome: latitude rings plus meridian ribs, as one mesh.</summary>
        public static Mesh LatticeDome(float radius, float beam, int meridians, int parallels)
        {
            string key = $"dome_{radius:F2}_{beam:F3}_{meridians}_{parallels}";
            return Cached(key, () =>
            {
                var combine = new List<CombineInstance>();
                // Meridian ribs (half arcs from the base to the crown).
                var rib = ArcBeam(radius, beam, beam, 0f, 90f, 24);
                for (int i = 0; i < meridians; i++)
                {
                    float yaw = 360f * i / meridians;
                    combine.Add(new CombineInstance { mesh = rib, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, yaw, 0), Vector3.one) });
                }
                // Parallel rings.
                for (int k = 1; k <= parallels; k++)
                {
                    float lat = 90f * k / (parallels + 1);
                    float r = Mathf.Cos(lat * Mathf.Deg2Rad) * radius;
                    float y = Mathf.Sin(lat * Mathf.Deg2Rad) * radius;
                    var ring = Torus(r, beam * 0.5f, Mathf.Max(24, meridians * 2), 6);
                    combine.Add(new CombineInstance { mesh = ring, transform = Matrix4x4.Translate(new Vector3(0, y, 0)) });
                }
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(combine.ToArray(), true, true);
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        /// <summary>Combine several (mesh, matrix) pairs into one cached mesh.</summary>
        public static Mesh Combine(string key, IList<(Mesh mesh, Matrix4x4 matrix)> parts)
        {
            return Cached("combo_" + key, () =>
            {
                var ci = new CombineInstance[parts.Count];
                for (int i = 0; i < parts.Count; i++) ci[i] = new CombineInstance { mesh = parts[i].mesh, transform = parts[i].matrix };
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(ci, true, true);
                mesh.RecalculateBounds();
                return mesh;
            });
        }
    }
}
