using System.Collections.Generic;

namespace KiHan.Logic
{
    /// <summary>
    /// 角色身上的 Buff 列表 (纯确定性整数逻辑帧)：添加即新增一层，各层独立计时，到期移除
    /// 添加 / 移除通过所在世界通知表现层 (ViewEvents)
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
        /// 新增一层，返回层编号
        /// </summary>
        public int Add(BuffInfo info)
        {
            int serial = ++_nextSerial;
            _buffs.Add(new BuffInstance { Info = info, Serial = serial, RemainingTicks = info.durationTicks });
            return serial;
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
