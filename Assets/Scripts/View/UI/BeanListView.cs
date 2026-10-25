using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// 一排豆：子节点上的 LightIcon 按层级顺序排列 (增减豆只需在预制体里增减子节点)
    /// 前 lit 颗为亮，其余为灭；亮的颗数达到满豆阈值时，亮着的豆显示满豆图。显示个数超过子节点数时只显示子节点数个；只在数值变化时修改节点
    /// </summary>
    public class BeanListView : MonoBehaviour
    {
        [Tooltip("亮的颗数达到此值时亮着的豆显示满豆图 (如查克拉 4)；0 表示全部点亮时才算满")]
        [SerializeField] private int _fullThreshold;

        private LightIcon[] _items;
        private int _count = -1;
        private int _lit = -1;

        /// <summary>
        /// 最多能显示的豆数
        /// </summary>
        public int Capacity => Items.Length;

        private LightIcon[] Items
        {
            get
            {
                if (_items == null) _items = GetComponentsInChildren<LightIcon>(true);
                return _items;
            }
        }

        /// <param name="count">显示的豆数 (其余隐藏)</param>
        /// <param name="lit">其中亮着的个数</param>
        public void Set(int count, int lit)
        {
            LightIcon[] items = Items;
            count = Mathf.Clamp(count, 0, items.Length);
            lit = Mathf.Clamp(lit, 0, count);
            if (count == _count && lit == _lit) return;

            bool full = _fullThreshold > 0 ? lit >= _fullThreshold : count > 0 && lit == count;
            BeanState litState = full ? BeanState.Full : BeanState.On;
            for (int i = 0; i < items.Length; i++)
            {
                // 超出个数的豆只关掉图片，节点仍参与布局：排版间距与编辑器中一致，不随个数重新排列
                items[i].SetState(i >= count ? BeanState.Hidden : i < lit ? litState : BeanState.Off);
            }
            _count = count;
            _lit = lit;
        }
    }
}
