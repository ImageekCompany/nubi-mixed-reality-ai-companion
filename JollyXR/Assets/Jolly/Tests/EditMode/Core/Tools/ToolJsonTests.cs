using Jolly.Contracts;
using Jolly.Core.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Tools
{
    public sealed class ToolJsonTests
    {
        [Test]
        public void JsonString_EscapesQuotesBackslashesAndEveryControlChar()
        {
            var raw = "a\"b\\c\nd\re\tf\u0001g\u001fh\u2028";

            var json = ToolResult.JsonString(raw);

            Assert.That(json, Is.EqualTo("\"a\\\"b\\\\c\\nd\\re\\tf\\u0001g\\u001fh\u2028\""));
            Assert.That(JToken.Parse(json).Value<string>(), Is.EqualTo(raw));
        }

        [Test]
        public void Failure_IsValidJsonWithCodeAndDetail()
        {
            var result = ToolResult.Failure("invalid_arguments", "property 'a' \"x\"\n");

            var parsed = JObject.Parse(result.OutputJson);

            Assert.That(result.Ok, Is.False);
            Assert.That((string)parsed["error"], Is.EqualTo("invalid_arguments"));
            Assert.That((string)parsed["detail"], Is.EqualTo("property 'a' \"x\"\n"));
        }

        [Test]
        public void DefaultResult_OutputsEmptyObject()
        {
            Assert.That(default(ToolResult).OutputJson, Is.EqualTo("{}"));
        }

        [Test]
        public void ToolJson_WritesTypedMembersInvariantly()
        {
            var json = new ToolJson().Add("status", "walking").Add("eta_s", 4).Add("distance_m", 1.84999).Add("ok", true)
                                     .Add("label", "say \"hi\"").ToString();

            Assert.That(json, Is.EqualTo("{\"status\":\"walking\",\"eta_s\":4,\"distance_m\":1.85,\"ok\":true,\"label\":\"say \\\"hi\\\"\"}"));
        }

        [Test]
        public void ToolJson_Empty_IsEmptyObject()
        {
            Assert.That(new ToolJson().ToString(), Is.EqualTo("{}"));
        }
    }
}
