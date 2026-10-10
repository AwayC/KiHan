using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 攻击盒 (基于逻辑帧确定性查询激活状态)
    /// </summary>
    public class AttackHitBox : MonoBehaviour
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
        /// Inspector 面板伤害倍率 (纯字符串确定性配置，如 "1.0", "1.5")
        /// </summary>
        [Tooltip("伤害倍率 (纯字符串确定性配置，如 1.0)")] public string damageRate = "1.0";

        /// <summary>
        /// 定点数确定性伤害倍率
        /// </summary>
        public FP damageRateFP => FP.TryParse(damageRate, out var rate) ? rate : FP.One;
        /// <summary>
        /// 基础优先级
        /// </summary>
        [Tooltip("基础的优先级")] public int basePriority;

        /// <summary>
        /// 临时的优先级
        /// </summary>
        private int _tempPriority = 0;

        /// <summary>
        /// 攻击盒当前的优先级
        /// </summary>
        public int Priority => _tempPriority + basePriority;

        /// <summary>
        /// 当前是否开启：彻底脱离 Unity Update 调度，在逻辑帧检测时纯确定性即时查询
        /// </summary>
        public bool Active => master != null && master.ShouldAttackBoxActive(tags);

        private void OnTriggerEnter(Collider other)
        {
            BeHitBox bhb = other.GetComponent<BeHitBox>();
            if (bhb && master && bhb.master != master)
            {
                master.OnAttackBoxHit(this, bhb);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            BeHitBox bhb = other.GetComponent<BeHitBox>();
            if (bhb && master && bhb.master != master)
            {
                master.OnAttackBoxExit(this, bhb);
            }
        }
    }
}
