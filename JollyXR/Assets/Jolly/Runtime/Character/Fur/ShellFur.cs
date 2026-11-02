using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Character
{
    /// <summary>
    /// The shells of one furry renderer (ADR-0045). The renderer's last material is the fur base layer on
    /// Jolly/Character/ShellFur; this appends copies of it with the shell keyword and rising heights. Unity draws every
    /// material past the last submesh on that submesh again, so the shells reuse the vertices the renderer skinned once:
    /// they follow the animation and the blendshapes for one extra draw each, with no extra skinning or mesh.
    /// The base material says how many shells to draw near, mid and far, and from which viewer distance; the shell
    /// materials for every level are made up front, so a level change only swaps the renderer's material array.
    /// Owns the shell materials: <see cref="Dispose"/> restores the renderer and destroys them.
    /// </summary>
    public sealed class ShellFur : IDisposable
    {
        public const string ShellKeyword = "_FUR_SHELL";
        /// <summary>AlphaTest: after every opaque, so a shell's clipped strands never block the skin's early depth.</summary>
        public const int ShellQueue = 2450;
        /// <summary>How far past a level's distance the viewer must go before it changes, m.</summary>
        public const float LodBand = 0.15f;

        static readonly int ShellHeightId = Shader.PropertyToID("_ShellHeight");
        static readonly int InertiaId = Shader.PropertyToID("_FurInertia");
        static readonly int LengthId = Shader.PropertyToID("_FurLength");
        static readonly int[] CountIds =
        {
            Shader.PropertyToID("_ShellCount"), Shader.PropertyToID("_ShellCountMid"), Shader.PropertyToID("_ShellCountFar"),
        };
        static readonly int MidFromId = Shader.PropertyToID("_ShellDistanceMid");
        static readonly int FarFromId = Shader.PropertyToID("_ShellDistanceFar");

        readonly Renderer _renderer;
        readonly Transform _anchor;
        readonly Material[] _own;
        readonly Material[][] _levels;
        readonly Material[][] _shellsByLevel;
        readonly float _midFrom;
        readonly float _farFrom;
        readonly FurInertia _inertia;
        bool _disposed;

        ShellFur(Renderer renderer, Material[] own, Material fur)
        {
            _renderer = renderer;
            _own = own;
            _anchor = renderer is SkinnedMeshRenderer { rootBone: not null } skinned ? skinned.rootBone : renderer.transform;
            _midFrom = fur.GetFloat(MidFromId);
            _farFrom = Mathf.Max(fur.GetFloat(FarFromId), _midFrom);
            // A few hertz and under-damped: a soft plush pile that bounces once and settles.
            _inertia = new FurInertia(2.5f, 0.45f, fur.GetFloat(LengthId));
            _levels = new Material[ShellFurLod.Levels][];
            _shellsByLevel = new Material[ShellFurLod.Levels][];
            for (var level = 0; level < ShellFurLod.Levels; level++)
            {
                var count = Mathf.Clamp(Mathf.RoundToInt(fur.GetFloat(CountIds[level])), 0, 64);
                var shells = new Material[count];
                for (var i = 0; i < count; i++)
                    shells[i] = CreateShell(fur, ShellFurLod.Height(i, count), count - 1 - i);
                _shellsByLevel[level] = shells;
                var all = new Material[own.Length + count];
                own.CopyTo(all, 0);
                // Outermost first: their surviving strands are already in the depth buffer when the inner ones draw.
                for (var i = 0; i < count; i++)
                    all[own.Length + i] = shells[count - 1 - i];
                _levels[level] = all;
            }
        }

        /// <summary>The LOD level drawn now (0 near), -1 before the first <see cref="Tick"/>.</summary>
        public int Level { get; private set; } = -1;
        public int ShellCount => Level < 0 ? 0 : _shellsByLevel[Level].Length;
        public Renderer Renderer => _renderer;

        /// <summary>A material on the shell-fur shader that is not itself a shell. Asks the shader's property list: in
        /// the editor a subshader tag read before the shader first compiled came back empty.</summary>
        public static bool IsFurBase(Material material) =>
            material != null && material.HasFloat(ShellHeightId) && !material.IsKeywordEnabled(ShellKeyword);

        /// <summary>Shells for every renderer under <paramref name="root"/> whose fur is on its last submesh.</summary>
        public static List<ShellFur> AttachAll(GameObject root)
        {
            var attached = new List<ShellFur>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (TryAttach(renderer, out var fur, out var reason))
                    attached.Add(fur);
                else if (reason != null)
                    Debug.LogWarning($"[ShellFur] {renderer.name}: {reason}");
            }
            return attached;
        }

        /// <summary>False with a null <paramref name="reason"/> when the renderer has no fur at all.</summary>
        public static bool TryAttach(Renderer renderer, out ShellFur fur, out string reason)
        {
            fur = null;
            reason = null;
            var own = renderer.sharedMaterials;
            var furSlot = Array.FindIndex(own, IsFurBase);
            if (furSlot < 0)
                return false;
            var subMeshes = SubMeshCount(renderer);
            if (own.Length != subMeshes)
                reason = $"{own.Length} materials on {subMeshes} submeshes; shells need exactly one per submesh";
            else if (furSlot != own.Length - 1)
                reason = $"the fur material is in slot {furSlot}; shells redraw only the last submesh ({own.Length - 1})";
            if (reason != null)
                return false;
            fur = new ShellFur(renderer, own, own[furSlot]);
            return true;
        }

        /// <summary>Picks the level for the viewer's distance and swings the tips with the body's motion.</summary>
        public void Tick(Vector3 viewer, float deltaTime)
        {
            if (_disposed)
                return;
            var level = ShellFurLod.Select(Vector3.Distance(viewer, _renderer.bounds.center), Level, _midFrom, _farFrom,
                                           LodBand);
            if (level != Level)
            {
                Level = level;
                _renderer.sharedMaterials = _levels[level];
            }
            var lag = _inertia.Step(_anchor.position, deltaTime);
            var shells = _shellsByLevel[level];
            for (var i = 0; i < shells.Length; i++)
                shells[i].SetVector(InertiaId, lag);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_renderer != null)
                _renderer.sharedMaterials = _own;
            foreach (var shells in _shellsByLevel)
            {
                foreach (var shell in shells)
                {
                    if (Application.isPlaying)
                        Object.Destroy(shell);
                    else
                        Object.DestroyImmediate(shell);
                }
            }
        }

        static Material CreateShell(Material fur, float height, int queueOffset)
        {
            var shell = new Material(fur) { name = $"{fur.name} shell {height:0.00}" };
            shell.EnableKeyword(ShellKeyword);
            shell.SetFloat(ShellHeightId, height);
            shell.renderQueue = ShellQueue + queueOffset;
            return shell;
        }

        static int SubMeshCount(Renderer renderer) => renderer switch
        {
            SkinnedMeshRenderer skinned => skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 0,
            MeshRenderer => renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null
                ? filter.sharedMesh.subMeshCount
                : 0,
            _ => 0,
        };
    }
}
