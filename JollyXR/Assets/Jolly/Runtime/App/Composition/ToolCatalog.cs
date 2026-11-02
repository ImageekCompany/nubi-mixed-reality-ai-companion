using Jolly.App.Adapters;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Imaging;
using Jolly.Core.Tools;

namespace Jolly.App.Composition
{
    /// <summary>Registers the whole tool catalogue of contracts §4.2 against the real adapters, then the active
    /// character's actions (ADR-0047): the character is chosen and constructed before this runs.</summary>
    public static class ToolCatalog
    {
        public static ToolRegistry Build(Foundation f, JollyConfig config, CloudServices cloud, XrServices xr, ICharacterController character,
                                         ISceneMemory scene, IPersonaProvider personas, ILocalizer localizer,
                                         IMaskPresenter masks, IRoomScan room, out FrameGrabber frames)
        {
            var vision = new VisionSettings
            {
                MinMeanLuma = config.visionMinMeanLuma, SegmenterModel = config.samModel, VisionModel = config.chatModel,
            };
            var locomotion = new LocomotionSettings();
            var projection = new CameraMathProjection();
            frames = new FrameGrabber(xr.Camera, new JpegImageEncoder(), f.Clock, vision);
            var kit = new VisionToolkit(frames, new SegmentPromptPlanner(xr.User, xr.World, projection, vision),
                new MaskGeometry(xr.World, projection, vision), cloud.Segmenter, masks, f.Clock, f.Log, vision, f.Events);

            var registry = new ToolRegistry();
            registry.Register(new WalkToTool(character, scene, xr.World, locomotion));
            registry.Register(new FollowUserTool(character, locomotion));
            registry.Register(new ComeHereTool(character, xr.User, xr.World, locomotion));
            registry.Register(new LookAtTool(character, xr.User, xr.World, scene, locomotion));
            registry.Register(new CaptureAndIdentifyTool(kit, cloud.Identifier, character, scene, localizer));
            registry.Register(new FindObjectTool(kit, character, scene, xr.User));
            registry.Register(new HighlightPartTool(kit, character, scene));
            registry.Register(new RememberPlaceTool(scene, xr.Anchors, xr.User, character, f.Clock, f.Log));
            registry.Register(new StopTool(character));
            registry.Register(new PlayGestureTool(character));
            registry.Register(new SetPersonaTool(personas));
            registry.Register(new GoToSurfaceTool(character, xr.World, room, locomotion));
            registry.Register(new RequestRoomScanTool(room, localizer));
            registry.Register(new SetHomeLocationTool());

            var actions = CharacterActionCatalog.For(character, xr.User, xr.World, locomotion, f.Log);
            actions.RegisterInto(registry);
            f.Log.Write(LogLevel.Info, "tools", $"evt=actions.offered count={actions.Tools.Count} tools={ActionNames(actions)}");
            return registry;
        }

        static string ActionNames(CharacterActionCatalog actions)
        {
            var names = new string[actions.Tools.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = actions.Tools[i].Definition.Name;
            return string.Join(",", names);
        }
    }
}
