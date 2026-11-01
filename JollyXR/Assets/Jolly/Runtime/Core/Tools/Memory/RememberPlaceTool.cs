using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Remembers "here" as a named place: the user's head projected to Nubi's floor, facing the user's heading, anchored
    /// when the platform persists anchors (contracts §4.2). The anchor save gets less than the tool's timeout, so a slow
    /// one still leaves time to remember the place unanchored.
    /// </summary>
    public sealed class RememberPlaceTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"maxLength\":32}},\"required\":[\"name\"],\"additionalProperties\":false}";

        static readonly TimeSpan AnchorTimeout = TimeSpan.FromSeconds(3);

        readonly ISceneMemory _scene;
        readonly ISpatialAnchors _anchors;
        readonly IUserPoseProvider _user;
        readonly ICharacterController _character;
        readonly IClock _clock;
        readonly ILog _log;

        public RememberPlaceTool(ISceneMemory scene, ISpatialAnchors anchors, IUserPoseProvider user,
                                 ICharacterController character, IClock clock, ILog log)
        {
            _scene = scene;
            _anchors = anchors;
            _user = user;
            _character = character;
            _clock = clock;
            _log = log;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.RememberPlace,
            "Remember a location under a name, e.g. 'sofa', so Nubi can walk there later.",
            Schema, TimeSpan.FromSeconds(5), ToolConcurrency.Parallel);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("name", out var name);
            var head = _user.Head;
            var position = new Vector3(head.position.x, _character.Position.y, head.position.z);
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
