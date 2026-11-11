using KiHan.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 替身键：替身消耗查克拉，豆的个数 = 查克拉上限够替身几次，亮的个数 = 当前查克拉够替身几次
    /// (豆的子节点数为显示上限，超过只显示上限个)；CD 结束且查克拉足够时显示可用图标
    /// CD 中与技能键一样：图标上覆盖径向遮罩，中央用图片数字显示剩余秒数 (遮罩与数字在 Awake 时生成，只在显示值变化时更新)
    /// </summary>
    public partial class SubstitutionButtonView : KiHan.UI.UIView
    {
        // @Ready：替身可用时显示的图标 (叠在不可用图标上方)，CD 遮罩与数字放在它的父节点 (图标区域) 上

        [Tooltip("CD 剩余秒数使用的图片数字 (与技能键相同)")]
        [SerializeField] private DigitFont _digitFont;

        private int _skill = -1;
        private Image _mask;
        private UIDigitRow _number;
        private bool _digitScaled;

        private void Awake()
        {
            Transform icon = _ready.transform.parent;
            var maskGo = new GameObject("CooldownMask", typeof(RectTransform));
            maskGo.transform.SetParent(icon, false);
            var maskRt = (RectTransform)maskGo.transform;
            RectTransform readyRt = _ready.rectTransform;
            maskRt.anchorMin = readyRt.anchorMin;
            maskRt.anchorMax = readyRt.anchorMax;
            maskRt.anchoredPosition = readyRt.anchoredPosition;
            maskRt.sizeDelta = readyRt.sizeDelta;
            _mask = maskGo.AddComponent<Image>();
            _mask.sprite = _ready.sprite;
            _mask.preserveAspect = true;
            _mask.color = new Color(0f, 0f, 0f, 0.5f);
            _mask.type = Image.Type.Filled;
            _mask.fillMethod = Image.FillMethod.Radial360;
            _mask.fillOrigin = (int)Image.Origin360.Top;
            _mask.fillClockwise = false;
            _mask.fillAmount = 0f;
            _mask.raycastTarget = false;

            _number = UIDigitRow.Create(icon, _digitFont);
            _number.transform.localPosition = readyRt.localPosition;
            _number.Show(-1);
        }

        public void Refresh(SkillController skills)
        {
            if (!_digitScaled) ScaleDigits();
            if (_skill < 0 || _skill >= skills.Count || skills.GetSkill(_skill).button != CommandKey.Substitution)
            {
                _skill = skills.IndexOfButton(CommandKey.Substitution);
            }

            if (_skill < 0)
            {
                _beans.Set(0, 0);
                ShowCooldown(0, 0);
                SetReady(false);
                return;
            }

            SkillInfo skill = skills.GetSkill(_skill);
            int cost = skill.chakraCost;
            // 不消耗查克拉时次数不受限，豆全亮
            int max = cost > 0 ? skills.ChakraMax / cost : _beans.Capacity;
            int usable = cost > 0 ? skills.Chakra / cost : max;
            _beans.Set(max, usable);

            int cooldown = skills.GetCooldown(_skill);
            ShowCooldown(cooldown, skill.cooldownTicks);
            SetReady(cooldown == 0 && usable > 0);
        }

        private void ShowCooldown(int cooldown, int total)
        {
            float fill = total > 0 && cooldown > 0 ? (float)cooldown / total : 0f;
            if (_mask.fillAmount != fill) _mask.fillAmount = fill;
            _number.Show(cooldown > 0 ? (cooldown + GameConfig.LOGIC_FPS - 1) / GameConfig.LOGIC_FPS : -1);
        }

        /// <summary>
        /// 数字的缩放抵消本按键相对界面根节点的缩放：数字在不同按键上一样大 (界面打开后布局已定时计算一次)
        /// </summary>
        private void ScaleDigits()
        {
            _digitScaled = true;
            Transform t = _number.transform;
            float relative = t.parent.lossyScale.x / transform.root.lossyScale.x;
            t.localScale = relative > 0f ? Vector3.one / relative : Vector3.one;
        }

        private void SetReady(bool ready)
        {
            if (_ready.enabled != ready) _ready.enabled = ready;
        }
    }
}
