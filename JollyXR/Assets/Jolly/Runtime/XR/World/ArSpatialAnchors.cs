using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Jolly.XR.World
{
    /// <summary>
    /// Persistent anchors for taught places (remember_place / walk_to). The id is the persistent GUID returned by
    /// ARAnchorManager.TrySaveAnchorAsync; on meta-openxr it equals the anchor's trackableId and survives sessions within
    /// one Space Setup. Anchors loaded this session are cached so walk_to resolves them without another load.
    /// </summary>
    public sealed class ArSpatialAnchors : ISpatialAnchors
    {
        readonly ARAnchorManager _anchors;
        readonly ILog _log;
        readonly Dictionary<string, ARAnchor> _loaded = new(StringComparer.Ordinal);

        public ArSpatialAnchors(ARAnchorManager anchors, ILog log)
        {
            _anchors = anchors;
            _log = log;
        }

        public async UniTask<string> CreateAndSave(Pose pose, CancellationToken ct)
        {
            if (!SupportsPersistence(out var descriptor) || !descriptor.supportsSaveAnchor)
                return null;

            // Cancellation is honoured only between steps that leave nothing behind: an added anchor is removed, and a
            // saved one is returned, since its GUID is the only handle to what is now persisted.
            var added = await _anchors.TryAddAnchorAsync(pose);
            if (!added.status.IsSuccess())
            {
                ct.ThrowIfCancellationRequested();
                _log.Write(LogLevel.Warning, "xr", $"evt=anchor.add_failed status={added.status.statusCode}");
                return null;
            }
            if (ct.IsCancellationRequested)
            {
                _anchors.TryRemoveAnchor(added.value);
                ct.ThrowIfCancellationRequested();
            }

            var saved = await _anchors.TrySaveAnchorAsync(added.value, ct);
            if (!saved.status.IsSuccess())
            {
                _anchors.TryRemoveAnchor(added.value);
                ct.ThrowIfCancellationRequested();
                _log.Write(LogLevel.Warning, "xr", $"evt=anchor.save_failed status={saved.status.statusCode}");
                return null;
            }

            var id = saved.value.guid.ToString("N");
            _loaded[id] = added.value;
            return id;
        }

        public async UniTask<Pose?> TryResolve(string anchorId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(anchorId) || !Guid.TryParseExact(anchorId, "N", out var guid))
                return null;
            if (_loaded.TryGetValue(anchorId, out var cached) && cached != null)
                return PoseOf(cached);
            if (!SupportsPersistence(out var descriptor) || !descriptor.supportsLoadAnchor)
                return null;

            var loaded = await _anchors.TryLoadAnchorAsync(new SerializableGuid(guid), ct);
            if (!loaded.status.IsSuccess())
            {
                ct.ThrowIfCancellationRequested();
                return null;
            }
            _loaded[anchorId] = loaded.value;
            return PoseOf(loaded.value);
        }

        bool SupportsPersistence(out XRAnchorSubsystemDescriptor descriptor)
        {
            descriptor = _anchors != null && _anchors.isActiveAndEnabled ? _anchors.descriptor : null;
            return descriptor != null;
        }

        static Pose PoseOf(ARAnchor anchor) => new(anchor.transform.position, anchor.transform.rotation);
    }
}
