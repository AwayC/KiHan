using KiHan.Logic;
using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// 非技能按键 (普攻等) 的 Buff 倒计时显示：挂在按键节点上 (与 VirtualButton 同节点)，用子节点 @CDTimer 圆形进度条显示
    /// 配置了在该按键上显示 (buffs 的 button) 的 Buff 的剩余时间 (只读本机角色的 Buff，按定时器相位在逻辑帧间插值)；没有时隐藏
    /// 技能按键由 SkillButtonView 显示 (接段窗口优先，没有接段时显示 Buff)
    /// </summary>
    public partial class ButtonTimerView : KiHan.UI.UIView
    {
        [Tooltip("显示配置在哪个按键上的 Buff")]
        [SerializeField] private CommandKey _key = CommandKey.Attack;

        public void Refresh(BuffController buffs)
        {
            _cDTimer.Show(Ratio(buffs, _key));
        }

        /// <summary>
        /// 配置在按键上、剩余最多的一层 Buff 的剩余比例 (没有为 0)：剩余逻辑帧减去本帧已走过的进度，再除以该层的总时长
        /// </summary>
        public static float Ratio(BuffController buffs, CommandKey key)
        {
            if (buffs == null || !buffs.TryGetButtonBuff(key, out int remaining, out int total) || total <= 0) return 0f;
            return (remaining - LockstepManager.Instance.TickAlpha) / total;
        }
    }
}
