using UnityEngine;

namespace Jolly.App
{
    /// <summary>Composition root of Main.unity (architecture §3, ADR-0009).</summary>
    [DisallowMultipleComponent]
    public sealed class JollyBootstrap : MonoBehaviour
    {
    }
}
