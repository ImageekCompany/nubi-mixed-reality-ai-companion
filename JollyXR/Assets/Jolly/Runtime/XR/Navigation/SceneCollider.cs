using UnityEngine;

namespace Jolly.XR.Navigation
{
    /// <summary>A collider child under one AR trackable, so it follows the trackable's pose updates.</summary>
    sealed class SceneCollider
    {
        readonly GameObject _gameObject;
        Mesh _mesh;

        public SceneCollider(Transform trackable, int layer)
        {
            _gameObject = new GameObject("NavGeometry") { layer = layer };
            _gameObject.transform.SetParent(trackable, false);
        }

        public Collider Collider { get; private set; }

        public Mesh Mesh
        {
            get
            {
                if (_mesh == null)
                    _mesh = new Mesh { name = "NavFloor" };
                return _mesh;
            }
        }

        public T GetOrAdd<T>() where T : Collider
        {
            var typed = Collider as T;
            if (typed != null)
                return typed;
            typed = _gameObject.AddComponent<T>();
            Collider = typed;
            return typed;
        }

        public void Destroy()
        {
            if (_mesh != null)
                Object.Destroy(_mesh);
            if (_gameObject != null)
                Object.Destroy(_gameObject);
        }
    }
}
