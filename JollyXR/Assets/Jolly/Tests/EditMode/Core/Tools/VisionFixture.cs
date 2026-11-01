using System;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Memory;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;

namespace Jolly.Tests.EditMode.Core.Tools
{
    // Same-named fakes from other WPs also live in Jolly.Testing; these tests are written against this WP's.
    using FakeLocalizer = Jolly.Testing.Conversation.FakeLocalizer;
    using FakeSpatialAnchors = Jolly.Testing.Conversation.FakeSpatialAnchors;
    using FakeWorldQuery = Jolly.Testing.Conversation.FakeWorldQuery;

    /// <summary>Camera at the origin looking down +Z at a wall 2 m away; a 64×64 image with f = 64 px.</summary>
    sealed class VisionFixture : IDisposable
    {
        public readonly FakeClock Clock = new();
        public readonly RecordingLog Log = new();
        public readonly FakeCameraFrameSource Camera = new();
        public readonly FakeUserPose User = new();
        public readonly FakeWorldQuery World = new() { WallZ = 2f };
        public readonly ScriptedSegmenter Segmenter = new();
        public readonly ScriptedObjectIdentifier Identifier = new();
        public readonly RecordingMaskPresenter Presenter = new();
        public readonly RecordingCharacterController Character = new();
        public readonly FakeLocalizer Localizer = new();
        public readonly VisionSettings Settings = new();
        public readonly SceneMemory Scene;
        public readonly VisionToolkit Kit;

        public VisionFixture()
        {
            Scene = new SceneMemory("unused", new FakeSpatialAnchors(), Clock, new MemoryLimits(), Log);
            var projection = new PinholeProjection();
            var frames = new FrameGrabber(Camera, new FakeImageEncoder(), Clock, Settings);
            Kit = new VisionToolkit(frames, new SegmentPromptPlanner(User, World, projection, Settings),
                new MaskGeometry(World, projection, Settings), Segmenter, Presenter, Clock, Log, Settings);
        }

        public CaptureAndIdentifyTool Capture() => new(Kit, Identifier, Character, Scene, Localizer);
        public FindObjectTool Find() => new(Kit, Character, Scene, User);
        public HighlightPartTool Highlight() => new(Kit, Character, Scene);

        public EncodedImage PreCapture() => Sync.Run(Kit.Frames.Capture(System.Threading.CancellationToken.None));

        public void Dispose() => Camera.Dispose();
    }
}
