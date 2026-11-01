using System.IO;
using Jolly.Contracts;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jolly.Tests.EditMode.Vision.Doubles
{
    /// <summary>Recorded SAM 3.1 responses and sample images under Tests/Fixtures (provenance in ATTRIBUTION.md).</summary>
    static class VisionFixtures
    {
        public const string PenConcept = "pen_concept_onebit.response.json";
        public const string MugConcept = "mug_concept_onebit.response.json";
        public const string MugNoMatch = "mug_banana_nomatch.response.json";
        public const string PenPointBoxLossless = "pen_point_box_lossless.txt";

        static string Root => Path.Combine(Application.dataPath, "Jolly", "Tests", "Fixtures");

        public static string Sam(string name) => File.ReadAllText(Path.Combine(Root, "sam", name));

        public static string SamOutputText(string responseName) =>
            (string)JObject.Parse(Sam(responseName))["output"][0]["content"][0]["text"];

        public static byte[] Image(string name) => File.ReadAllBytes(Path.Combine(Root, "images", name));

        public static EncodedImage Jpeg(int width, int height) =>
            new(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, "image/jpeg", width, height,
                new CameraIntrinsics(new Vector2(500, 500), new Vector2(width / 2f, height / 2f), new Vector2Int(width, height)),
                new Pose(Vector3.zero, Quaternion.identity));
    }
}
