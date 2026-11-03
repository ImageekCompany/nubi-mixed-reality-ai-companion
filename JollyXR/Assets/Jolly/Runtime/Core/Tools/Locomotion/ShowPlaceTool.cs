using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Leads the way to something remembered and points at it (ADR-0051), resolving the name like walk_to: a taught place,
    /// furniture from the room scan, or a seen object. The walk is the answer, so the tool waits for it, says the line
    /// and ends the turn with no closing model round.
    /// </summary>
    public sealed class ShowPlaceTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"place\":{\"type\":\"string\",\"maxLength\":64," +
            "\"description\":\"Taught place name, seen object id or label (obj_3, mug), or furniture type\"}}," +
            "\"required\":[\"place\"],\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly ISceneMemory _scene;
        readonly IWorldQuery _world;
        readonly ILocalizer _localizer;
        readonly PointToPlace _pointer;
        readonly List<SceneSurface> _surfaces = new();

        public ShowPlaceTool(ICharacterController character, ISceneMemory scene, IWorldQuery world, ILocalizer localizer,
                             PointToPlace pointer)
        {
            _character = character;
            _scene = scene;
            _world = world;
            _localizer = localizer;
            _pointer = pointer;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.ShowPlace,
            "Walk toward a place the user showed you earlier or a piece of furniture, and point at it. " +
            "Call it when they ask where a place is or to lead them there, e.g. 'where is the kitchen?', 'show me the desk', " +
            "'take me to the door'. For an object they are looking for, like 'where is my mug?', use find_object instead. " +
            "You say 'There!' by yourself on arrival.",
            Schema, TimeSpan.FromSeconds(1) + PointToPlace.WalkBudget, ToolConcurrency.Exclusive);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("place", out var place);
            if (!TryResolve(place, out var target))
                return ToolResult.Failure("unknown_place", place);
            var pointed = await _pointer.Run(target, ct);
            var output = new ToolJson().Add("status", pointed ? "pointing" : "stopped").Add("place", place).ToString();
            return new ToolResult(true, output, pointed ? CoreLines.Get(_localizer, CoreLines.PlaceThere) : null, endsTurn: true);
        }

        bool TryResolve(string place, out Vector3 target)
        {
            if (_scene.TryGetPlace(place, out var taught))
            {
                target = taught.Pose.position;
                return true;
            }
            _world.GetSurfaces(_surfaces);
            if (SurfaceTargets.TryNearest(_surfaces, FurnitureLabels.Normalize(place), _character.Position, out var surface))
            {
                target = SurfaceTargets.LookPoint(surface);
                return true;
            }
            if (SeenObjectLookup.TryFind(_scene, place, out var seen))
            {
                target = seen.WorldPosition;
                return true;
            }
            target = default;
            return false;
        }
    }
}
