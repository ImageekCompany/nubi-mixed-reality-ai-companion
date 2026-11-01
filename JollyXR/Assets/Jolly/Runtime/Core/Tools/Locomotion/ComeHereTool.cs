using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>Walks to 1 m in front of the user's head, projected to Nubi's floor, then faces the user (architecture §5.5).</summary>
    public sealed class ComeHereTool : ITool
    {
        readonly ICharacterController _character;
        readonly IUserPoseProvider _user;
        readonly IWorldQuery _world;
        readonly LocomotionSettings _settings;

        public ComeHereTool(ICharacterController character, IUserPoseProvider user, IWorldQuery world, LocomotionSettings settings)
        {
            _character = character;
            _user = user;
            _world = world;
            _settings = settings;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.ComeHere, "Walk to the user and face them.",
            ToolSchemas.Empty, TimeSpan.FromSeconds(3), ToolConcurrency.Locomotion);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            var head = _user.Head;
            var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
            forward = forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;
            var target = head.position + forward * _settings.ComeHereDistance;
            target.y = _character.Position.y;

            _character.LookAt(LookTarget.User);
            var output = new ToolJson();
            if (_world.TrySampleWalkable(target, _settings.SampleRadius, out var walkable)
                && _character.TryStartMoveTo(walkable, _settings.ComeHereStopDistance, out var eta))
                output.Add("status", "walking").Add("eta_s", Mathf.CeilToInt(eta));
            else
                output.Add("status", "unreachable");
            return UniTask.FromResult(ToolResult.Success(output.ToString()));
        }
    }
}
