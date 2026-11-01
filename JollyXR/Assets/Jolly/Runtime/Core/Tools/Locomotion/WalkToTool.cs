using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Walks to a taught place, a furniture surface or a seen object, in that order (architecture §5.5). Returns once the
    /// motion has started; the walk runs on the controller's own token and survives barge-in.
    /// </summary>
    public sealed class WalkToTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"place\":{\"type\":\"string\",\"maxLength\":64," +
            "\"description\":\"Place name, furniture type, or object id like obj_3\"}},\"required\":[\"place\"],\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly ISceneMemory _scene;
        readonly IWorldQuery _world;
        readonly LocomotionSettings _settings;

        public WalkToTool(ICharacterController character, ISceneMemory scene, IWorldQuery world, LocomotionSettings settings)
        {
            _character = character;
            _scene = scene;
            _world = world;
            _settings = settings;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.WalkTo,
            "Walk Nubi to a named place the user taught, a furniture type (table, couch, bed, storage, screen) or a seen object id.",
            Schema, TimeSpan.FromSeconds(3), ToolConcurrency.Locomotion);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("place", out var place);
            if (!TryResolve(place, out var target))
                return UniTask.FromResult(ToolResult.Failure("unknown_place", place));

            var output = new ToolJson();
            if (_world.TrySampleWalkable(target, _settings.SampleRadius, out var walkable)
                && _character.TryStartMoveTo(walkable, _settings.WalkStopDistance, out var eta))
            {
                output.Add("status", "walking").Add("place", place).Add("eta_s", Mathf.CeilToInt(eta));
            }
            else
            {
                _character.LookAt(LookTarget.At(target));
                output.Add("status", "unreachable").Add("place", place);
            }
            return UniTask.FromResult(ToolResult.Success(output.ToString()));
        }

        bool TryResolve(string place, out Vector3 target)
        {
            if (_scene.TryGetPlace(place, out var taught))
            {
                target = taught.Pose.position;
                return true;
            }
            if (_world.TryFindLabeledSurface(FurnitureLabels.Normalize(place), _character.Position, out target))
                return true;
            if (_scene.TryGetObject(place, out var seen))
            {
                target = seen.WorldPosition;
                return true;
            }
            return false;
        }
    }
}
