using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 受击盒 (基于逻辑帧确定性查询激活状态)
    /// </summary>
    public class BeHitBox : MonoBehaviour
    {
        /// <summary>
        /// 盒子的标签分类
        /// </summary>
        [Tooltip("盒子的tag")] public string[] tags;

        /// <summary>
        /// 这个框的主人
        /// </summary>
        [HideInInspector] public CharacterObj master;

        /// <summary>
        /// 基础的优先级
        /// </summary>
        [Tooltip("基础的优先级")] public int basePriority;

        /// <summary>
        /// 临时的优先级
        /// </summary>
        private int _tempPriority = 0;

        /// <summary>
        /// 受击盒当前的优先级
        /// </summary>
        public int Priority => _tempPriority + basePriority;

        /// <summary>
        /// 当前是否开启：彻底脱离 Unity Update 调度，在逻辑帧检测时纯确定性即时查询
        /// </summary>
        public bool Active => master != null && master.ShouldBeHitBoxActive(tags);

        /// <summary>
        /// 是否有一个 tag 处于 checkTags 激活列表中
        /// </summary>
        public bool TagHit(List<string> checkTags)
        {
            if (tags == null || checkTags == null) return false;
            foreach (string t in tags)
            {
                if (checkTags.Contains(t)) return true;
            }
            return false;
        }
    }
}
