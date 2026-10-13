using System.Collections.Generic;
using KiHan.Asset;
using KiHan.Logic;
using KiHan.Math;
using UnityEngine;

namespace KiHan.View.Render
{
    /// <summary>
    /// 角色表现层视图：只读绑定的 CharacterObj，不回写任何逻辑数据
    ///
    /// 节点结构 (代码构建，全部不旋转)：
    /// ActorView (地面锚点，BattleViewSpace.GroundToWorld)
    ///   ├ 脚下阴影 (CharacterFootShadow，略微后移，不随跳跃升高)
    ///   └ Body (本地 Y = 逻辑高度 z；挂接特效的父节点)
    ///       └ Sprite (帧偏移 + 主精灵) └ ExtraLayer_n (额外图层)
    ///
    /// 时序：位置在相邻两个逻辑帧快照之间插值 (滞后一帧，任意渲染帧率平滑)；
    /// 精灵帧与朝向直接读取逻辑当前状态 (素材帧率与逻辑帧率一致，逐帧精确)
    /// </summary>
    public class ActorView : MonoBehaviour
    {
        private const string SpriteMaterialPath = "Render/SpriteZWriteAlphaTest";

        /// <summary>
        /// 额外图层每级 OrderOffset 朝相机 (-Z) 前移的距离
        /// </summary>
        private const float LayerDepthStep = 0.001f;

        private static Material _spriteMaterial;

        public CharacterObj Character { get; private set; }

        /// <summary>
        /// 随角色位移与离地高度移动的节点，跟随型特效挂在此节点下
        /// </summary>
        public Transform Body { get; private set; }

        private SpriteRenderer _mainRenderer;
        private readonly List<SpriteRenderer> _extraRenderers = new List<SpriteRenderer>();
        private CharacterFootShadow _footShadow;

        // 逻辑帧快照 (x 横向, y 纵深, z 高度)
        private Vector3 _prevLogicPos;
        private Vector3 _currLogicPos;
        private float _lastTickTime;

        private static Material SpriteMaterial
        {
            get
            {
                if (_spriteMaterial == null) _spriteMaterial = AssetManager.Instance.Load<Material>(SpriteMaterialPath);
                return _spriteMaterial;
            }
        }

        /// <summary>
        /// 为逻辑角色创建视图
        /// </summary>
        public static ActorView Create(CharacterObj character)
        {
            var go = new GameObject($"{character.name}_View");
            var view = go.AddComponent<ActorView>();
            view.Build();
            view.Bind(character);
            return view;
        }

        private void Build()
        {
            _footShadow = gameObject.AddComponent<CharacterFootShadow>();

            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(Body, false);
            _mainRenderer = spriteGo.AddComponent<SpriteRenderer>();
            _mainRenderer.sharedMaterial = SpriteMaterial;
        }

        private void Bind(CharacterObj character)
        {
            Character = character;
            _currLogicPos = ReadLogicPos();
            _prevLogicPos = _currLogicPos;
            _lastTickTime = Time.time;
            _footShadow.InitShadow(character.TeamId);
            ApplyTransform(_currLogicPos);
            RenderFrame();
        }

        /// <summary>
        /// 每个逻辑帧结算后调用：推进插值快照
        /// </summary>
        public void OnLogicTick()
        {
            _prevLogicPos = _currLogicPos;
            _currLogicPos = ReadLogicPos();
            _lastTickTime = Time.time;
        }

        /// <summary>
        /// 销毁前归还对象池资源
        /// </summary>
        public void Release()
        {
            _footShadow.RecycleShadow();
            Character = null;
        }

        private Vector3 ReadLogicPos()
        {
            FPVector3 p = Character.motion.Position;
            return new Vector3(p.x.AsFloat(), p.y.AsFloat(), p.z.AsFloat());
        }

        private void LateUpdate()
        {
            if (Character == null) return;

            float alpha = Mathf.Clamp01((Time.time - _lastTickTime) / GameConfig.LOGIC_TICK_TIME);
            ApplyTransform(Vector3.Lerp(_prevLogicPos, _currLogicPos, alpha));
            RenderFrame();
        }

        private void ApplyTransform(Vector3 logicPos)
        {
            transform.position = BattleViewSpace.GroundToWorld(logicPos.x, logicPos.y);
            Body.localPosition = new Vector3(0f, logicPos.z, 0f);
        }

        private void RenderFrame()
        {
            FrameAnimator anim = Character.action != null ? Character.action.anim : null;
            SpriteFrameData frame = anim != null && anim.CurrentAnimData != null
                ? anim.CurrentAnimData.GetCurrentFrameData(anim.CurrentStepIndex)
                : null;

            if (frame == null || frame.Sprite == null)
            {
                _mainRenderer.enabled = false;
                HideExtraLayers(0);
                return;
            }

            bool flip = Character.FacingLeft;
            _mainRenderer.enabled = true;
            _mainRenderer.sprite = frame.Sprite;
            _mainRenderer.flipX = flip;
            _mainRenderer.transform.localPosition = new Vector3(
                (flip ? -frame.Offset.x : frame.Offset.x) * BattleViewSpace.PixelToWorld,
                frame.Offset.y * BattleViewSpace.PixelToWorld,
                0f);

            RenderExtraLayers(frame, flip);
        }

        private void RenderExtraLayers(SpriteFrameData frame, bool flip)
        {
            List<EffectLayerInfo> layers = frame.ExtraLayers;
            int count = layers != null ? layers.Count : 0;

            while (_extraRenderers.Count < count)
            {
                var layerGo = new GameObject($"ExtraLayer_{_extraRenderers.Count}");
                layerGo.transform.SetParent(_mainRenderer.transform, false);
                var sr = layerGo.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteMaterial;
                _extraRenderers.Add(sr);
            }

            for (int i = 0; i < count; i++)
            {
                EffectLayerInfo layer = layers[i];
                SpriteRenderer sr = _extraRenderers[i];
                sr.enabled = true;
                sr.sprite = layer.Sprite;
                sr.color = layer.TintColor;
                sr.flipX = flip;
                // 本地 -Z 朝向相机：用微小深度偏移代替 sortingOrder
                sr.transform.localPosition = new Vector3(
                    (flip ? -layer.Offset.x : layer.Offset.x) * BattleViewSpace.PixelToWorld,
                    layer.Offset.y * BattleViewSpace.PixelToWorld,
                    -layer.OrderOffset * LayerDepthStep);
            }

            HideExtraLayers(count);
        }

        private void HideExtraLayers(int from)
        {
            for (int i = from; i < _extraRenderers.Count; i++)
            {
                _extraRenderers[i].enabled = false;
            }
        }

        #region Gizmos：按逻辑判定盒 (定点数 AABB) 绘制

        private void OnDrawGizmos()
        {
            if (Character == null || Character.action == null || Character.action.anim == null) return;

            FrameAnimator anim = Character.action.anim;
            Gizmos.matrix = BattleViewSpace.LogicToWorldMatrix;

            Gizmos.color = new Color(0f, 0.4f, 1f, 0.8f);
            DrawBoxes(anim.GetCurrentHurtBoxes());

            Gizmos.color = new Color(1f, 0f, 0f, 0.9f);
            DrawBoxes(anim.GetCurrentHitBoxes());

            Gizmos.matrix = Matrix4x4.identity;
        }

        private void DrawBoxes(List<LogicBox> boxes)
        {
            if (boxes == null) return;
            foreach (LogicBox box in boxes)
            {
                if (!box.IsValid) continue;
                FPBox world = box.ToWorld(Character.motion.Position, Character.FacingLeft);
                Gizmos.DrawWireCube(world.Center.ToVector3(), (world.Max - world.Min).ToVector3());
            }
        }

        #endregion
    }
}
