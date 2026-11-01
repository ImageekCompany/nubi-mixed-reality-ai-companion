using Jolly.App;
using Jolly.Core.Tools;
using Jolly.Tests.PlayMode.Presentation;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.App
{
    /// <summary>Privacy settings of the persona assets that ship, not of test profiles.</summary>
    public sealed class ShippedPersonaTests
    {
        const string Folder = "Assets/Jolly/Content/Personas/";

        [Test]
        public void Listener_CanSwitchByVoiceButNeverUsesTheCamera()
        {
            var tools = PresentationAssets.Load<PersonaProfileAsset>(Folder + "Psychologist.asset").allowedTools;

            Assert.That(tools, Does.Contain(ToolNames.SetPersona));
            Assert.That(tools, Has.None.EqualTo(ToolNames.CaptureAndIdentify).And.None.EqualTo(ToolNames.FindObject)
                                  .And.None.EqualTo(ToolNames.HighlightPart));
        }
    }
}
