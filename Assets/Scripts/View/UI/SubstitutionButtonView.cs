using KiHan.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 替身键：替身消耗查克拉，豆的个数 = 查克拉上限够替身几次，亮的个数 = 当前查克拉够替身几次
    /// (豆的子节点数为显示上限，超过只显示上限个)；CD 结束且查克拉足够时显示可用图标
    /// </summary>
    public class SubstitutionButtonView : MonoBehaviour
    {
        [Tooltip("替身可用时显示的图标 (叠在不可用图标上方)")]
        [SerializeField] private Image _ready;

        [SerializeField] private BeanListView _beans;

        private int _skill = -1;

        public void Refresh(SkillController skills)
        {
            if (_skill < 0 || _skill >= skills.Count || skills.GetSkill(_skill).button != CommandKey.Substitution)
            {
                _skill = skills.IndexOfButton(CommandKey.Substitution);
            }

            if (_skill < 0)
            {
                _beans.Set(0, 0);
                SetReady(false);
                return;
            }

            int cost = skills.GetSkill(_skill).chakraCost;
            // 不消耗查克拉时次数不受限，豆全亮
            int max = cost > 0 ? skills.ChakraMax / cost : _beans.Capacity;
            int usable = cost > 0 ? skills.Chakra / cost : max;
            _beans.Set(max, usable);
            SetReady(skills.GetCooldown(_skill) == 0 && usable > 0);
        }

        private void SetReady(bool ready)
        {
            if (_ready.enabled != ready) _ready.enabled = ready;
        }
    }
}
