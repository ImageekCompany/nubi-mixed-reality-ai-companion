using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Remembers a named place: "there", where the user's finger or else gaze lands (ADR-0051), or "here", their head
    /// projected to Nubi's floor; facing the user's heading, anchored when the platform persists anchors (contracts §4.2).
    /// The anchor save gets less than the tool's timeout, so a slow one still leaves time to remember the place unanchored.
    /// </summary>
    public sealed class RememberPlaceTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"maxLength\":32}," +
            "\"where\":{\"type\":\"string\",\"enum\":[\"there\",\"here\"]," +
            "\"description\":\"'there': what the user is showing, pointing at or looking at. 'here': the spot they stand on.\"}}," +
            "\"required\":[\"name\",\"where\"],\"additionalProperties\":false}";

        static readonly TimeSpan AnchorTimeout = TimeSpan.FromSeconds(3);

        readonly ISceneMemory _scene;
        readonly ISpatialAnchors _anchors;
        readonly IUserPoseProvider _user;
        readonly ICharacterController _character;
        readonly IClock _clock;
        readonly ILog _log;
        readonly IWorldQuery _world;
        readonly LocomotionSettings _settings;

        public RememberPlaceTool(ISceneMemory scene, ISpatialAnchors anchors, IUserPoseProvider user,
                                 ICharacterController character, IClock clock, ILog log, IWorldQuery world,
                                 LocomotionSettings settings)
        {
            _world = world;
            _settings = settings;
            _scene = scene;
            _anchors = anchors;
            _user = user;
            _character = character;
            _clock = clock;
            _log = log;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.RememberPlace,
            "Remember a place under a name so you can walk there or show it later. Call it in this turn whenever the user " +
            "names or points out a place, even in passing and without asking you to remember it: 'this is the kitchen', " +
            "'that over there is the kitchen', 'my desk is there', 'remember this spot'. Words alone do not remember it.",
            Schema, TimeSpan.FromSeconds(5), ToolConcurrency.Parallel);

        /// <summary>Nubi turns to it as well: that is the acknowledgement the user sees.</summary>
        Vector3 Shown(Pose head)
        {
            var ray = _user.TryGetPointingRay(out var pointing) ? pointing : new Ray(head.position, head.rotation * Vector3.forward);
            var point = _world.TryRaycast(ray, _settings.ShownPlaceRayMaxDistance, out var hit)
                ? hit
                : ray.GetPoint(_settings.ShownPlaceFallbackDistance);
            _character.LookAt(LookTarget.At(point));
            return point;
        }

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("name", out var name);
            var head = _user.Head;
            var position = args.TryGetString("where", out var where) && where == "there"
                ? Shown(head)
                : new Vector3(head.position.x, _character.Position.y, head.position.z);
            var heading = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
            var pose = new Pose(position, heading.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(heading));

            string anchorId = null;
            using (var scope = new TimeoutScope(_clock, AnchorTimeout, ct))
            {
                try
                {
                    anchorId = await _anchors.CreateAndSave(pose, scope.Token);
                }
                catch (OperationCanceledException) when (scope.TimedOut && !ct.IsCancellationRequested)
                {
                    _log.Write(LogLevel.Warning, "tools", "evt=anchor.save_timeout");
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _log.Write(LogLevel.Warning, "tools", "evt=anchor.save_failed", e);
                }
            }

            _scene.RememberPlace(new PlaceRecord { Name = name, Pose = pose, AnchorId = anchorId, CreatedAt = _clock.UtcNow });
            _scene.TryGetPlace(name, out var saved);
            return ToolResult.Success(new ToolJson().Add("status", "saved").Add("name", saved?.Name ?? name)
                                                    .Add("anchored", anchorId != null).ToString());
        }
    }
}
