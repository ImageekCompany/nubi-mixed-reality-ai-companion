using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>"Where are my glasses?" (architecture §5.3.1): SAM text prompt on a fresh capture, every instance outlined,
    /// Nubi looks at the one nearest the user.</summary>
    public sealed class FindObjectTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"noun_phrase\":{\"type\":\"string\",\"maxLength\":40," +
            "\"description\":\"1-3 word noun phrase, e.g. 'reading glasses'\"}},\"required\":[\"noun_phrase\"],\"additionalProperties\":false}";

        readonly VisionToolkit _kit;
        readonly ICharacterController _character;
        readonly ISceneMemory _scene;
        readonly IUserPoseProvider _user;

        public FindObjectTool(VisionToolkit kit, ICharacterController character, ISceneMemory scene, IUserPoseProvider user)
        {
            _kit = kit;
            _character = character;
            _scene = scene;
            _user = user;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.FindObject,
            "Locate a named object the user is searching for ('where is my X', 'find my X'), even one you have seen " +
            "before: a fresh look that highlights every visible instance, e.g. 'glasses' or 'keys'.",
            Schema, TimeSpan.FromSeconds(8), ToolConcurrency.Exclusive);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("noun_phrase", out var phrase);
            var image = await _kit.TryCapture(ct);
            if (image == null)
                return ToolResult.Failure("camera_unavailable");
            _scene.LastCapture = image;

            var objects = await _kit.Segment(image, SegmentPrompt.ForText(phrase), ct);
            if (objects.Count == 0)
                return ToolResult.Success(new ToolJson().Add("found", 0).ToString());
            if (objects.Count > _kit.Settings.MaxInstances)
                objects = Take(objects, _kit.Settings.MaxInstances);

            var head = _user.Head.position;
            var nearestWorld = Vector3.zero;
            var nearestDistance = float.MaxValue;
            var nearestRange = 0f;
            foreach (var o in objects)
            {
                var range = _kit.Geometry.Locate(o, image, out var world);
                var fromUser = Vector3.Distance(head, world);
                if (fromUser >= nearestDistance)
                    continue;
                nearestDistance = fromUser;
                nearestWorld = world;
                nearestRange = range;
            }

            _kit.Present(objects, image, nearestRange);
            _character.LookAt(LookTarget.At(nearestWorld));
            var nearest = _scene.AddObject(phrase, nearestWorld, "found on request");
            return ToolResult.Success(new ToolJson().Add("found", objects.Count).Add("nearest_id", nearest.Id)
                                                    .Add("distance_m", nearestDistance).ToString());
        }

        static IReadOnlyList<SegmentedObject> Take(IReadOnlyList<SegmentedObject> objects, int count)
        {
            var taken = new List<SegmentedObject>(count);
            for (var i = 0; i < count; i++)
                taken.Add(objects[i]);
            return taken;
        }
    }
}
