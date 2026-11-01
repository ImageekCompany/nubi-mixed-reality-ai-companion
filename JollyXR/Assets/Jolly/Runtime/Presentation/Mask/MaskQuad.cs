using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>One pooled overlay quad: its renderer and an R8 texture holding the box-local mask.</summary>
    sealed class MaskQuad
    {
        static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTex_TexelSize");

        readonly GameObject _gameObject;
        readonly MeshRenderer _renderer;
        readonly MaterialPropertyBlock _properties = new();
        Texture2D _texture;

        public MaskQuad(Transform parent, Mesh mesh, Material material, int index)
        {
            _gameObject = new GameObject("MaskQuad" + index);
            _gameObject.transform.SetParent(parent, false);
            _gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            _renderer = _gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _gameObject.SetActive(false);
        }

        public bool IsVisible => _gameObject.activeSelf;
        public Transform Transform => _gameObject.transform;
        public Texture2D Texture => _texture;

        public void Show(Pose pose, Vector2 size, BitMask mask)
        {
            Upload(mask);
            _gameObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            _properties.SetTexture(MaskTexId, _texture);
            // Set explicitly: _TexelSize is not guaranteed for textures bound through a property block, and the
            // outline (the non-colour cue, architecture §11) vanishes when it is zero.
            _properties.SetVector(MaskTexelSizeId, new Vector4(1f / _texture.width, 1f / _texture.height, _texture.width, _texture.height));
            SetFade(1f);
            _gameObject.SetActive(true);
        }

        public void SetFade(float fade)
        {
            _properties.SetFloat(FadeId, fade);
            _renderer.SetPropertyBlock(_properties);
        }

        public void Hide() => _gameObject.SetActive(false);

        public void Destroy()
        {
            if (_texture != null)
                Object.Destroy(_texture);
            if (_gameObject != null)
                Object.Destroy(_gameObject);
        }

        /// <summary>Mask rows are top-first; texture rows are bottom-first, so rows flip.</summary>
        void Upload(BitMask mask)
        {
            int w = mask.Width, h = mask.Height;
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null)
                    Object.Destroy(_texture);
                _texture = new Texture2D(w, h, TextureFormat.R8, false, true)
                {
                    name = "Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
            }

            var pixels = _texture.GetPixelData<byte>(0);
            for (var y = 0; y < h; y++)
            {
                var row = (h - 1 - y) * w;
                for (var x = 0; x < w; x++)
                    pixels[row + x] = mask[x, y] ? (byte)255 : (byte)0;
            }
            _texture.Apply(false, false);
        }
    }
}
