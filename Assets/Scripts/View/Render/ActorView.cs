using System;
using System.Collections.Generic;
using KiHan.Logic;
using KiHan.Math;
using UnityEngine;

namespace KiHan.View.Render
{
    /// <summary>
    /// 动作角色 3D 纵深表现层视图 (ActorView)
    /// 核心职责：
    /// 1. 3D 空间映射：Logic.pos.x -> World.X, Logic.height -> World.Y, Logic.pos.y(纵深) -> World.Z
    /// 2. 2D 序列帧驱动：负责 Sprite 帧切换与平滑插值
    /// 3. 硬件深度遮挡：依托 Alpha-Test ZWrite Shader 让 2D 角色天然与 3D 粒子网格产生 Z-Buffer 穿插
    /// 4. 骨骼挂点管理与脚下圆盘阴影联动
    /// </summary>
    public class ActorView : MonoBehaviour
    {
        [Header("逻辑实体绑定")]
        public LogicEntity BindEntity;

        [Header("核心渲染节点")]
        [SerializeField] protected SpriteRenderer mainRenderer;
        [SerializeField] protected Transform displayRoot;
        [SerializeField] protected CharacterSockets sockets;
        [Header("独立脚下圆盘阴影组件 (可开关/可换皮)")]
        [SerializeField] protected CharacterFootShadow footShadow;

        [Header("多图层渲染 (额外图层，如武器特效、光效贴花)")]
        [SerializeField] protected List<SpriteRenderer> extraRenderers = new();

        // 平滑插值相关状态
        protected Vector3 _targetWorldPos;
        protected float _interpolateTime = 0f;
        protected float _stepDuration = LogicEntity.LOGIC_TICK_TIME;

        // 动画序列帧播放状态
        protected AnimationFrameData _lastAnim;
        protected int _lastAnimVersion = -1;
        protected float _visualTimer = 0f;
        protected int _visualFrameIndex = 0;

        public CharacterSockets Sockets => sockets;
        public CharacterFootShadow FootShadow => footShadow;

        protected virtual void Awake()
        {
            if (displayRoot == null) displayRoot = transform;
            if (mainRenderer == null) mainRenderer = GetComponentInChildren<SpriteRenderer>();
            if (sockets == null) sockets = GetComponentInChildren<CharacterSockets>();
            if (footShadow == null) footShadow = GetComponentInChildren<CharacterFootShadow>();

            // 确保材质使用支持写深度的 Alpha-Test Shader
            EnsureZWriteMaterial();
        }

        private void EnsureZWriteMaterial()
        {
            if (mainRenderer != null && mainRenderer.sharedMaterial != null)
            {
                if (!mainRenderer.sharedMaterial.shader.name.Contains("ZWrite"))
                {
                    Shader zwriteShader = Shader.Find("KiHan/Render/SpriteZWriteAlphaTest");
                    if (zwriteShader != null)
                    {
                        mainRenderer.material = new Material(zwriteShader);
                    }
                }
            }
        }

        /// <summary>
        /// 瞬间同步至实体逻辑位置并重置表现状态 (如对象池唤醒时调用)
        /// </summary>
        public void SnapToEntity()
        {
            if (BindEntity == null) return;

            // X -> X, Height -> Y, Y(纵深) -> Z
            _targetWorldPos = new Vector3(
                BindEntity.pos.x,
                0f, // 根节点始终锚定在地面
                BindEntity.pos.y
            );
            transform.position = _targetWorldPos;
            _interpolateTime = 0f;
            _visualTimer = 0f;
            _visualFrameIndex = 0;
            _lastAnim = null;
            _lastAnimVersion = -1;
        }

        protected virtual void LateUpdate()
        {
            if (BindEntity == null) return;

            // 1. 计算 3D 目标世界坐标
            // 1. 根节点世界坐标始终在地面 (X -> X, 0 -> 地面, Y -> 纵深 Z)
            Vector3 logicWorldPos = new Vector3(
                BindEntity.pos.x,
                0f,
                BindEntity.pos.y
            );

            if (logicWorldPos != _targetWorldPos)
            {
                _interpolateTime = 0f;
                _targetWorldPos = logicWorldPos;
            }

            // 2. 在渲染帧平滑插值位移
            _interpolateTime += Time.deltaTime;
            float t = Mathf.Clamp01(_interpolateTime / _stepDuration);
            transform.position = Vector3.Lerp(transform.position, _targetWorldPos, t);

            // 3. 推进 2D 序列帧动画索引
            UpdateVisualFrameIndex();

            // 4. 渲染当前精灵帧与多层贴花
            RenderCurrentFrame();

            // 5. 同步面向翻转与挂点镜像
            if (sockets != null)
            {
                sockets.SetFacing(BindEntity.IsFacingLeft);
            }

            // 6. 驱动独立脚下圆盘阴影
            if (footShadow != null && BindEntity != null)
            {
                footShadow.InitShadow(BindEntity.owner);
                footShadow.UpdatePosition();
            }
        }

        protected virtual void UpdateVisualFrameIndex()
        {
            var currentAnim = BindEntity.CurrAnim;
            if (currentAnim == null || currentAnim.Steps.Count == 0) return;

            // 动画重置检测
            if (currentAnim != _lastAnim || BindEntity.AnimVersion != _lastAnimVersion)
            {
                _lastAnim = currentAnim;
                _lastAnimVersion = BindEntity.AnimVersion;
                _visualTimer = 0f;
                _visualFrameIndex = 0;
            }

            var steps = currentAnim.Steps;
            int safeIndex = Mathf.Clamp(_visualFrameIndex, 0, steps.Count - 1);
            var currentStep = steps[safeIndex];

            _visualTimer += Time.deltaTime;
            float stepDurationSec = currentStep.Duration * GameConfig.RENDER_TICK_TIME;

            if (_visualTimer >= stepDurationSec)
            {
                _visualTimer -= stepDurationSec;
                int nextIndex = _visualFrameIndex + 1;

                bool isLooping = BindEntity != null && BindEntity.ForceLoop && !BindEntity.ForceNotLoop;

                if (nextIndex < steps.Count)
                {
                    _visualFrameIndex = nextIndex;
                }
                else if (isLooping)
                {
                    _visualFrameIndex = 0;
                }
                else
                {
                    _visualFrameIndex = steps.Count - 1;
                }
            }
        }

        protected virtual void RenderCurrentFrame()
        {
            if (mainRenderer == null || BindEntity.CurrAnim == null) return;

            var frameData = BindEntity.CurrAnim.GetCurrentFrameData(_visualFrameIndex);
            if (frameData == null) return;

            float p2u = 0.01f;

            // 设置 Sprite 与面向翻转
            mainRenderer.sprite = frameData.Sprite;
            mainRenderer.flipX = BindEntity.IsFacingLeft;

            // 应用帧偏移量 (根据面向做水平镜像)
            // 应用帧偏移量与离地高度 (DisplayRoot 负责离地高度)
            float targetHeightMeters = BindEntity.height * 0.01f;
            float offX = (mainRenderer.flipX ? -frameData.Offset.x : frameData.Offset.x) * p2u;
            float offY = frameData.Offset.y * p2u + targetHeightMeters;
            displayRoot.localPosition = new Vector3(offX, offY, 0f);

            // 渲染额外附带图层
            RenderExtraLayers(frameData, p2u);
        }

        protected virtual void RenderExtraLayers(SpriteFrameData frameData, float p2u)
        {
            int layerCount = frameData.ExtraLayers.Count;
            while (extraRenderers.Count < layerCount)
            {
                GameObject layerGo = new GameObject($"ExtraLayer_{extraRenderers.Count}");
                layerGo.transform.SetParent(displayRoot);
                var sr = layerGo.AddComponent<SpriteRenderer>();
                sr.material = mainRenderer.sharedMaterial; // 共享 ZWrite 材质
                extraRenderers.Add(sr);
            }

            for (int i = 0; i < extraRenderers.Count; i++)
            {
                var sr = extraRenderers[i];
                if (i < layerCount)
                {
                    var layer = frameData.ExtraLayers[i];
                    sr.gameObject.SetActive(true);
                    sr.sprite = layer.Sprite;
                    sr.color = layer.TintColor;
                    sr.flipX = mainRenderer.flipX;
                    float lx = (sr.flipX ? -layer.Offset.x : layer.Offset.x) * p2u;
                    float ly = layer.Offset.y * p2u;
                    // 在 Z 轴上增加微小的深度偏移量代替传统的 sortingOrder
                    sr.transform.localPosition = new Vector3(lx, ly, -layer.OrderOffset * 0.001f);
                }
                else
                {
                    sr.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 将 3D 特效 (如螺旋丸网格) 或 2D 贴花直接挂接到角色指定挂点
        /// </summary>
        public void AttachEffect(GameObject fxInstance, SocketType socketType, bool followFacing = true)
        {
            if (sockets != null)
            {
                sockets.AttachEffect(fxInstance, socketType, followFacing);
            }
            else
            {
                fxInstance.transform.SetParent(this.transform, false);
                fxInstance.transform.localPosition = Vector3.zero;
            }
        }

        #region Gizmos 调试 3D 判定盒

        private void OnDrawGizmos()
        {
            if (BindEntity == null || BindEntity.CurrAnim == null) return;

            // 受击盒 (蓝色)
            var hurtBoxes = BindEntity.CurrAnim.GetHurtBoxes(_visualFrameIndex);
            if (hurtBoxes != null)
            {
                Gizmos.color = new Color(0f, 0.4f, 1f, 0.5f);
                foreach (var box in hurtBoxes) Draw3DLogicBox(box);
            }

            // 攻击盒 (红色)
            var hitBoxes = BindEntity.CurrAnim.GetHitBoxes(_visualFrameIndex);
            if (hitBoxes != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
                foreach (var box in hitBoxes) Draw3DLogicBox(box);
            }
        }

        private void Draw3DLogicBox(LogicBox box)
        {
            float p2u = 0.01f;
            float w = box.Size.x * p2u;
            float h = box.Size.y * p2u;
            float d = box.Side * p2u;
            if (d <= 0.01f) d = 0.5f;

            float dir = BindEntity.IsFacingLeft ? -1f : 1f;
            float cx = transform.position.x + (box.Center.x * p2u * dir);
            float cy = transform.position.y + (box.Center.y * p2u);
            float cz = transform.position.z;

            Gizmos.DrawWireCube(new Vector3(cx, cy, cz), new Vector3(w, h, d));
        }

        #endregion
    }
}
