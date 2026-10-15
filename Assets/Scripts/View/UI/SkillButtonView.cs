using KiHan.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 技能按键的图标 / CD / 接段显示 (只读本机角色的 SkillController)
    /// - 图标：显示轮到的那一段的图标 (该段未配置时用技能图标；都未配置时保留预制体原图)
    /// - CD 中：按键上覆盖径向遮罩，中央显示剩余秒数
    /// - 接段窗口中：下一段没有自己的图标时，中央显示下一段的段号
    /// - 其余时间显示固定标题 (如替身键的"替身")
    /// 图标在加载阶段预载，文本使用预生成的字符串，均只在显示值变化时更新 (0 GC)
    /// </summary>
    public class SkillButtonView : MonoBehaviour
    {
        private static readonly string[] Numbers = BuildNumbers(100);

        private CommandKey _button;
        private string _title;
        private SkillIconCache _icons;
        private Image _image;
        private Image _mask;
        private Text _label;
        private int _skill = -1;
        private int _shownSeconds = -1;
        private int _shownStage = -1;
        private string _shownIcon;

        /// <summary>
        /// 在按键上构建遮罩与文字
        /// </summary>
        public static SkillButtonView Attach(VirtualButton button, CommandKey skillButton, SkillIconCache icons, string title = "")
        {
            var view = button.gameObject.AddComponent<SkillButtonView>();
            view._button = skillButton;
            view._icons = icons;
            view._title = title ?? string.Empty;
            view._image = button.GetComponent<Image>();

            var maskGo = new GameObject("CooldownMask", typeof(RectTransform));
            maskGo.transform.SetParent(button.transform, false);
            Stretch(maskGo.GetComponent<RectTransform>());
            view._mask = maskGo.AddComponent<Image>();
            view._mask.sprite = view._image != null ? view._image.sprite : null;
            view._mask.preserveAspect = true;
            view._mask.color = new Color(0f, 0f, 0f, 0.6f);
            view._mask.type = Image.Type.Filled;
            view._mask.fillMethod = Image.FillMethod.Radial360;
            view._mask.fillOrigin = (int)Image.Origin360.Top;
            view._mask.fillClockwise = false;
            view._mask.fillAmount = 0f;
            view._mask.raycastTarget = false;

            view._label = CreateLabel(button.transform, 34);
            view._label.text = view._title;
            return view;
        }

        public void Refresh(SkillController skills)
        {
            if (_skill < 0 || _skill >= skills.Count || skills.GetSkill(_skill).button != _button)
            {
                _skill = skills.IndexOfButton(_button);
            }

            if (_skill < 0)
            {
                _mask.fillAmount = 0f;
                Show(0, 0);
                return;
            }

            SkillInfo skill = skills.GetSkill(_skill);
            int cooldown = skills.GetCooldown(_skill);
            _mask.fillAmount = skill.cooldownTicks > 0 ? (float)cooldown / skill.cooldownTicks : 0f;

            int seconds = cooldown > 0 ? (cooldown + GameConfig.LOGIC_FPS - 1) / GameConfig.LOGIC_FPS : 0;
            int stage = cooldown <= 0 ? skills.GetNextStage(_skill) : 0;
            string icon = skill.IconOfStage(stage);
            ShowIcon(icon);

            // 下一段有自己的图标时不再显示段号
            bool stageHasIcon = stage > 0 && stage < skill.stages.Length && !string.IsNullOrEmpty(skill.stages[stage].icon);
            Show(seconds, stageHasIcon ? 0 : stage);
        }

        private void ShowIcon(string address)
        {
            if (address == _shownIcon) return;
            _shownIcon = address;

            Sprite sprite = _icons != null ? _icons.Get(address) : null;
            if (sprite == null || _image == null) return;
            _image.sprite = sprite;
            _mask.sprite = sprite;
        }

        private void Show(int seconds, int nextStage)
        {
            if (seconds == _shownSeconds && nextStage == _shownStage) return;
            _shownSeconds = seconds;
            _shownStage = nextStage;

            if (seconds > 0) _label.text = Numbers[Mathf.Min(seconds, Numbers.Length - 1)];
            else if (nextStage > 0) _label.text = Numbers[Mathf.Min(nextStage + 1, Numbers.Length - 1)];
            else _label.text = _title;
        }

        internal static Text CreateLabel(Transform parent, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static string[] BuildNumbers(int count)
        {
            var numbers = new string[count];
            for (int i = 0; i < count; i++) numbers[i] = i.ToString();
            return numbers;
        }
    }
}
