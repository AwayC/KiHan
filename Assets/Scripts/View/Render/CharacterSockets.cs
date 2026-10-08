using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiHan.View.Render
{
    public enum SocketType
    {
        Root,       // 脚底锚点 (起跳烟尘、滑步)
        Chest,      // 胸口中心 (默认受击火花、击退受力点)
        Head,       // 头部 (受击重击、眩晕特效)
        HandL,      // 左手
        HandR,      // 右手 (螺旋丸、千鸟、苦无持握点)
        Custom      // 自定义挂点
    }

    /// <summary>
    /// 2D 序列帧角色的骨骼与特效挂点管理器
    /// 自动处理角色面向翻转 (FlipX) 时的挂点对称镜像与 2D/3D 特效依附
    /// </summary>
    public class CharacterSockets : MonoBehaviour
    {
        [Serializable]
        public class SocketEntry
        {
            public SocketType type;
            public string customName;
            public Transform socketTransform;
            [HideInInspector] public Vector3 originalLocalPos;
        }

        [Header("挂点配置列表")]
        [SerializeField] private List<SocketEntry> sockets = new();

        [Header("挂点根节点 (统一翻转)")]
        [SerializeField] private Transform socketsRoot;

        private readonly Dictionary<string, Transform> _socketMap = new(StringComparer.OrdinalIgnoreCase);
        private bool _isFacingLeft = false;

        private void Awake()
        {
            if (socketsRoot == null) socketsRoot = transform;

            // 初始化记录原始相对坐标与哈希表
            for (int i = 0; i < sockets.Count; i++)
            {
                var entry = sockets[i];
                if (entry.socketTransform != null)
                {
                    entry.originalLocalPos = entry.socketTransform.localPosition;
                    string key = entry.type == SocketType.Custom ? entry.customName : entry.type.ToString();
                    if (!string.IsNullOrEmpty(key) && !_socketMap.ContainsKey(key))
                    {
                        _socketMap.Add(key, entry.socketTransform);
                    }
                }
            }
        }

        /// <summary>
        /// 更新角色面向翻转
        /// </summary>
        public void SetFacing(bool isFacingLeft)
        {
            _isFacingLeft = isFacingLeft;

            if (socketsRoot != null)
            {
                Vector3 scale = socketsRoot.localScale;
                scale.x = isFacingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                socketsRoot.localScale = scale;
            }
        }

        /// <summary>
        /// 根据挂点类型获取对应挂点 Transform
        /// </summary>
        public Transform GetSocket(SocketType type)
        {
            return GetSocket(type.ToString());
        }

        /// <summary>
        /// 根据挂点名字获取对应挂点 Transform
        /// </summary>
        public Transform GetSocket(string socketName)
        {
            if (string.IsNullOrEmpty(socketName)) return transform;
            if (_socketMap.TryGetValue(socketName, out var targetTrans))
            {
                return targetTrans;
            }
            return transform;
        }

        /// <summary>
        /// 将特效 (2D/3D) 依附到指定挂点
        /// </summary>
        /// <param name="effectGo">特效实例</param>
        /// <param name="socketType">目标挂点</param>
        /// <param name="followFacing">是否随角色翻转面向 (3D 特效若本身不需翻转可设为 false)</param>
        public void AttachEffect(GameObject effectGo, SocketType socketType, bool followFacing = true)
        {
            Transform socket = GetSocket(socketType);
            if (socket == null || effectGo == null) return;

            effectGo.transform.SetParent(socket, false);
            effectGo.transform.localPosition = Vector3.zero;

            if (!followFacing && _isFacingLeft)
            {
                // 反转本地 X 轴抵消挂点父级的镜像翻转
                Vector3 ls = effectGo.transform.localScale;
                ls.x = -ls.x;
                effectGo.transform.localScale = ls;
            }
        }
    }
}
