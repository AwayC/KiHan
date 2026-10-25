using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// 通灵键：豆的个数 = 佩戴的通灵数，亮的个数 = 还没使用的通灵数 (豆的子节点数为显示上限)
    /// </summary>
    public class SummonButtonView : MonoBehaviour
    {
        [SerializeField] private BeanListView _beans;

        /// <param name="equipped">佩戴的通灵数</param>
        /// <param name="remaining">还没使用的通灵数</param>
        public void Show(int equipped, int remaining)
        {
            _beans.Set(equipped, remaining);
        }
    }
}
