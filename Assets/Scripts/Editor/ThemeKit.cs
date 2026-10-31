using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using DiceThrower.Game;
using DiceThrower.UI;
using Object = UnityEngine.Object;

namespace DiceThrower.EditorTools
{
    internal enum SurfaceBlend { Opaque, Alpha, Additive }

    /// <summary>
    /// Shared pieces of the theme builders (DvarvenTavernBuilder, CultistHideoutBuilder, CathedralBuilder, ...): folders, textures, materials,
    /// primitives, colliders, generated meshes, the ThemeScene settings and RegisterTheme. A builder only decides what its scene contains.
    /// Everything is create-or-update in place, so references to the assets stay valid when a builder runs again.
    /// </summary>
    internal static class ThemeKit
    {
        public const string LitShader = "Universal Render Pipeline/Lit";
        public const string UnlitShader = "Universal Render Pipeline/Unlit";

        // ------------------------------------------------------------------ folders and textures

        /// <summary>Creates the folders (and their parents) that do not exist yet.</summary>
        public static void EnsureFolders(params string[] dirs)
        {
            foreach (string dir in dirs)
            {
                string[] parts = dir.Split('/');
                string path = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    path += "/" + parts[i];
                    BuilderAssets.EnsureFolder(path);
                }
            }
        }

        /// <summary>Loads a texture, and tells where to put the theme art when it is missing.</summary>
        public static Texture2D Tex(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogError("Theme art missing: " + path + " (copy the theme's textures into the project, see Tools/ThemeArt).");
            return tex;
        }

        /// <summary>Wrap mode of a 3D texture: clamp for pictures that must not bleed over their edge, repeat for tiles.</summary>
        public static void SetupTexture(string path, bool clamp)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            TextureWrapMode wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (importer.wrapMode == wrap && importer.mipmapEnabled) return;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        /// <summary>Crisp UI textures: no mip maps, no compression, transparent edges stay transparent.</summary>
        public static void SetupUiTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ materials

        private static Material LoadOrCreate(string path, string shaderName)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        private static void Set(Material m, string property, float value)
        {
            if (m.HasProperty(property)) m.SetFloat(property, value);
        }

        /// <summary>URP Lit material. Tiling repeats the texture across the mesh.</summary>
        public static Material Mat(string dir, string name, Color color, Texture tex, Vector2 tiling, float smoothness, float metallic)
        {
            Material m = LoadOrCreate(dir + "/" + name + ".mat", LitShader);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>URP Unlit material, opaque or blended (alpha, or additive for light). Glowing things and light rays use it.</summary>
        public static Material Unlit(string dir, string name, Color color, Texture tex, SurfaceBlend blend, bool doubleSided)
        {
            Material m = LoadOrCreate(dir + "/" + name + ".mat", UnlitShader);
            bool transparent = blend != SurfaceBlend.Opaque;
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            Set(m, "_Surface", transparent ? 1f : 0f);
            Set(m, "_Blend", blend == SurfaceBlend.Additive ? 2f : 0f);
            Set(m, "_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            Set(m, "_DstBlend", blend == SurfaceBlend.Additive ? (float)BlendMode.One : transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            Set(m, "_SrcBlendAlpha", (float)BlendMode.One);
            Set(m, "_DstBlendAlpha", blend == SurfaceBlend.Additive ? (float)BlendMode.One : transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            Set(m, "_ZWrite", transparent ? 0f : 1f);
            Set(m, "_Cull", doubleSided ? 0f : 2f);
            if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            m.SetShaderPassEnabled("ShadowCaster", !transparent);
            m.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Both faces of a Lit material render (a tube whose winding does not matter).</summary>
        public static Material DoubleSided(Material m)
        {
            Set(m, "_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The material shines in its own colour times <paramref name="strength"/>.</summary>
        public static void Emit(Material m, Color color, float strength)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * strength);
            EditorUtility.SetDirty(m);
        }

        public static PhysicsMaterial Physics(string path, float bounciness, float dynamicFriction, float staticFriction)
        {
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial(System.IO.Path.GetFileNameWithoutExtension(path));
                AssetDatabase.CreateAsset(pm, path);
            }
            pm.bounciness = bounciness;
            pm.dynamicFriction = dynamicFriction;
            pm.staticFriction = staticFriction;
            pm.bounceCombine = PhysicsMaterialCombine.Average;
            pm.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        // ------------------------------------------------------------------ objects and colliders

        /// <summary>A built-in primitive with a material. The collider stays only when <paramref name="keepCollider"/> is set.</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A mesh asset on an object of its own (no collider).</summary>
        public static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A flat quad lying on the ground (facing up; texture u = x, v = z), turned by <paramref name="yaw"/> degrees.</summary>
        public static GameObject FlatQuad(Transform parent, string name, Vector3 pos, Vector2 size, float yaw, Material mat)
        {
            GameObject go = Prim(PrimitiveType.Quad, parent, name, pos, new Vector3(size.x, size.y, 1f), mat, false);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            return go;
        }

        public static void Wall(Transform parent, string name, Vector3 pos, Vector3 size, PhysicsMaterial mat)
        {
            var go = new GameObject("Wall" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = mat;
        }

        /// <summary>An invisible, tall ring of box colliders whose inner face lies on the circle of <paramref name="innerRadius"/>.</summary>
        public static void RingWalls(Transform parent, float innerRadius, float thickness, float height, int segments, PhysicsMaterial mat)
        {
            float mid = innerRadius + thickness * 0.5f;
            float chord = 2f * (innerRadius + thickness) * Mathf.Tan(Mathf.PI / segments) * 1.02f; // covers the gap at the outer corners
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var go = new GameObject("Wall" + i);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(Mathf.Sin(a) * mid, height * 0.5f, Mathf.Cos(a) * mid);
                go.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(chord, height, thickness);
                box.sharedMaterial = mat;
            }
        }

        /// <summary>
        /// The invisible part of a round play surface: the surface marker (scale = diameter), a ground slab whose top is y = 0
        /// where dice land, a ring of walls at the play radius and a ceiling. Returns the surface marker.
        /// </summary>
        public static Transform RoundSurface(GameObject root, RoundSurfaceStyle st, PhysicsMaterial phys)
        {
            var surface = new GameObject("Surface").transform;
            surface.SetParent(root.transform, false);
            surface.localScale = new Vector3(st.outerDiameter, 1f, st.outerDiameter); // only its scale matters: the size the camera fits

            float span = st.outerDiameter + 2f * st.wallThickness;
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -st.groundThickness * 0.5f, 0f);
            var slab = ground.AddComponent<BoxCollider>();
            slab.size = new Vector3(span, st.groundThickness, span);
            slab.sharedMaterial = phys;

            Transform walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            RingWalls(walls, st.PlayRadius, st.wallThickness, st.wallHeight, st.wallSegments, phys);
            Wall(walls, "Ceiling", new Vector3(0f, st.wallHeight, 0f), new Vector3(span, 0.5f, span), phys);
            return surface;
        }

        /// <summary>
        /// The rectangular play surface of a table theme: the surface marker (scale = outer size), the slab dice land on, the drawn top, the visible
        /// rim, invisible walls on the rim's inner face and a ceiling. Returns the surface marker; <paramref name="table"/> is the parent of the visible parts.
        /// </summary>
        public static Transform TableShell(GameObject root, TableSurfaceStyle st, PhysicsMaterial phys, Material bodyMat, Material topMat, Material rimMat, out Transform table)
        {
            Vector2 size = st.outerSize;
            float rw = st.rimWidth, rh = st.rimHeight, th = st.topThickness;

            var surface = new GameObject("Surface").transform;
            surface.SetParent(root.transform, false);
            surface.localScale = new Vector3(size.x, 1f, size.y); // only its scale matters: the outer size the camera fits

            table = new GameObject("Table").transform;
            table.SetParent(root.transform, false);
            GameObject body = Prim(PrimitiveType.Cube, table, "Body", new Vector3(0f, -th * 0.5f, 0f), new Vector3(size.x, th, size.y), bodyMat, true);
            body.GetComponent<Collider>().sharedMaterial = phys;

            Vector2 inner = new Vector2(size.x - 2f * rw, size.y - 2f * rw);
            FlatQuad(table, "Top", new Vector3(0f, 0.002f, 0f), inner, 0f, topMat);

            float sx = size.x * 0.5f - rw * 0.5f, sz = size.y * 0.5f - rw * 0.5f;
            Prim(PrimitiveType.Cube, table, "RimLeft", new Vector3(-sx, rh * 0.5f, 0f), new Vector3(rw, rh, size.y), rimMat, false);
            Prim(PrimitiveType.Cube, table, "RimRight", new Vector3(sx, rh * 0.5f, 0f), new Vector3(rw, rh, size.y), rimMat, false);
            Prim(PrimitiveType.Cube, table, "RimNear", new Vector3(0f, rh * 0.5f, -sz), new Vector3(inner.x, rh, rw), rimMat, false);
            Prim(PrimitiveType.Cube, table, "RimFar", new Vector3(0f, rh * 0.5f, sz), new Vector3(inner.x, rh, rw), rimMat, false);

            // the walls rise to the ceiling, so no die flies out over the low rim (the rim itself is static: dice bounce off these colliders)
            Transform walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            Wall(walls, "Left", new Vector3(-sx, st.wallHeight * 0.5f, 0f), new Vector3(rw, st.wallHeight, size.y), phys);
            Wall(walls, "Right", new Vector3(sx, st.wallHeight * 0.5f, 0f), new Vector3(rw, st.wallHeight, size.y), phys);
            Wall(walls, "Near", new Vector3(0f, st.wallHeight * 0.5f, -sz), new Vector3(inner.x, st.wallHeight, rw), phys);
            Wall(walls, "Far", new Vector3(0f, st.wallHeight * 0.5f, sz), new Vector3(inner.x, st.wallHeight, rw), phys);
            Wall(walls, "Ceiling", new Vector3(0f, st.wallHeight, 0f), new Vector3(size.x, 0.5f, size.y), phys);
            return surface;
        }

        public static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, bool flicker)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            if (flicker) go.AddComponent<LightFlicker>();
            return light;
        }

        /// <summary>Adds a <see cref="ColorPulse"/> that swells the colour of the given renderers between a and b.</summary>
        public static void AddPulse(GameObject host, Renderer[] targets, Color a, Color b, float speed)
        {
            var pulse = host.AddComponent<ColorPulse>();
            var so = new SerializedObject(pulse);
            SerializedProperty list = so.FindProperty("targets");
            list.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            so.FindProperty("colorA").colorValue = a;
            so.FindProperty("colorB").colorValue = b;
            so.FindProperty("speed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Adds a <see cref="Drift"/> that sways the object slowly back and forth.</summary>
        public static void AddDrift(GameObject host, Vector3 amplitude, float speed)
        {
            var drift = host.AddComponent<Drift>();
            var so = new SerializedObject(drift);
            so.FindProperty("amplitude").vector3Value = amplitude;
            so.FindProperty("speed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ generated meshes (saved as assets, so the prefab keeps them)

        /// <summary>A flat disc facing up, radius r; the texture's inscribed circle maps onto it (u = x, v = z).</summary>
        public static Mesh DiscMesh(float radius, int segments)
        {
            var verts = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i + 1] = new Vector3(c * radius, 0f, s * radius);
                uv[i + 1] = new Vector2(0.5f + 0.5f * c, 0.5f + 0.5f * s);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % segments + 1;
                tris[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Disc" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A tapering tube along a path (a tentacle, a root). Both faces are drawn by the material, so the winding is free.</summary>
        public static Mesh TubeMesh(Vector3[] path, float radiusStart, float radiusEnd, int sides)
        {
            int n = path.Length, ring = sides + 1;
            var verts = new Vector3[n * ring];
            var normals = new Vector3[n * ring];
            var uv = new Vector2[n * ring];
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(tangent, Vector3.up);
                if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(tangent, Vector3.right);
                side.Normalize();
                Vector3 up = Vector3.Cross(side, tangent);
                float r = Mathf.Lerp(radiusStart, radiusEnd, i / (n - 1f));
                for (int s = 0; s < ring; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    Vector3 dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    verts[i * ring + s] = path[i] + dir * r;
                    normals[i * ring + s] = dir;
                    uv[i * ring + s] = new Vector2(s / (float)sides, i / (n - 1f));
                }
            }
            for (int i = 0; i < n - 1; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = i * ring + s, b = a + 1, c = a + ring, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            var mesh = new Mesh { name = "Tube" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A raised ring (an annulus with a top and both side walls) from <paramref name="inner"/> to <paramref name="outer"/>; the texture repeats <paramref name="repeat"/> times round it.</summary>
        public static Mesh RingMesh(float inner, float outer, float height, int segments, float repeat)
        {
            // profile, seen from the centre: inner foot, inner top, outer top, outer foot
            var profile = new[] { new Vector2(inner, 0f), new Vector2(inner, height), new Vector2(outer, height), new Vector2(outer, 0f) };
            int columns = segments + 1;
            var verts = new Vector3[columns * profile.Length];
            var uv = new Vector2[verts.Length];
            var tris = new List<int>();
            for (int i = 0; i < columns; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                for (int p = 0; p < profile.Length; p++)
                {
                    verts[i * profile.Length + p] = new Vector3(Mathf.Sin(a) * profile[p].x, profile[p].y, Mathf.Cos(a) * profile[p].x);
                    uv[i * profile.Length + p] = new Vector2(repeat * i / segments, p / (profile.Length - 1f));
                }
            }
            for (int i = 0; i < segments; i++)
            {
                for (int p = 0; p < profile.Length - 1; p++)
                {
                    int a = i * profile.Length + p, b = a + profile.Length;
                    tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b }); // clockwise seen from outside: the top faces up
                }
            }
            var mesh = new Mesh { name = "Ring" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Many small spheres on a circle merged into one mesh (a ring of light dots), one draw call.</summary>
        public static Mesh RingOfSpheres(float ringRadius, int count, float size, float y)
        {
            Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var parts = new CombineInstance[count];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                parts[i].mesh = sphere;
                parts[i].transform = Matrix4x4.TRS(new Vector3(Mathf.Sin(a) * ringRadius, y, Mathf.Cos(a) * ringRadius), Quaternion.identity, Vector3.one * size);
            }
            var mesh = new Mesh { name = "RingOfSpheres" };
            mesh.CombineMeshes(parts, true, true);
            return mesh;
        }

        /// <summary>Saves a generated mesh as an asset; an existing asset is overwritten in place so references stay valid.</summary>
        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path); // Unity warns when the main object's name differs from the file name
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ------------------------------------------------------------------ scene settings, prefab, registration

        /// <summary>Writes the surface marker, the rim height and the light, camera and ambient settings into the ThemeScene.</summary>
        public static void ConfigureScene(ThemeScene scene, Transform surface, float rimHeight, bool round,
                                          Color lightColor, float lightIntensity, Vector3 lightEuler, Color cameraBackground, Color ambient)
        {
            var so = new SerializedObject(scene);
            so.FindProperty("surface").objectReferenceValue = surface;
            so.FindProperty("rimHeight").floatValue = rimHeight;
            so.FindProperty("round").boolValue = round;
            so.FindProperty("lightColor").colorValue = lightColor;
            so.FindProperty("lightIntensity").floatValue = lightIntensity;
            so.FindProperty("lightEuler").vector3Value = lightEuler;
            so.FindProperty("cameraBackground").colorValue = cameraBackground;
            so.FindProperty("ambientColor").colorValue = ambient;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Saves the built scene as a prefab (an existing prefab is replaced in place) and removes the temporary scene object.</summary>
        public static ThemeScene SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ThemeScene>();
        }

        /// <summary>Adds the theme to the DataUiConfig, or updates its entry. The other themes and their order stay as they are.</summary>
        public static void RegisterTheme(string themeName, string sheetPath, ThemeScene scene)
        {
            var config = AssetDatabase.LoadAssetAtPath<DataUiConfig>(BuilderPaths.DataUiConfig);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(sheetPath);
            if (config == null || sheet == null)
            {
                Debug.LogError(themeName + ": " + BuilderPaths.DataUiConfig + " or " + sheetPath + " not found; the theme was not registered.");
                return;
            }

            var list = new List<ThemeSpec>(config.themes ?? new ThemeSpec[0]);
            ThemeSpec spec = list.Find(t => t.name == themeName);
            if (spec == null)
            {
                spec = new ThemeSpec { name = themeName };
                list.Add(spec);
            }
            spec.sheet = sheet;
            spec.scene = scene;
            config.themes = list.ToArray();
            EditorUtility.SetDirty(config);
        }

        /// <summary>Puts the themes in the given order and drops every other entry (the removed Tavern and Moonlight themes). A name with no entry is skipped.</summary>
        public static void SetThemeOrder(params string[] names)
        {
            var config = AssetDatabase.LoadAssetAtPath<DataUiConfig>(BuilderPaths.DataUiConfig);
            if (config == null) return;
            var ordered = new List<ThemeSpec>();
            foreach (string name in names)
            {
                ThemeSpec spec = new List<ThemeSpec>(config.themes ?? new ThemeSpec[0]).Find(t => t.name == name);
                if (spec != null) ordered.Add(spec);
                else Debug.LogWarning("DiceThrower: theme '" + name + "' is not registered; build it first.");
            }
            config.themes = ordered.ToArray();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The last step of every builder: save, refresh and say what was built.</summary>
        public static void Finish(string themeName, string prefabPath)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceThrower: " + themeName + " built (" + prefabPath + ") and added to the themes. Press Play and pick it in Settings > Theme.");
        }
    }
}
