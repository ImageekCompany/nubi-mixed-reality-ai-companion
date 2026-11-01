using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Core.Memory
{
    /// <summary>Hand-written JSON codec of <c>scene.json</c>: {version, next_object, objects[], places[]}.</summary>
    public static class SceneFile
    {
        const int Version = 1;

        public static string Write(int nextObject, IEnumerable<SeenObjectDto> objects, IEnumerable<PlaceDto> places)
        {
            var text = new StringWriter();
            using (var w = new JsonTextWriter(text))
            {
                w.WriteStartObject();
                Field(w, "version", Version);
                Field(w, "next_object", nextObject);
                w.WritePropertyName("objects");
                w.WriteStartArray();
                foreach (var o in objects)
                {
                    w.WriteStartObject();
                    Field(w, "id", o.id); Field(w, "label", o.label); Field(w, "summary", o.summary);
                    Field(w, "x", o.x); Field(w, "y", o.y); Field(w, "z", o.z);
                    Field(w, "anchor", o.anchor); Field(w, "seen_ms", o.seen_ms);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WritePropertyName("places");
                w.WriteStartArray();
                foreach (var p in places)
                {
                    w.WriteStartObject();
                    Field(w, "name", p.name);
                    Field(w, "x", p.x); Field(w, "y", p.y); Field(w, "z", p.z);
                    Field(w, "qx", p.qx); Field(w, "qy", p.qy); Field(w, "qz", p.qz); Field(w, "qw", p.qw);
                    Field(w, "anchor", p.anchor); Field(w, "created_ms", p.created_ms);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return text.ToString();
        }

        /// <summary>Returns next_object. Throws <see cref="FormatException"/> or <see cref="JsonException"/> when malformed.</summary>
        public static int Read(string json, List<SeenObjectDto> objects, List<PlaceDto> places)
        {
            var root = JObject.Parse(json);
            if ((int?)root["version"] != Version)
                throw new FormatException("unsupported scene file version");
            foreach (var o in (JArray)root["objects"] ?? new JArray())
            {
                objects.Add(new SeenObjectDto
                {
                    id = (string)o["id"], label = (string)o["label"], summary = (string)o["summary"],
                    x = (float)o["x"], y = (float)o["y"], z = (float)o["z"],
                    anchor = (string)o["anchor"], seen_ms = (long)o["seen_ms"],
                });
            }
            foreach (var p in (JArray)root["places"] ?? new JArray())
            {
                places.Add(new PlaceDto
                {
                    name = (string)p["name"], x = (float)p["x"], y = (float)p["y"], z = (float)p["z"],
                    qx = (float)p["qx"], qy = (float)p["qy"], qz = (float)p["qz"], qw = (float)p["qw"],
                    anchor = (string)p["anchor"], created_ms = (long)p["created_ms"],
                });
            }
            return (int)root["next_object"];
        }

        static void Field(JsonWriter w, string name, string value)
        {
            if (value == null)
                return;
            w.WritePropertyName(name);
            w.WriteValue(value);
        }

        static void Field(JsonWriter w, string name, float value) { w.WritePropertyName(name); w.WriteValue(value); }
        static void Field(JsonWriter w, string name, long value) { w.WritePropertyName(name); w.WriteValue(value); }
    }
}
