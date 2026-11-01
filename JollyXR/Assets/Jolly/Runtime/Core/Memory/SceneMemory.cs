using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Diagnostics;
using UnityEngine;

namespace Jolly.Core.Memory
{
    /// <summary>
    /// Seen objects (LRU, most recent first) and taught places (architecture §8). Only anchored entries survive a reload:
    /// raw positions are stale after a recenter. Object ids come from a persisted counter and are never reused. An anchor
    /// whose entry is replaced, evicted or cleared is erased from the device in the background.
    /// </summary>
    public sealed class SceneMemory : ISceneMemory
    {
        readonly string _filePath;
        readonly ISpatialAnchors _anchors;
        readonly IClock _clock;
        readonly MemoryLimits _limits;
        readonly ILog _log;
        readonly List<SeenObject> _objects = new();
        readonly List<PlaceRecord> _places = new();
        int _lastObjectNumber;

        public SceneMemory(string filePath, ISpatialAnchors anchors, IClock clock, MemoryLimits limits, ILog log)
        {
            _filePath = filePath;
            _anchors = anchors;
            _clock = clock;
            _limits = limits;
            _log = log;
        }

        public IReadOnlyList<SeenObject> Objects => _objects;
        public IReadOnlyList<PlaceRecord> Places => _places;
        public EncodedImage LastCapture { get; set; }

        public SeenObject AddObject(string label, Vector3 worldPosition, string summary, string anchorId = null)
        {
            var seen = new SeenObject
            {
                Id = "obj_" + ++_lastObjectNumber,
                Label = PromptText.Sanitize(label, _limits.LabelChars),
                WorldPosition = worldPosition,
                AnchorId = anchorId,
                SeenAt = _clock.UtcNow,
                Summary = PromptText.Sanitize(summary, _limits.ObjectSummaryChars),
            };
            _objects.Insert(0, seen);
            if (_objects.Count > _limits.SceneObjects)
            {
                EraseAnchor(_objects[^1].AnchorId);
                _objects.RemoveAt(_objects.Count - 1);
            }
            return seen;
        }

        public bool TryGetObject(string id, out SeenObject obj)
        {
            var index = _objects.FindIndex(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
            obj = index >= 0 ? _objects[index] : null;
            if (index > 0)
            {
                _objects.RemoveAt(index);
                _objects.Insert(0, obj);
            }
            return obj != null;
        }

        public void RememberPlace(PlaceRecord place)
        {
            var name = NormalizeName(place.Name);
            var index = _places.FindIndex(p => p.Name == name);
            if (index >= 0)
            {
                if (_places[index].AnchorId != place.AnchorId)
                    EraseAnchor(_places[index].AnchorId);
                _places.RemoveAt(index);
            }
            _places.Add(new PlaceRecord
            {
                Name = name, Pose = place.Pose, AnchorId = place.AnchorId,
                CreatedAt = place.CreatedAt == default ? _clock.UtcNow : place.CreatedAt,
            });
            if (_places.Count > _limits.Places)
            {
                var oldest = Oldest();
                EraseAnchor(oldest.AnchorId);
                _places.Remove(oldest);
            }
        }

        public bool TryGetPlace(string name, out PlaceRecord place)
        {
            var normalized = NormalizeName(name);
            place = _places.Find(p => p.Name == normalized);
            return place != null;
        }

        public string BuildPromptContext(int maxChars) => SceneContext.Build(_objects, _places, _clock.UtcNow, maxChars);

        public async UniTask Load(CancellationToken ct)
        {
            ResetInMemory();
            if (!AtomicFile.TryReadAllText(_filePath, out var json))
                return;
            var objects = new List<SeenObjectDto>();
            var places = new List<PlaceDto>();
            try
            {
                _lastObjectNumber = SceneFile.Read(json, objects, places);
            }
            catch (Exception e) when (StoreErrors.IsCorruptFile(e))
            {
                _log.Write(LogLevel.Warning, "memory", "evt=memory.load_failed store=scene", e);
                return;
            }

            foreach (var dto in places)
            {
                if (await Resolve(dto.anchor, ct) is not { } pose)
                    continue;
                var place = dto.ToRecord(pose);
                place.Name = NormalizeName(place.Name);
                _places.Add(place);
            }
            foreach (var dto in objects)
            {
                if (await Resolve(dto.anchor, ct) is { } pose)
                    _objects.Add(dto.ToObject(pose.position));
            }
        }

        public UniTask Save(CancellationToken ct)
        {
            var objects = new List<SeenObjectDto>(_objects.Count);
            foreach (var o in _objects)
                objects.Add(SeenObjectDto.From(o));
            var places = new List<PlaceDto>(_places.Count);
            foreach (var p in _places)
                places.Add(PlaceDto.From(p));
            AtomicFile.WriteAllText(_filePath, SceneFile.Write(_lastObjectNumber, objects, places));
            return UniTask.CompletedTask;
        }

        public void Clear()
        {
            foreach (var o in _objects)
                EraseAnchor(o.AnchorId);
            foreach (var p in _places)
                EraseAnchor(p.AnchorId);
            ResetInMemory();
            AtomicFile.Delete(_filePath);
        }

        void ResetInMemory()
        {
            _objects.Clear();
            _places.Clear();
            LastCapture = null;
        }

        void EraseAnchor(string anchorId)
        {
            if (!string.IsNullOrEmpty(anchorId))
                EraseInBackground(anchorId).Forget();
        }

        async UniTaskVoid EraseInBackground(string anchorId)
        {
            try
            {
                await _anchors.Erase(anchorId, CancellationToken.None);
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Warning, "memory", "evt=memory.anchor_erase_failed", e);
            }
        }

        UniTask<Pose?> Resolve(string anchorId, CancellationToken ct) =>
            string.IsNullOrEmpty(anchorId) ? UniTask.FromResult<Pose?>(null) : _anchors.TryResolve(anchorId, ct);

        PlaceRecord Oldest()
        {
            var oldest = _places[0];
            foreach (var p in _places)
            {
                if (p.CreatedAt < oldest.CreatedAt)
                    oldest = p;
            }
            return oldest;
        }

        // Place names come from model tool arguments and are echoed into the context block, so they get the label rules.
        string NormalizeName(string name) => PromptText.Sanitize(name, _limits.LabelChars).ToLowerInvariant();
    }
}
