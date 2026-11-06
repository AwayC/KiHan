using System.Collections.Generic;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 角色身上的 Buff 列表 (纯确定性整数逻辑帧)：添加即新增一层，各层独立计时，到期移除；
    /// 达到叠层上限时再添加只刷新最早一层的剩余时间。添加 / 移除通过所在世界通知表现层 (BattleWorld.AddBuff / RemoveBuff)
    /// </summary>
    public class BuffController
    {
        private readonly List<BuffInstance> _buffs = new List<BuffInstance>(8);
        private int _nextSerial;

        public int Count => _buffs.Count;

        public BuffInstance this[int index] => _buffs[index];

        /// <summary>
        /// 当前持有指定 id 的层数
        /// </summary>
        public int CountOf(string id)
        {
            int n = 0;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Info.id == id) n++;
            }
            return n;
        }

        /// <summary>
        /// 指定 id 剩余最多的一层的剩余帧数，没有为 0
        /// </summary>
        public int GetRemaining(string id)
        {
            int remaining = 0;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Info.id == id && _buffs[i].RemainingTicks > remaining) remaining = _buffs[i].RemainingTicks;
            }
            return remaining;
        }

        /// <summary>
        /// 全部层的伤害加成之和
        /// </summary>
        public FP DamageRate
        {
            get
            {
                FP sum = FP.Zero;
                for (int i = 0; i < _buffs.Count; i++) sum += _buffs[i].Info.damageRate;
                return sum;
            }
        }

        /// <summary>
        /// 全部层的暴击率加成之和
        /// </summary>
        public FP CritRate
        {
            get
            {
                FP sum = FP.Zero;
                for (int i = 0; i < _buffs.Count; i++) sum += _buffs[i].Info.critRate;
                return sum;
            }
        }

        /// <summary>
        /// 配置了在按键 key 上显示、剩余最多的一层 (表现层显示用)，没有返回 false
        /// </summary>
        public bool TryGetButtonBuff(CommandKey key, out int remaining, out int total)
        {
            remaining = 0;
            total = 0;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Info.button != key || _buffs[i].RemainingTicks <= remaining) continue;
                remaining = _buffs[i].RemainingTicks;
                total = _buffs[i].TotalTicks;
            }
            return remaining > 0;
        }

        /// <summary>
        /// 添加一层，持续 ticks 帧 (&lt;= 0 时不添加)；已达叠层上限时刷新最早一层的剩余时间
        /// </summary>
        /// <param name="added">true：新增了一层 (表现层需要播放特效)；false：刷新了已有层或未添加</param>
        /// <returns>新增或刷新的层编号，未添加为 0</returns>
        public int Add(BuffInfo info, int ticks, out bool added)
        {
            added = false;
            if (ticks <= 0) return 0;

            if (info.maxStack > 0 && CountOf(info.id) >= info.maxStack)
            {
                for (int i = 0; i < _buffs.Count; i++)
                {
                    if (_buffs[i].Info.id != info.id) continue;
                    BuffInstance oldest = _buffs[i];
                    oldest.RemainingTicks = ticks;
                    oldest.TotalTicks = ticks;
                    _buffs[i] = oldest;
                    return oldest.Serial;
                }
            }

            int serial = ++_nextSerial;
            _buffs.Add(new BuffInstance { Info = info, Serial = serial, RemainingTicks = ticks, TotalTicks = ticks });
            added = true;
            return serial;
        }

        /// <summary>
        /// 移除指定 id 的全部层并通知世界
        /// </summary>
        public void Remove(string id, CharacterObj owner)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].Info.id != id) continue;
                BuffInstance buff = _buffs[i];
                _buffs.RemoveAt(i);
                owner.World?.OnBuffRemoved(owner, buff);
            }
        }

        /// <summary>
        /// 逻辑帧推进：到期的层移除并通知世界
        /// </summary>
        public void Tick(CharacterObj owner)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = _buffs[i];
                if (--buff.RemainingTicks > 0)
                {
                    _buffs[i] = buff;
                    continue;
                }

                _buffs.RemoveAt(i);
                owner.World?.OnBuffRemoved(owner, buff);
            }
        }

        /// <summary>
        /// 清空 (开局 / 复用实例时，不通知表现层)
        /// </summary>
        public void Clear()
        {
            _buffs.Clear();
            _nextSerial = 0;
        }
    }
}
