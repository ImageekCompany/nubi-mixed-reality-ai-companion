using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;

namespace Jolly.Core.Tools
{
    /// <summary>Starts a walk to the floor beside a piece of furniture, looking at it so Nubi ends up facing it.</summary>
    static class SurfaceWalk
    {
        public static bool TryStart(ICharacterController character, IWorldQuery world, SceneSurface surface,
                                    LocomotionSettings settings, out float etaSeconds)
        {
            etaSeconds = 0f;
            character.LookAt(LookTarget.At(SurfaceTargets.LookPoint(surface)));
            var beside = SurfaceTargets.Approach(surface, character.Position, settings.SurfaceClearance);
            return world.TrySampleWalkable(beside, settings.SampleRadius, out var walkable)
                   && character.TryStartMoveTo(walkable, settings.SurfaceStopDistance, out etaSeconds);
        }
    }
}
