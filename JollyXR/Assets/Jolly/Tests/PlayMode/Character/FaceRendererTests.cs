using Jolly.Character;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>Face channels onto a character whose blendshapes are named differently from Nubi's, and onto a jaw.</summary>
    public sealed class FaceRendererTests
    {
        GameObject _face;
        SkinnedMeshRenderer _renderer;
        Mesh _mesh;
        Transform _jaw;
        readonly float[] _weights = new float[FaceChannels.Count];

        [SetUp]
        public void SetUp()
        {
            _mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            foreach (var shape in new[] { "MouthOpen", "Grin", "EyesShut" })
                _mesh.AddBlendShapeFrame(shape, 100f, new Vector3[3], null, null);
            _face = new GameObject("Face");
            _renderer = _face.AddComponent<SkinnedMeshRenderer>();
            _renderer.sharedMesh = _mesh;
            _jaw = new GameObject("Jaw").transform;
            _jaw.SetParent(_face.transform, false);
            System.Array.Clear(_weights, 0, _weights.Length);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_face);
            Object.Destroy(_mesh);
        }

        float Weight(string shape) => _renderer.GetBlendShapeWeight(_mesh.GetBlendShapeIndex(shape));

        static FaceChannelBinding Bind(string channel, string shape, float weight = 1f, float jaw = 0f) => new()
        {
            channel = channel,
            blendShapes = shape == null ? new BlendShapeTarget[0] : new[] { new BlendShapeTarget { name = shape, weight = weight } },
            jawOpen = jaw,
        };

        [Test]
        public void Write_RenamedShape_ScaledByItsWeight()
        {
            // Arrange
            var face = new FaceRenderer(_renderer, new[] { Bind("Smile", "Grin", 0.5f) });
            _weights[(int)FaceChannel.Smile] = 1f;

            // Act
            face.Write(_weights);

            // Assert
            Assert.That(Weight("Grin"), Is.EqualTo(50f).Within(0.1f));
        }

        [Test]
        public void Write_ShapeSharedByTwoChannels_TakesTheStrongest()
        {
            // Arrange: one shape for both eyes.
            var face = new FaceRenderer(_renderer, new[] { Bind("Blink_L", "EyesShut"), Bind("Blink_R", "EyesShut") });
            _weights[(int)FaceChannel.BlinkLeft] = 0.3f;
            _weights[(int)FaceChannel.BlinkRight] = 0.8f;

            // Act
            face.Write(_weights);

            // Assert
            Assert.That(Weight("EyesShut"), Is.EqualTo(80f).Within(0.1f));
        }

        [Test]
        public void Write_JawViseme_OpensTheJawFromItsBindPose()
        {
            // Arrange
            var rest = Quaternion.Euler(0f, 30f, 0f);
            _jaw.localRotation = rest;
            var face = new FaceRenderer(_renderer, new[] { Bind("A", null, jaw: 1f), Bind("O", "MouthOpen", jaw: 0.5f) },
                                        _jaw, new Vector3(20f, 0f, 0f));
            _weights[(int)FaceChannel.A] = 0.5f;

            // Act: twice, to show the jaw does not accumulate.
            face.Write(_weights);
            face.Write(_weights);

            // Assert
            Assert.That(Quaternion.Angle(_jaw.localRotation, rest * Quaternion.Euler(10f, 0f, 0f)), Is.LessThan(0.01f));
            Assert.That(Weight("MouthOpen"), Is.Zero);
        }

        [Test]
        public void Construct_UnknownShapeOrChannel_IsSkipped()
        {
            var face = new FaceRenderer(_renderer, new[] { Bind("Smile", "NoSuchShape"), Bind("Wink", "Grin") });
            _weights[(int)FaceChannel.Smile] = 1f;

            Assert.DoesNotThrow(() => face.Write(_weights));
            Assert.That(Weight("Grin"), Is.Zero);
        }
    }
}
