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
    /// Walks to the nearest piece of real furniture with the label and stops beside it, facing it (ADR-0025). Perching on
    /// it is not offered: the NavMesh never bakes furniture as walkable and the rig has no climb or sit clips.
    /// </summary>
    public sealed class GoToSurfaceTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"label\":{\"type\":\"string\"," +
            "\"enum\":[\"couch\",\"table\",\"desk\",\"bed\",\"chair\",\"storage\",\"screen\"]," +
            "\"description\":\"Furniture from the room scan\"}},\"required\":[\"label\"],\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly IWorldQuery _world;
        readonly IRoomScan _room;
        readonly LocomotionSettings _settings;
        readonly List<SceneSurface> _surfaces = new();

        public GoToSurfaceTool(ICharacterController character, IWorldQuery world, IRoomScan room, LocomotionSettings settings)
        {
            _character = character;
            _world = world;
            _room = room;
            _settings = settings;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.GoToSurface,
            "Walk to a piece of the user's real furniture from the room scan, e.g. the couch or the table, " +
            "and stop beside it facing it.",
            Schema, TimeSpan.FromSeconds(3), ToolConcurrency.Locomotion);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("label", out var requested);
            return UniTask.FromResult(Go(FurnitureLabels.Normalize(requested)));
        }

        ToolResult Go(string label)
        {
            if (_room.Status != RoomScanStatus.Scanned)
                return ToolResult.Failure("room_not_scanned");
            _world.GetSurfaces(_surfaces);
            if (!SurfaceTargets.TryNearest(_surfaces, label, _character.Position, out var surface))
                return ToolResult.Failure("no_surface", label);

            var output = new ToolJson();
            if (SurfaceWalk.TryStart(_character, _world, surface, _settings, out var eta))
                output.Add("status", "walking").Add("label", label).Add("eta_s", Mathf.CeilToInt(eta));
            else
                output.Add("status", "unreachable").Add("label", label);
            return ToolResult.Success(output.ToString());
        }
    }
}
