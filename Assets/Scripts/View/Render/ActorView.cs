using System.Collections.Generic;
using KiHan.Asset;
using KiHan.Logic;
using KiHan.Math;
using Managers;
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

        /// <summary>
        /// 同纵深角色的前后裁决：按 EntityId 分档朝相机 (-Z) 前移，EntityId 越大越靠前。
        /// 每档间距大于额外图层的深度跨度 (OrderOffset ±1 → ±LayerDepthStep)，保证两个角色整体前后分开、不交叉穿插；
        /// 最大偏移 (ActorDepthSlots - 1) × ActorDepthStep 小于 ActionEffectOffset，不会盖过同纵深其他角色的动作特效
        /// </summary>
        private const float ActorDepthStep = 0.004f;
        private const int ActorDepthSlots = 8;

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

        /// <summary>
        /// 本角色触发的需要跟踪的动作事件特效：跟随型按锚点逐帧定位，所属动作结束时可提前停止
        /// </summary>
        private struct BoundEffect
        {
            public int EffectId;
            public bool Follow;
            public string Anchor;
            public Vector2Int Offset;
            public Vector2Int LastAnchor;
            public int ActionSerial;
            public bool StopOnActionEnd;

            /// <summary>
            /// Buff 层编号 (0 表示不是 Buff 特效)：Buff 到期时按编号停止
            /// </summary>
            public int BuffSerial;
        }

        private readonly List<BoundEffect> _boundEffects = new List<BoundEffect>(4);

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
            // 只缩放 Body 自身：其 localPosition (离地高度) 不受影响，子节点 (精灵/图层/跟随特效) 整体缩放
            Body.localScale = Vector3.one * BattleViewSpace.CharacterScale;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(Body, false);
            _mainRenderer = spriteGo.AddComponent<SpriteRenderer>();
            _mainRenderer.sharedMaterial = SpriteMaterial;
        }

        private void Bind(CharacterObj character)
        {
            Character = character;
            EnsureExtraRenderers(MaxExtraLayers(character));
            _currLogicPos = ReadLogicPos();
            _prevLogicPos = _currLogicPos;
            _lastTickTime = Time.time;
            // 简单生成物 (投射物) 没有脚下阴影
            _footShadow.EnableShadow = character.Hittable;
            _footShadow.InitShadow(character.TeamId);
            ApplyTransform(_currLogicPos);
            RenderFrame();
        }

        /// <summary>
        /// 复用视图：绑定到 (重新生成的) 逻辑实体并显示
        /// </summary>
        public void Rebind(CharacterObj character)
        {
            gameObject.SetActive(true);
            Bind(character);
        }

        /// <summary>
        /// 实体移出战场：停止跟随特效并隐藏，保留节点与阴影供下次复用
        /// </summary>
        public void Hide()
        {
            StopBoundEffects();
            Character = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 登记本角色触发的特效
        /// </summary>
        /// <param name="follow">跟随型 (特效已由 EffectManager 挂到 Body 下)：逐帧对齐锚点并随朝向镜像；否则只跟踪停止条件</param>
        public void AttachEffect(int effectId, bool follow, string anchor, Vector2Int offset, int actionSerial, bool stopOnActionEnd, int buffSerial = 0)
        {
            _boundEffects.Add(new BoundEffect
            {
                EffectId = effectId,
                Follow = follow,
                Anchor = anchor,
                Offset = offset,
                ActionSerial = actionSerial,
                StopOnActionEnd = stopOnActionEnd,
                BuffSerial = buffSerial
            });
            UpdateBoundEffects();
        }

        /// <summary>
        /// Buff 到期：停止该层的循环特效
        /// </summary>
        public void StopBuffEffect(int buffSerial)
        {
            for (int i = _boundEffects.Count - 1; i >= 0; i--)
            {
                if (_boundEffects[i].BuffSerial != buffSerial) continue;
                EffectManager.Instance.StopEffect(_boundEffects[i].EffectId);
                _boundEffects.RemoveAt(i);
            }
        }

        /// <summary>
        /// 角色全部动画帧中额外图层的最大数量 (绑定时一次性预建渲染节点，局内不再新建)
        /// </summary>
        private static int MaxExtraLayers(CharacterObj character)
        {
            FrameAnimator anim = character.action != null ? character.action.anim : null;
            if (anim == null) return 0;

            int max = 0;
            foreach (AnimationFrameData data in anim.Anims)
            {
                for (int i = 0; i < data.Steps.Count; i++)
                {
                    SpriteFrameData frame = data.GetCurrentFrameData(i);
                    if (frame != null && frame.ExtraLayers != null && frame.ExtraLayers.Count > max) max = frame.ExtraLayers.Count;
                }
            }
            return max;
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
            StopBoundEffects();
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
            UpdateBoundEffects();
        }

        /// <summary>
        /// 跟随特效逐帧定位到当前动画帧的锚点 (锚点本帧缺失时沿用上次位置) 并随朝向镜像；
        /// 特效已自然结束则注销，所属动作已结束且要求停止时提前回收
        /// </summary>
        private void UpdateBoundEffects()
        {
            if (_boundEffects.Count == 0) return;

            EffectManager effects = EffectManager.Instance;
            int serial = Character.action != null ? Character.action.ActionSerial : 0;
            FrameAnimator anim = Character.action != null ? Character.action.anim : null;
            SpriteFrameData frame = anim != null && anim.CurrentAnimData != null ? anim.CurrentAnimData.GetCurrentFrameData(anim.CurrentStepIndex) : null;
            bool flip = Character.FacingLeft;

            for (int i = _boundEffects.Count - 1; i >= 0; i--)
            {
                BoundEffect bound = _boundEffects[i];
                if (!effects.TryGetEffect(bound.EffectId, out EffectView view))
                {
                    _boundEffects.RemoveAt(i);
                    continue;
                }

                if (bound.StopOnActionEnd && bound.ActionSerial != serial)
                {
                    effects.StopEffect(bound.EffectId);
                    _boundEffects.RemoveAt(i);
                    continue;
                }

                if (!bound.Follow) continue;

                if (!string.IsNullOrEmpty(bound.Anchor) && TryGetAnchor(frame, bound.Anchor, out Vector2Int anchor))
                {
                    bound.LastAnchor = anchor;
                    _boundEffects[i] = bound;
                }

                Vector2Int px = bound.LastAnchor + bound.Offset;
                Transform t = view.transform;
                t.localPosition = new Vector3(
                    (flip ? -px.x : px.x) * BattleViewSpace.PixelToWorld,
                    px.y * BattleViewSpace.PixelToWorld,
                    -BattleViewSpace.ActionEffectOffset / BattleViewSpace.CharacterScale);

                Vector3 scale = t.localScale;
                scale.x = flip ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                t.localScale = scale;
            }
        }

        private static bool TryGetAnchor(SpriteFrameData frame, string name, out Vector2Int position)
        {
            position = Vector2Int.zero;
            if (frame == null || frame.EffectAnchors == null) return false;
            for (int i = 0; i < frame.EffectAnchors.Count; i++)
            {
                EffectAnchorInfo a = frame.EffectAnchors[i];
                if (a != null && a.Name == name)
                {
                    position = a.Position;
                    return true;
                }
            }
            return false;
        }

        private void StopBoundEffects()
        {
            EffectManager effects = EffectManager.Instance;
            for (int i = 0; i < _boundEffects.Count; i++)
            {
                effects.StopEffect(_boundEffects[i].EffectId);
            }
            _boundEffects.Clear();
        }

        private void ApplyTransform(Vector3 logicPos)
        {
            // 纵深相同时世界 Z 相同，ZTest LEqual 下两者像素谁覆盖谁取决于不稳定的绘制顺序 (互相穿插/闪烁)，加档位偏移打破平局
            float depthBias = (Character.EntityId & (ActorDepthSlots - 1)) * ActorDepthStep;
            transform.position = BattleViewSpace.GroundToWorld(logicPos.x, logicPos.y) + new Vector3(0f, 0f, -depthBias);
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

            EnsureExtraRenderers(count);

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

        private void EnsureExtraRenderers(int count)
        {
            while (_extraRenderers.Count < count)
            {
                var layerGo = new GameObject($"ExtraLayer_{_extraRenderers.Count}");
                layerGo.transform.SetParent(_mainRenderer.transform, false);
                var sr = layerGo.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteMaterial;
                sr.enabled = false;
                _extraRenderers.Add(sr);
            }
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
