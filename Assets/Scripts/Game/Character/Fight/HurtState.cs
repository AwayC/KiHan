namespace KiHan.Logic
{
    /// <summary>
    /// 受击状态 (只约束受击者自身，其他角色照常行动)
    /// </summary>
    public enum HurtState
    {
        /// <summary>
        /// 非受击，可自由行动
        /// </summary>
        None,

        /// <summary>
        /// 僵直：地面受击，不能主动行动，受击动作循环播放，僵直时间到后回到受击动作的 autoNext
        /// </summary>
        Stun,

        /// <summary>
        /// 击飞 (浮空)：空中受击或被击飞，不能主动行动，按速度模型受重力下落
        /// 动作流转 float -> fall (循环)；空中受击与触地弹起从 float 的 airHitStep 切片步重播，弹不起则转倒地
        /// </summary>
        Launch,

        /// <summary>
        /// 被抓取：不能主动行动，位置锁定在抓取者锚点上，不受重力与外力影响，受击动作循环播放 (或按 grab.pose 定格)
        /// </summary>
        Grabbed,

        /// <summary>
        /// 倒地：不能主动行动，循环播放 lie，固定 lieSec 秒后起身；只有 canHitLying 的攻击能命中，带击飞速度时重新浮空
        /// </summary>
        Lying,

        /// <summary>
        /// 起身：不能主动行动，播放 standup，期间无敌 (不产生任何命中)；播完回到默认动作并追加一段无敌时间
        /// </summary>
        Standup,
    }
}
