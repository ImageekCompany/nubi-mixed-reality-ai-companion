namespace Jolly.XR.World
{
    /// <summary>Opens the runtime's room-scan UI. It is runtime-specific (Meta: Space Setup), so it lives in XR.MetaQuest.</summary>
    public interface ISceneCaptureLauncher
    {
        bool CanRequest { get; }
        bool TryRequest();
    }
}
