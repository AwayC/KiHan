using UnityEngine;
using UnityEngine.UI;

namespace KiHan.UI
{
    /// <summary>
    /// 柔边蒙版：挂在要被裁切的图片上，用一张形状图的 alpha 乘到图片上 (双线性采样，斜边平滑；Unity 的 Mask 是模板裁切，斜边有锯齿)
    /// - 形状图拉伸铺满 area 矩形，矩形外按形状图边缘像素延伸 (形状图上边缘不透明 = 向上无限显示)
    /// - area 与图片都不能旋转 (只支持平移与缩放)；area 可以是图片的父节点，调 area 不影响裁切形状的比例
    /// - 每个实例持有一份材质副本 (打断合批，只用于确实需要的节点)；位置变化时只改材质参数，不重建网格
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UISoftMask : MonoBehaviour, IMaterialModifier
    {
        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");
        private static readonly int MaskUVId = Shader.PropertyToID("_MaskUV");

        private static readonly string[] StencilProperties =
        {
            "_Stencil", "_StencilComp", "_StencilOp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask"
        };

        [Tooltip("蒙版区域：形状图拉伸铺满这个矩形")]
        [SerializeField] private RectTransform _area;

        [Tooltip("蒙版形状：取 alpha (不透明 = 显示)，边缘半透明即柔边")]
        [SerializeField] private Sprite _shape;

        [Tooltip("柔边蒙版材质 (GUI/Materials/UISoftMask.mat)")]
        [SerializeField] private Material _baseMaterial;

        private Graphic _graphic;
        private Material _material;
        private Vector4 _rect;
        private readonly Vector3[] _corners = new Vector3[4];

        private bool Ready => _area != null && _shape != null && _baseMaterial != null;

        private void OnEnable()
        {
            _graphic = GetComponent<Graphic>();
            _graphic.SetMaterialDirty();
        }

        private void OnDisable()
        {
            if (_graphic != null) _graphic.SetMaterialDirty();
            ReleaseMaterial();
        }

        private void OnDestroy()
        {
            ReleaseMaterial();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ReleaseMaterial();
            if (_graphic != null) _graphic.SetMaterialDirty();
        }
#endif

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled || !Ready) return baseMaterial;

            if (_material == null)
            {
                _material = new Material(_baseMaterial) { name = _baseMaterial.name + " (Instance)", hideFlags = HideFlags.HideAndDontSave };
                Texture texture = _shape.texture;
                Rect uv = _shape.textureRect;
                _material.SetTexture(MaskTexId, texture);
                _material.SetVector(MaskUVId, new Vector4(uv.x / texture.width, uv.y / texture.height, uv.width / texture.width, uv.height / texture.height));
                _rect = Vector4.zero;
            }

            // 上游 (Mask 模板等) 改过的模板参数保留下来
            foreach (string property in StencilProperties)
            {
                if (baseMaterial.HasProperty(property)) _material.SetFloat(property, baseMaterial.GetFloat(property));
            }

            UpdateRect();
            return _material;
        }

        private void LateUpdate()
        {
            if (_material != null) UpdateRect();
        }

        /// <summary>
        /// 蒙版矩形换算到 UI 网格所在的空间 (根画布本地坐标，与 RectMask2D 的裁切矩形一致)
        /// </summary>
        private void UpdateRect()
        {
            Canvas canvas = _graphic != null ? _graphic.canvas : null;
            if (canvas == null || _area == null) return;

            Transform space = canvas.rootCanvas.transform;
            _area.GetWorldCorners(_corners);
            Vector3 min = space.InverseTransformPoint(_corners[0]);
            Vector3 max = space.InverseTransformPoint(_corners[2]);
            float width = max.x - min.x;
            float height = max.y - min.y;
            if (Mathf.Abs(width) < 1e-4f || Mathf.Abs(height) < 1e-4f) return;

            var rect = new Vector4(min.x, min.y, 1f / width, 1f / height);
            if (rect == _rect) return;
            _rect = rect;
            _material.SetVector(MaskRectId, rect);
        }

        private void ReleaseMaterial()
        {
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
            _material = null;
        }
    }
}
