#if UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Editor.Character;
using Jolly.Testing;
using Jolly.XR.Navigation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// A 10 × 10 m floor with a disconnected 3 × 3 m island at x = 20, baked by the production NavMeshBaker, and the real Nubi prefab
    /// constructed with fakes. Time runs at <see cref="TimeScale"/>× so walks take a fraction of real time. Furniture added
    /// with <see cref="AddFurniture"/> is baked as Not Walkable, like a Space Setup bounding box. Without the floor
    /// (<see cref="Create"/> floor: false) the test brings its own NavMesh.
    /// </summary>
    sealed class CharacterTestRig : IDisposable
    {
        public const float TimeScale = 4f;
        public static readonly Vector3 Island = new(20f, 0f, 0f);

        readonly NavMeshBaker _navMesh = new();
        readonly GameObject _floor;

        CharacterTestRig(ILipSyncDriver lipSync, bool floor)
        {
            Time.timeScale = TimeScale;
            _floor = floor ? BuildFloor() : null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NubiAssetPaths.Prefab);
            Assert(prefab != null, $"missing {NubiAssetPaths.Prefab}");
            Nubi = Object.Instantiate(prefab).GetComponent<NubiCharacterController>();
            LipSync = lipSync;
            Nubi.Construct(User, lipSync, Clock, new FakeRandom());
        }

        public NubiCharacterController Nubi { get; }
        public FakeClock Clock { get; } = new();
        public StubUserPose User { get; } = new();
        public ILipSyncDriver LipSync { get; }
        public Animator Animator => Nubi.Animator;
        public NavMeshBaker NavMesh => _navMesh;

        /// <summary>A box standing on the floor, baked as an obstacle; rebakes at once.</summary>
        public void AddFurniture(Vector3 floorCenter, Vector3 size)
        {
            var box = new GameObject("Furniture");
            box.transform.SetParent(_floor.transform, false);
            box.transform.position = floorCenter + Vector3.up * (size.y * 0.5f);
            var collider = box.AddComponent<BoxCollider>();
            collider.size = size;
            Physics.SyncTransforms();
            _navMesh.AddObstacle(collider);
            _navMesh.Rebuild(0);
        }

        public static CharacterTestRig Create(ILipSyncDriver lipSync = null, bool floor = true) =>
            new(lipSync ?? new ScriptedLipSync(), floor);

        public void Dispose()
        {
            if (Nubi != null)
                Object.Destroy(Nubi.gameObject);
            _navMesh.Dispose();
            if (_floor != null)
                Object.Destroy(_floor);
            Time.timeScale = 1f;
        }

        /// <summary>Yields frames until the condition holds or the scaled-time budget runs out.</summary>
        public static async UniTask<bool> Until(Func<bool> condition, float scaledSeconds)
        {
            var end = Time.time + scaledSeconds;
            while (!condition())
            {
                if (Time.time > end)
                    return false;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            return true;
        }

        GameObject BuildFloor()
        {
            var root = new GameObject("TestFloor");
            Plane(root, Vector3.zero, 1f);
            Plane(root, Island, 0.3f);
            _navMesh.Rebuild(0);
            return root;
        }

        void Plane(GameObject parent, Vector3 position, float scale)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.transform.SetParent(parent.transform, false);
            plane.transform.localPosition = position;
            plane.transform.localScale = Vector3.one * scale;
            _navMesh.AddWalkable(plane.GetComponent<Collider>());
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
#endif
