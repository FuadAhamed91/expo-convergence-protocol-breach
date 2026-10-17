using TMPro;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>Small helpers for assembling props and architecture from generated meshes.</summary>
    public static class Kit
    {
        public static MeshRenderer Mesh(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return mr;
        }

        /// <summary>Static box with a collider, positioned by its centre in the parent's space.</summary>
        public static MeshRenderer Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float yaw = 0f, bool collider = true)
        {
            var mr = Mesh(parent, name, MeshGen.Box(size), mat, center, Vector3.one, Quaternion.Euler(0, yaw, 0));
            if (collider)
            {
                var bc = mr.gameObject.AddComponent<BoxCollider>();
                bc.size = size;
            }
            mr.gameObject.isStatic = true;
            return mr;
        }

        /// <summary>Static mesh with a convex or box collider fitted to its bounds.</summary>
        public static MeshRenderer Solid(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, bool collider = true)
        {
            var mr = Mesh(parent, name, mesh, mat, pos, Vector3.one, rot);
            if (collider)
            {
                var bc = mr.gameObject.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size;
            }
            mr.gameObject.isStatic = true;
            return mr;
        }

        /// <summary>World-space holographic text (TextMeshPro).</summary>
        public static TextMeshPro Text(Transform parent, string text, Vector3 localPos, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text_" + (text.Length > 12 ? text.Substring(0, 12) : text));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            var font = ExpoFonts.Main;
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size * 10f;
            tmp.color = color;
            tmp.alignment = align;
            tmp.characterSpacing = 6f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(40f, 4f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tmp;
        }

        public static Light PointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        public static Light SpotLight(Transform parent, Vector3 localPos, Quaternion rot, Color color, float range, float angle, float intensity)
        {
            var go = new GameObject("Spot");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.5f;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        public static Transform Group(Transform parent, string name, Vector3 pos, float yaw = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }
    }

    /// <summary>Helpers for Blender-made FBX models dropped into Resources/Models.</summary>
    public static class ExpoModelUtil
    {
        /// <summary>Maps imported material slots to game materials by name ("Accent"/"Glow" and "Dark").</summary>
        public static void ApplyMaterials(GameObject root, Material primary, Material secondary, Material accent)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    if (n.Contains("accent") || n.Contains("glow") || n.Contains("emiss") || n.Contains("lens")) mats[i] = accent;
                    else if (n.Contains("dark") || n.Contains("secondary") || n.Contains("frame")) mats[i] = secondary;
                    else mats[i] = primary;
                }
                r.sharedMaterials = mats;
            }
        }

        public static Renderer FindRenderer(GameObject root, string nameContains)
        {
            nameContains = nameContains.ToLowerInvariant();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name.ToLowerInvariant().Contains(nameContains)) return r;
            return null;
        }
    }
}
