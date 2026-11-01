using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Tools
{
    /// <summary>Head and eyes toward the user, the surface the user points at, or a seen object.</summary>
    public sealed class LookAtTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"target\":{\"type\":\"string\",\"maxLength\":32," +
            "\"description\":\"'user', 'pointed', or an object id like obj_3\"}},\"required\":[\"target\"],\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly IUserPoseProvider _user;
        readonly IWorldQuery _world;
        readonly ISceneMemory _scene;
        readonly LocomotionSettings _settings;

        public LookAtTool(ICharacterController character, IUserPoseProvider user, IWorldQuery world, ISceneMemory scene,
                          LocomotionSettings settings)
        {
            _character = character;
            _user = user;
            _world = world;
            _scene = scene;
            _settings = settings;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.LookAt,
            "Turn Nubi's head and eyes toward the user, the object the user is pointing at, or a seen object id.",
            Schema, TimeSpan.FromSeconds(1), ToolConcurrency.Parallel);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("target", out var target);
            var result = Look((target ?? string.Empty).Trim());
            return UniTask.FromResult(result);
        }

        ToolResult Look(string target)
        {
            if (string.Equals(target, "user", StringComparison.OrdinalIgnoreCase))
            {
                _character.LookAt(LookTarget.User);
            }
            else if (string.Equals(target, "pointed", StringComparison.OrdinalIgnoreCase))
            {
                if (!_user.TryGetPointingRay(out var ray))
                    return ToolResult.Failure("no_pointing");
                var point = _world.TryRaycast(ray, _settings.LookRayMaxDistance, out var hit) ? hit : ray.GetPoint(2f);
                _character.LookAt(LookTarget.At(point));
            }
            else if (_scene.TryGetObject(target, out var seen))
            {
                _character.LookAt(LookTarget.At(seen.WorldPosition));
            }
            else
            {
                return ToolResult.Failure("unknown_object", target);
            }
            return ToolResult.Success(new ToolJson().Add("status", "looking").Add("target", target).ToString());
        }
    }
}
