using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Jolly.XR.Occlusion
{
    /// <summary>
    /// The occlusion fallback (scope "Should 3", ADR-0023): one depth-only box per Space Setup bounding box, drawn before
    /// Nubi with no colour write, so Nubi's pixels behind furniture fail the depth test and the passthrough shows through
    /// the transparent eye buffer. Planes are not used: the boxes already cover tables and couches, the floor would clip
    /// Nubi's feet, and Nubi never walks behind a wall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneOccluders : MonoBehaviour
    {
        [SerializeField] ARBoundingBoxManager boxes;
        [Tooltip("Jolly/XR/DepthOccluder: ZWrite on, ColorMask 0, queue before Nubi.")]
        [SerializeField] Material occluderMaterial;

        readonly Dictionary<TrackableId, MeshRenderer> _occluders = new();
        Mesh _cube;
        bool _visible;

        public int Count => _occluders.Count;

        /// <summary>Off by default; <see cref="RealWorldOcclusion"/> turns it on when depth is unavailable.</summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value)
                    return;
                _visible = value;
                foreach (var occluder in _occluders.Values)
                    occluder.enabled = value;
            }
        }

        void OnEnable()
        {
            if (boxes == null)
                return;
            boxes.trackablesChanged.AddListener(OnBoxesChanged);
            foreach (var box in boxes.trackables)
                Upsert(box);
        }

        void OnDisable()
        {
            if (boxes != null)
                boxes.trackablesChanged.RemoveListener(OnBoxesChanged);
        }

        void OnDestroy()
        {
            foreach (var occluder in _occluders.Values)
            {
                if (occluder != null)
                    Destroy(occluder.gameObject);
            }
            _occluders.Clear();
            if (_cube != null)
                Destroy(_cube);
        }

        void OnBoxesChanged(ARTrackablesChangedEventArgs<ARBoundingBox> args)
        {
            foreach (var box in args.added)
                Upsert(box);
            foreach (var box in args.updated)
                Upsert(box);
            foreach (var removed in args.removed)
                Remove(removed.Key);
        }

        /// <summary>Adds or resizes the occluder of one box. Public for tests, which have no bounding-box provider.</summary>
        public void Upsert(TrackableId id, Transform box, Vector3 size)
        {
            if (occluderMaterial == null)
                return;
            if (!_occluders.TryGetValue(id, out var occluder) || occluder == null)
            {
                occluder = Create(box);
                _occluders[id] = occluder;
            }
            occluder.transform.localScale = size;
        }

        public void Remove(TrackableId id)
        {
            if (!_occluders.Remove(id, out var occluder))
                return;
            if (occluder != null)
                Destroy(occluder.gameObject);
        }

        void Upsert(ARBoundingBox box) => Upsert(box.trackableId, box.transform, box.size);

        MeshRenderer Create(Transform box)
        {
            if (_cube == null)
                _cube = UnitCube.Create();
            var go = new GameObject("SceneOccluder");
            go.transform.SetParent(box, false);
            go.AddComponent<MeshFilter>().sharedMesh = _cube;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = occluderMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = _visible;
            return renderer;
        }
    }
}
