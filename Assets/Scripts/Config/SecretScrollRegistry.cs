using System;
using System.Collections.Generic;
using KiHan.Asset;
using KiHan.Logic;
using UnityEngine;

namespace KiHan.Config
{
    /// <summary>
    /// 秘卷表 (Resources/Config/SecretScrollRegistry.asset)：秘卷属于玩家出战配置 (PlayerLoadout.secret)，与角色无关
    /// - 释放：按秘卷键播放角色自己的秘卷技能 (skills.json 中 button 为 Secret 的技能，动作一般是结印 reel)，
    ///   佩戴秘卷时该技能的 CD 与图标换成秘卷的，并在进入动作时给自己加秘卷的 Buff
    /// - Buff：每个秘卷自带一个 Buff (加伤害 / 加暴击 ...)，开局登记到战斗世界的 Buff 表，动作指令也能按 id 引用
    /// </summary>
    [CreateAssetMenu(fileName = "SecretScrollRegistry", menuName = "KiHan/Secret Scroll Registry")]
    public class SecretScrollRegistry : ScriptableObject
    {
        public const string ResourcePath = "Config/SecretScrollRegistry";

        /// <summary>
        /// 秘卷图标在技能图标缓存中的 key 前缀 (图标直接引用 Sprite，不走资源地址)
        /// </summary>
        public const string IconKeyPrefix = "SecretScroll/";

        public List<SecretScrollEntry> scrolls = new List<SecretScrollEntry>();

        private static SecretScrollRegistry _instance;

        /// <summary>
        /// 首次访问时加载 (秘卷表常驻)
        /// </summary>
        public static SecretScrollRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetManager.Instance.Load<SecretScrollRegistry>(ResourcePath);
                    if (_instance == null) Debug.LogError($"[SecretScrollRegistry] 找不到秘卷表: Resources/{ResourcePath}");
                }
                return _instance;
            }
        }

        /// <summary>
        /// 按 id 查找秘卷，没有 (或 id 为空) 返回 null
        /// </summary>
        public static SecretScrollEntry Find(string id)
        {
            SecretScrollRegistry registry = string.IsNullOrEmpty(id) ? null : Instance;
            if (registry == null) return null;
            foreach (SecretScrollEntry entry in registry.scrolls)
            {
                if (entry.id == id) return entry;
            }
            return null;
        }

        /// <summary>
        /// 秘卷表中是否有该 id 的 Buff
        /// </summary>
        public static bool HasBuff(string buffId)
        {
            SecretScrollRegistry registry = Instance;
            if (registry == null) return false;
            foreach (SecretScrollEntry entry in registry.scrolls)
            {
                if (entry.buff != null && entry.buff.id == buffId) return true;
            }
            return false;
        }

        /// <summary>
        /// 全部秘卷的 Buff 配置追加到 result
        /// </summary>
        public static void CollectBuffs(List<BuffInfo> result)
        {
            SecretScrollRegistry registry = Instance;
            if (registry == null) return;
            foreach (SecretScrollEntry entry in registry.scrolls)
            {
                if (entry.buff != null && !string.IsNullOrEmpty(entry.buff.id)) result.Add(entry.buff.ToRuntime());
            }
        }
    }

    [Serializable]
    public class SecretScrollEntry
    {
        public string id;

        [Tooltip("显示名")]
        public string name;

        [Tooltip("秘卷键图标")]
        public Sprite icon;

        [Tooltip("CD (字符串秒数)，替换角色秘卷技能的 CD")]
        public string cooldown;

        [Tooltip("释放时给自己加的 Buff (id 在全部秘卷与角色 buffs.json 中唯一)")]
        public BuffData buff = new BuffData();

        /// <summary>
        /// 图标在技能图标缓存中的 key
        /// </summary>
        public string IconKey => SecretScrollRegistry.IconKeyPrefix + id;
    }
}
