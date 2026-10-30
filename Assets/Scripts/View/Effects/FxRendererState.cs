using UnityEngine;

namespace KiHan.View.Effects
{
    /// <summary>
    /// 特效渲染器的动画状态汇总：各动画组件只写入自己的槽位，每帧由本组件合成后一次性写入 MaterialPropertyBlock
    /// (不实例化材质，0 GC)。颜色 = 材质 _TintColor × 曲线颜色 × 变色；贴图 ST = 材质 ST × 序列帧 × 平铺，偏移相加
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class FxRendererState : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        public Color CurveTint = Color.white;
        public Color ChangeTint = Color.white;
        public Vector2 SpriteScale = Vector2.one;
        public Vector2 SpriteOffset = Vector2.zero;
        public Vector2 Tiling = Vector2.one;
        public Vector2 ScrollOffset = Vector2.zero;
        public Vector2 CurveOffset = Vector2.zero;

        /// <summary>
        /// 本节点动画要求隐藏 (序列帧延迟前 / 播完自动销毁)
        /// </summary>
        public bool SelfHidden;

        /// <summary>
        /// 本节点或祖先节点的曲线动画播完自动销毁 (原版销毁整个子树)
        /// </summary>
        public bool Destroyed;

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _baseTint = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private Vector4 _baseSt = new Vector4(1f, 1f, 0f, 0f);

        private void Init()
        {
            if (_renderer != null) return;
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            Material mat = _renderer.sharedMaterial;
            if (mat != null && mat.HasProperty(TintId)) _baseTint = mat.GetColor(TintId);
            if (mat != null && mat.HasProperty(MainTexStId)) _baseSt = mat.GetVector(MainTexStId);
        }

        private void OnEnable()
        {
            ResetState();
        }

        internal void ResetState()
        {
            CurveTint = Color.white;
            ChangeTint = Color.white;
            SpriteScale = Vector2.one;
            SpriteOffset = Vector2.zero;
            Tiling = Vector2.one;
            ScrollOffset = Vector2.zero;
            CurveOffset = Vector2.zero;
            SelfHidden = false;
            Destroyed = false;
        }

        private void LateUpdate()
        {
            Apply();
        }

        internal void Apply()
        {
            Init();
            bool hidden = SelfHidden || Destroyed;
            _renderer.enabled = !hidden;
            if (hidden) return;

            var scale = new Vector2(_baseSt.x * SpriteScale.x * Tiling.x, _baseSt.y * SpriteScale.y * Tiling.y);
            Vector2 offset = new Vector2(_baseSt.z, _baseSt.w) + SpriteOffset + ScrollOffset + CurveOffset;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(TintId, _baseTint * CurveTint * ChangeTint);
            _block.SetVector(MainTexStId, new Vector4(scale.x, scale.y, offset.x, offset.y));
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// 标记节点及其子节点上的全部特效渲染器为已销毁 (隐藏，以便对象池复用时恢复)
        /// </summary>
        public static void SetDestroyed(Transform root, bool destroyed)
        {
            foreach (FxRendererState state in root.GetComponentsInChildren<FxRendererState>(true))
            {
                state.Destroyed = destroyed;
            }
        }
    }

    /// <summary>
    /// 原版特效动画组件 (FxCurveAnimation / FxUvAnimation / FxSpriteAnimation / FxBillboard / FxChangeColor) 的基类：
    /// OnEnable 从 0 开始计时 (对象池复用时重播)，Update 累计时间后按该时刻采样
    /// </summary>
    public abstract class FxAnimatedBase : MonoBehaviour
    {
        private float _time;
        private FxRendererState _state;

        /// <summary>
        /// 本节点的渲染状态 (没有渲染器的节点为 null)
        /// </summary>
        protected FxRendererState State => _state != null ? _state : (_state = GetComponent<FxRendererState>());

        protected virtual void OnEnable()
        {
            _time = 0f;
            Restart();
            Sample(0f);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            Sample(_time);
        }

        protected virtual void Restart()
        {
        }

        protected abstract void Sample(float time);

        /// <summary>
        /// 把整个特效子树的 Fx* 动画与粒子同步到指定时刻 (编辑模式预览用；运行时由 Update 自行推进)
        /// </summary>
        public static void SampleTree(GameObject root, float time)
        {
            FxAnimatedBase[] animated = root.GetComponentsInChildren<FxAnimatedBase>(true);
            FxRendererState[] states = root.GetComponentsInChildren<FxRendererState>(true);
            foreach (FxAnimatedBase item in animated) item.Restart();
            foreach (FxRendererState state in states) state.ResetState();
            foreach (FxAnimatedBase item in animated) item.Sample(time);
            foreach (FxRendererState state in states) state.Apply();
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                // 只驱动最外层粒子：Simulate(withChildren) 会带上子粒子
                Transform parent = ps.transform.parent;
                if (parent == null || parent.GetComponentInParent<ParticleSystem>(true) == null) ps.Simulate(time, true, true);
            }
        }
    }
}
