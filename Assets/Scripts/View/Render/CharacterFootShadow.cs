using System;
using UnityEngine;
using KiHan.Asset;

namespace KiHan.View.Render
{
    /// <summary>
    /// 独立脚下圆盘阴影管理器
    /// 职责：
    /// 1. 独立管理原版双圈旋转阴影 (shadow_1 / shadow_2) 的生命周期与显示状态
    /// 2. 支持局部开关 (每实体独立开关) 与全局开关 (画质/设置总控)
    /// 3. 支持对象池回收与动态换皮
    /// </summary>
    public class CharacterFootShadow : MonoBehaviour
    {
        /// <summary>
        /// 全局阴影总开关 (可由游戏画质设置或性能调优模块统一开启/关闭)
        /// </summary>
        public static bool GlobalShadowEnabled = true;

        [Header("阴影开关")]
        [SerializeField] private bool enableShadow = true;

        [Header("地表微小偏移 (防止 Z-Fighting)")]
        [SerializeField] private Vector3 surfaceOffset = new Vector3(0f, 0.01f, 0f);

        [Header("资源配置")]
        [SerializeField] private string shadowPathFormat = "UI/Shadow/shadow_{0}";
        [SerializeField] private GameObject customShadowPrefab;

        private GameObject _shadowInstance;
        private int _currentOwner = -1;
        private bool _isSpawned = false;

        public bool EnableShadow
        {
            get => enableShadow;
            set
            {
                enableShadow = value;
                RefreshVisibility();
            }
        }

        public bool IsSpawned => _isSpawned;

        public GameObject ShadowInstance => _shadowInstance;

        private void OnEnable()
        {
            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (_shadowInstance != null)
            {
                _shadowInstance.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            RecycleShadow();
        }

        /// <summary>
        /// 初始化并生成对应 Owner (1P/2P) 的脚下阴影圆盘
        /// </summary>
        public void InitShadow(int ownerId)
        {
            if (_currentOwner == ownerId && _shadowInstance != null)
            {
                RefreshVisibility();
                return;
            }

            // 如果已有旧阴影先回收
            RecycleShadow();

            _currentOwner = ownerId;
            if (!ShouldRenderShadow()) return;

            SpawnShadowInstance();
        }

        private bool ShouldRenderShadow()
        {
            return GlobalShadowEnabled && enableShadow && gameObject.activeInHierarchy;
        }

        private void SpawnShadowInstance()
        {
            if (_shadowInstance != null) return;

            string path = string.Format(shadowPathFormat, _currentOwner > 0 ? _currentOwner : 1);

            // 从对象池提取阴影
            if (ObjectPoolManager.Instance != null)
            {
                _shadowInstance = ObjectPoolManager.Instance.Spawn(path, transform.position, Quaternion.identity, this.transform, PoolCategory.UI);
            }
            else
            {
                _shadowInstance = Managers.ResManager.Instance.Spawn(path, transform.position, Quaternion.identity, this.transform);
            }

            if (_shadowInstance != null)
            {
                _isSpawned = true;
                _shadowInstance.transform.localPosition = surfaceOffset;

                // 挂载原版旋转特效
                var effect = _shadowInstance.GetComponent<ShadowEffect>() ?? _shadowInstance.AddComponent<ShadowEffect>();
                if (_currentOwner == 1)
                {
                    effect.Speed1 = -180f;
                    effect.Speed2 = -225f; // 1P 专属：外圈逆时针，内圈反向差速旋转
                }
                else
                {
                    effect.Speed1 = effect.Speed2 = 180f; // 2P 专属：同速旋转
                }
            }

            RefreshVisibility();
        }

        /// <summary>
        /// 刷新阴影可见性 (由开关或全局配置变更触发)
        /// </summary>
        public void RefreshVisibility()
        {
            bool shouldShow = ShouldRenderShadow();

            if (shouldShow)
            {
                if (_shadowInstance == null && _currentOwner > 0)
                {
                    SpawnShadowInstance();
                }
                else if (_shadowInstance != null)
                {
                    _shadowInstance.SetActive(true);
                    _shadowInstance.transform.localPosition = surfaceOffset;
                }
            }
            else
            {
                if (_shadowInstance != null)
                {
                    _shadowInstance.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 每帧更新阴影坐标与贴地 (保持在角色水平根节点下方)
        /// </summary>
        public void UpdatePosition()
        {
            if (_shadowInstance != null && _shadowInstance.activeSelf)
            {
                _shadowInstance.transform.localPosition = surfaceOffset;
            }
        }

        /// <summary>
        /// 回收归还阴影对象
        /// </summary>
        public void RecycleShadow()
        {
            if (_shadowInstance != null)
            {
                if (ObjectPoolManager.Instance != null)
                {
                    ObjectPoolManager.Instance.Recycle(_shadowInstance);
                }
                else
                {
                    Managers.ResManager.Instance.Recycle(_shadowInstance);
                }
                _shadowInstance = null;
                _isSpawned = false;
            }
        }
    }
}
