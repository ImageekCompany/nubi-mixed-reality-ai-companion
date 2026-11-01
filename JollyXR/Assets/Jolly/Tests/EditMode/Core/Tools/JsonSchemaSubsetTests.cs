using System;
using Jolly.Core.Tools;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Tools
{
    public sealed class JsonSchemaSubsetTests
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"place\":{\"type\":\"string\",\"maxLength\":8,\"description\":\"d\"}," +
            "\"name\":{\"type\":\"string\",\"enum\":[\"wave\",\"nod\"]}," +
            "\"speed\":{\"type\":\"number\",\"minimum\":0.5,\"maximum\":2}," +
            "\"seated\":{\"type\":\"boolean\"}}," +
            "\"required\":[\"place\"],\"additionalProperties\":false}";

        static readonly ToolSchema Parsed = JsonSchemaSubset.Parse(Schema);

        [Test]
        public void Validate_AcceptsEveryPropertyType()
        {
            var ok = JsonSchemaSubset.Validate(Parsed, "{\"place\":\"sofa\",\"name\":\"nod\",\"speed\":1,\"seated\":true}",
                out var args, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(args.TryGetString("place", out var place) && place == "sofa", Is.True);
            Assert.That(args.TryGetString("name", out var name) && name == "nod", Is.True);
            Assert.That(args.TryGetNumber("speed", out var speed) && speed == 1.0, Is.True);
            Assert.That(args.TryGetBool("seated", out var seated) && seated, Is.True);
            Assert.That(args.TryGetString("speed", out _), Is.False, "typed getters do not convert");
            Assert.That(args.TryGetString("missing", out _), Is.False);
        }

        [TestCase("{}", "missing required property 'place'")]
        [TestCase("{\"place\":5}", "property 'place' must be a string")]
        [TestCase("{\"place\":\"123456789\"}", "property 'place' is longer than 8 characters")]
        [TestCase("{\"place\":\"a\",\"name\":\"dance\"}", "property 'name' must be one of: wave, nod")]
        [TestCase("{\"place\":\"a\",\"speed\":3}", "property 'speed' must be at most 2")]
        [TestCase("{\"place\":\"a\",\"speed\":0.1}", "property 'speed' must be at least 0.5")]
        [TestCase("{\"place\":\"a\",\"speed\":\"fast\"}", "property 'speed' must be a number")]
        [TestCase("{\"place\":\"a\",\"seated\":\"yes\"}", "property 'seated' must be a boolean")]
        [TestCase("{\"place\":\"a\",\"extra\":1}", "unknown property 'extra'")]
        [TestCase("[1]", "arguments must be a JSON object")]
        [TestCase("{\"place\":", "arguments are not valid JSON")]
        [TestCase("{\"place\":null}", "property 'place' must be a string")]
        public void Validate_RejectsWithModelFacingMessage(string json, string expected)
        {
            var ok = JsonSchemaSubset.Validate(Parsed, json, out var args, out var error);

            Assert.That(ok, Is.False);
            Assert.That(args, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Validate_EmptyArguments_MeanEmptyObject(string json)
        {
            var empty = JsonSchemaSubset.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}");

            Assert.That(JsonSchemaSubset.Validate(empty, json, out _, out var error), Is.True, error);
        }

        [TestCase("{\"type\":\"array\"}")]
        [TestCase("{\"type\":\"object\",\"properties\":{}}")]
        [TestCase("{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"object\"}},\"additionalProperties\":false}")]
        [TestCase("{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"string\",\"pattern\":\"x\"}},\"additionalProperties\":false}")]
        [TestCase("{\"type\":\"object\",\"properties\":{},\"required\":[\"a\"],\"additionalProperties\":false}")]
        [TestCase("not json")]
        public void Parse_RejectsSchemasOutsideTheSubset(string schema)
        {
            Assert.Throws<ArgumentException>(() => JsonSchemaSubset.Parse(schema));
        }
    }
}
