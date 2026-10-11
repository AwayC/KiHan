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
        /// 击飞：空中受击，不能主动行动，按速度模型受重力下落，落地时解除并固定进入 hurt_land
        /// </summary>
        Launch,

        /// <summary>
        /// 被抓取：不能主动行动，位置锁定在抓取者锚点上，不受重力与外力影响，受击动作循环播放
        /// </summary>
        Grabbed,
    }
}
