using System;
using System.Linq;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Chat;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Chat
{
    public sealed class ResponsesRequestWriterTests
    {
        const string Model = "muse-spark-1.3";
        const string WalkSchema = "{\"type\":\"object\",\"properties\":{\"place\":{\"type\":\"string\",\"maxLength\":64}},\"required\":[\"place\"],\"additionalProperties\":false}";

        [Test]
        public void Write_SetsTheProtocolFieldsOfAdr0004()
        {
            var body = Write(new ModelRequest { Instructions = "Be Nubi.", Input = new[] { ConversationItem.User("hi") }, SafetyIdentifier = "abc" });

            Assert.That((string)body["model"], Is.EqualTo(Model));
            Assert.That((bool)body["stream"], Is.True);
            Assert.That((bool)body["store"], Is.False);
            Assert.That((bool)body["parallel_tool_calls"], Is.True);
            Assert.That((string)body.SelectToken("reasoning.effort"), Is.EqualTo("minimal"));
            Assert.That(body["include"].Values<string>(), Is.EqualTo(new[] { "reasoning.encrypted_content" }));
            Assert.That((int)body["max_output_tokens"], Is.EqualTo(1024));
            Assert.That((string)body["instructions"], Is.EqualTo("Be Nubi."));
            Assert.That((string)body["safety_identifier"], Is.EqualTo("abc"));
            Assert.That(body["tools"], Is.Null);
        }

        [Test]
        public void Write_PromptCacheKey_DependsOnlyOnTheStaticPrefix()
        {
            var tool = new ToolDefinition("walk_to", "Walk somewhere.", WalkSchema, TimeSpan.FromSeconds(3), ToolConcurrency.Locomotion);
            ModelRequest Request(string instructions, string user, params ToolDefinition[] tools) =>
                new() { Instructions = instructions, Input = new[] { ConversationItem.User(user) }, Tools = tools };

            var first = (string)Write(Request("Be Nubi.", "hi", tool))["prompt_cache_key"];
            var otherTurn = (string)Write(Request("Be Nubi.", "what is this?", tool))["prompt_cache_key"];
            var otherPersona = (string)Write(Request("Be a mentor.", "hi", tool))["prompt_cache_key"];
            var otherTools = (string)Write(Request("Be Nubi.", "hi"))["prompt_cache_key"];

            Assert.That(first, Does.Match("^jolly-[0-9a-f]{16}$"));
            Assert.That(otherTurn, Is.EqualTo(first), "the turn's input must not change the routing key");
            Assert.That(otherPersona, Is.Not.EqualTo(first));
            Assert.That(otherTools, Is.Not.EqualTo(first));
        }

        [TestCase(ReasoningEffort.Minimal, "minimal")]
        [TestCase(ReasoningEffort.Low, "low")]
        [TestCase(ReasoningEffort.Medium, "medium")]
        public void Write_MapsEffort(ReasoningEffort effort, string wire)
        {
            var body = Write(new ModelRequest { Effort = effort, Input = new[] { ConversationItem.User("hi") } });

            Assert.That((string)body.SelectToken("reasoning.effort"), Is.EqualTo(wire));
        }

        [Test]
        public void Write_Tools_UseTheFlatFunctionShapeWithTheRawSchema()
        {
            var tool = new ToolDefinition("walk_to", "Walk somewhere.", WalkSchema, TimeSpan.FromSeconds(3), ToolConcurrency.Locomotion);

            var body = Write(new ModelRequest { Input = new[] { ConversationItem.User("go") }, Tools = new[] { tool } });

            var json = (JObject)body["tools"].Single();
            Assert.That((string)json["type"], Is.EqualTo("function"));
            Assert.That((string)json["name"], Is.EqualTo("walk_to"));
            Assert.That((string)json["description"], Is.EqualTo("Walk somewhere."));
            Assert.That(JToken.DeepEquals(json["parameters"], JToken.Parse(WalkSchema)), Is.True);
        }

        [Test]
        public void Write_InputItems_MapToResponsesItemsInOrder()
        {
            const string reasoning = "{\"id\":\"rs_1\",\"type\":\"reasoning\",\"summary\":[],\"encrypted_content\":\"abc\"}";
            var input = new[]
            {
                ConversationItem.User("what is \"this\"?\n"),
                ConversationItem.Opaque(reasoning),
                ConversationItem.Assistant("On my way."),
                ConversationItem.Call("call_1", "walk_to", "{\"place\":\"sofa\"}"),
                ConversationItem.Result("call_1", "{\"status\":\"walking\"}"),
            };

            var items = (JArray)Write(new ModelRequest { Input = input })["input"];

            Assert.That(items.Count, Is.EqualTo(5));
            Assert.That((string)items[0]["role"], Is.EqualTo("user"));
            Assert.That((string)items[0].SelectToken("content[0].type"), Is.EqualTo("input_text"));
            Assert.That((string)items[0].SelectToken("content[0].text"), Is.EqualTo("what is \"this\"?\n"));
            Assert.That(JToken.DeepEquals(items[1], JToken.Parse(reasoning)), Is.True);
            Assert.That((string)items[2]["role"], Is.EqualTo("assistant"));
            Assert.That((string)items[2].SelectToken("content[0].type"), Is.EqualTo("output_text"));
            Assert.That((string)items[2].SelectToken("content[0].text"), Is.EqualTo("On my way."));
            Assert.That((string)items[3]["type"], Is.EqualTo("function_call"));
            Assert.That((string)items[3]["call_id"], Is.EqualTo("call_1"));
            Assert.That((string)items[3]["name"], Is.EqualTo("walk_to"));
            Assert.That((string)items[3]["arguments"], Is.EqualTo("{\"place\":\"sofa\"}"));
            Assert.That((string)items[4]["type"], Is.EqualTo("function_call_output"));
            Assert.That((string)items[4]["call_id"], Is.EqualTo("call_1"));
            Assert.That((string)items[4]["output"], Is.EqualTo("{\"status\":\"walking\"}"));
        }

        [Test]
        public void Write_UserImage_BecomesAnInputImageDataUrl()
        {
            var image = new EncodedImage(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg", 640, 480, default, Pose.identity);

            var item = Write(new ModelRequest { Input = new[] { ConversationItem.UserImage(image) } })["input"][0];

            Assert.That((string)item["role"], Is.EqualTo("user"));
            Assert.That((string)item.SelectToken("content[0].type"), Is.EqualTo("input_image"));
            Assert.That((string)item.SelectToken("content[0].image_url"), Is.EqualTo("data:image/jpeg;base64,/9j/"));
        }

        [Test]
        public void Write_EstimatesInputCharsFromTextAndAFixedCostPerImage()
        {
            var image = new EncodedImage(new byte[3000], "image/jpeg", 640, 480, default, Pose.identity);
            var request = new ModelRequest
            {
                Instructions = "1234",
                Input = new[] { ConversationItem.User("123456"), ConversationItem.UserImage(image) },
            };

            var written = new ResponsesRequestWriter().Write(request, Model);

            Assert.That(written.InputChars, Is.EqualTo(4 + 6 + ResponsesRequestWriter.ImageInputChars));
        }

        [Test]
        public void Write_ReusedWriter_ProducesIndependentBodies()
        {
            var writer = new ResponsesRequestWriter();

            var first = writer.Write(new ModelRequest { Input = new[] { ConversationItem.User("one") } }, Model).Json;
            var second = writer.Write(new ModelRequest { Input = new[] { ConversationItem.User("two") } }, Model).Json;

            Assert.That(first, Does.Contain("one").And.Not.Contain("two"));
            Assert.That(second, Does.Contain("two").And.Not.Contain("one"));
        }

        static JObject Write(ModelRequest request) => JObject.Parse(new ResponsesRequestWriter().Write(request, Model).Json);
    }
}
