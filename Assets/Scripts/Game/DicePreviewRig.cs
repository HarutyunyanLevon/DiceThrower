using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// One hidden camera and a few die "slots" far away from the tray. Each slot owns a render texture; <see cref="Render"/> draws the
    /// slot's die into it with the shared camera, one slot after the other. The Dice tab uses it for the live spinning d20 of the Attack
    /// and Damage sets, the sprite atlas tool for one still picture per die type. A plain class: the owner calls <see cref="Spin"/> and
    /// <see cref="Render"/> (only while the pictures are on screen) and <see cref="Dispose"/>.
    /// </summary>
    public sealed class DicePreviewRig : System.IDisposable
    {
        private sealed class Slot
        {
            public GameObject Object;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public RenderTexture Texture;
            public Mesh Mesh;
            public Quaternion Pose = Quaternion.identity;
            public DieKind Kind;
            public bool HasDie;
            public DiceSkin Skin;
            public DieFxDriver Fx;   // only for a DiceFX shader die
        }

        private static readonly int UiPreviewId = Shader.PropertyToID("_DiceFxUiPreview");

        private readonly DicePreviewConfig _cfg;
        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly Slot[] _slots;
        private float _yaw;

        /// <param name="textureSize">Pixels per side of every slot's render texture.</param>
        /// <param name="withLight">Adds a light of its own (the sprite tool, which cannot rely on the open scene). The live preview uses the scene's light.</param>
        /// <param name="stageOffset">Moves this rig's stage away from the config's stage. Two rigs alive at once (the Dice tab's slots and its grid) need different stages, or each camera would see the other's dice.</param>
        public DicePreviewRig(DicePreviewConfig cfg, int slotCount, int textureSize, bool withLight, Vector3 stageOffset = default(Vector3))
        {
            _cfg = cfg;
            _root = new GameObject("DicePreview") { hideFlags = HideFlags.HideAndDontSave };
            _root.transform.position = cfg.stagePosition + stageOffset;

            var cameraObject = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false; // drawn by hand, see Render
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent
            _camera.fieldOfView = cfg.fieldOfView;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 50f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.useOcclusionCulling = false;

            if (withLight)
            {
                var lightObject = new GameObject("Light") { hideFlags = HideFlags.HideAndDontSave };
                lightObject.transform.SetParent(_root.transform, false);
                lightObject.transform.rotation = Quaternion.Euler(cfg.lightEuler);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = cfg.lightIntensity;
                light.shadows = LightShadows.None;
            }

            _slots = new Slot[Mathf.Max(1, slotCount)];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = NewSlot(i, Mathf.Max(8, textureSize));
        }

        public int SlotCount { get { return _slots.Length; } }

        /// <summary>The picture of a slot. UI elements can show it as their background.</summary>
        public RenderTexture TextureOf(int slot)
        {
            return _slots[slot].Texture;
        }

        /// <summary>True when the slot shows this skin's model of the kind.</summary>
        public bool Shows(int slot, DiceSkin skin, DieKind kind)
        {
            Slot s = _slots[slot];
            return s.HasDie && s.Skin == skin && s.Kind == kind;
        }

        /// <summary>
        /// Puts the skin's model of <paramref name="kind"/> in the slot, in its still pose (see <see cref="DiePose"/>: the kind's classic silhouette, tilted a little).
        /// False (and an empty slot) when the skin has no model or the model has no such die.
        /// </summary>
        public bool SetDie(int slot, DiceSkin skin, DieKind kind)
        {
            Slot s = _slots[slot];
            DestroyObject(s.Mesh);
            s.Mesh = null;
            s.Filter.sharedMesh = null;
            s.HasDie = false;
            s.Skin = null;
            if (s.Fx != null) s.Fx.Unbind();
            s.Fx = null;

            ModelDie solid = skin != null && skin.Model != null ? skin.Model.Solid(kind) : null;
            if (solid == null) return false;

            s.Mesh = solid.NewMesh();
            s.Filter.sharedMesh = s.Mesh;
            Material material = skin.Model.MaterialOf(skin.Variant);
            s.Renderer.sharedMaterial = material;
            s.Fx = DieFxDriver.BindPreview(s.Object, s.Renderer, material, solid.Shape.Radius, skin.Effects, _root.transform, _camera);
            s.Pose = DiePose.Still(solid.Shape, _cfg.TiltOf(kind));
            s.Kind = kind;
            s.Skin = skin;
            s.HasDie = true;
            return true;
        }

        /// <summary>Turns every die around the vertical axis.</summary>
        public void Spin(float deltaSeconds)
        {
            _yaw = Mathf.Repeat(_yaw + deltaSeconds * _cfg.spinDegreesPerSecond, 360f);
        }

        /// <summary>The effect driver of a slot's die (null for a plain die): play a nat 20 / nat 1 effect with SetEffect.</summary>
        public DieFxDriver FxOf(int slot)
        {
            return _slots[slot].Fx;
        }

        /// <summary>Advances the shader dice's fades. Call every frame, also for slots that are not drawn this frame.</summary>
        public void TickEffects(float deltaSeconds)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Fx != null) _slots[i].Fx.Tick(deltaSeconds);
            }
        }

        /// <summary>Draws the slot's die into its texture (cleared to transparent when the slot is empty).</summary>
        public void Render(int slot)
        {
            Slot target = _slots[slot];
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                s.Object.SetActive(i == slot && s.HasDie);
                if (s.HasDie) s.Object.transform.rotation = Quaternion.Euler(0f, _yaw, 0f) * s.Pose;
                if (s.Fx != null) s.Fx.ShowExtras(i == slot && s.HasDie); // the extras (rays, cloud) of the other slots stay hidden
            }

            // while a rain cloud / rays are out, the picture is framed wider and lifted so they fit
            bool extras = target.Fx != null && target.Fx.ExtrasVisible;
            float radius = target.HasDie ? DieShape.RadiusOf(target.Kind) : 1f;
            float distance = radius * (extras ? _cfg.effectFramePadding : _cfg.framePadding) / Mathf.Sin(_cfg.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 lift = extras ? Vector3.up * (radius * _cfg.effectLiftRadii) : Vector3.zero;
            _camera.transform.SetPositionAndRotation(_root.transform.position + lift + Vector3.back * distance, Quaternion.identity); // looks along +z at the die
            _camera.targetTexture = target.Texture;
            Shader.SetGlobalFloat(UiPreviewId, 1f); // the additive rays then write straight-alpha colour (see DiceFX_Rays)
            _camera.Render();
            Shader.SetGlobalFloat(UiPreviewId, 0f);
            _camera.targetTexture = null;
        }

        /// <summary>The pixels of a slot's last picture, bottom row first. The die's rim keeps clean colours (see BleedEdges).</summary>
        public Color32[] Capture(int slot)
        {
            RenderTexture texture = _slots[slot].Texture;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
            Color32[] pixels = copy.GetPixels32();
            RenderTexture.active = previous;
            DestroyObject(copy);

            BleedEdges(pixels, texture.width, texture.height);
            return pixels;
        }

        public void Dispose()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                DestroyObject(_slots[i].Mesh);
                if (_slots[i].Texture != null) _slots[i].Texture.Release();
                DestroyObject(_slots[i].Texture);
            }
            DestroyObject(_root);
        }

        // ------------------------------------------------------------------ helpers

        private Slot NewSlot(int index, int size)
        {
            var s = new Slot();
            s.Object = new GameObject("Die" + index) { hideFlags = HideFlags.HideAndDontSave };
            s.Object.transform.SetParent(_root.transform, false);
            s.Filter = s.Object.AddComponent<MeshFilter>();
            s.Renderer = s.Object.AddComponent<MeshRenderer>();
            s.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.Renderer.receiveShadows = false;
            s.Object.SetActive(false);

            s.Texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = Mathf.Max(1, _cfg.antiAliasing),
                filterMode = FilterMode.Bilinear,
                name = "DicePreview" + index,
            };
            s.Texture.Create();
            return s;
        }

        /// <summary>
        /// The antialiased rim of the die blends with the transparent black background, which would show as a dark fringe on a light UI.
        /// Every partly transparent pixel takes the colour of an opaque neighbour instead and keeps its alpha.
        /// </summary>
        private static void BleedEdges(Color32[] pixels, int width, int height)
        {
            var source = (Color32[])pixels.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 p = source[y * width + x];
                    if (p.a == 0 || p.a == 255) continue;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                            Color32 n = source[ny * width + nx];
                            if (n.a != 255) continue;
                            pixels[y * width + x] = new Color32(n.r, n.g, n.b, p.a);
                            dy = 2; // found: leave both loops
                            break;
                        }
                    }
                }
            }
        }

        private static void DestroyObject(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
