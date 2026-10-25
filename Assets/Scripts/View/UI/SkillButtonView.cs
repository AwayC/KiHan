using KiHan.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 技能按键的图标 / CD / 接段显示 (只读本机角色的 SkillController)，挂在预制体的技能按键上 (与 VirtualButton、Image 同节点)
    /// - 图标：显示轮到的那一段的图标 (该段未配置时用技能图标；都未配置时保留预制体原图)
    /// - CD 中：图标变黑白，覆盖径向遮罩，中央用图片数字显示剩余秒数
    /// - 查克拉不足 (如大招) 或未配置该技能：图标变黑白，不可用
    /// - 可用：图标原色，显示就绪光圈 (可不配置)
    /// - 接段窗口中：下一段没有自己的图标时，中央显示下一段的段号
    /// 遮罩与数字在 Awake 时生成；图标在加载阶段预载，均只在显示值变化时更新 (0 GC)
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SkillButtonView : MonoBehaviour
    {
        private enum Look
        {
            Ready,
            Cooldown,
            Locked,
        }

        [Tooltip("对应 skills.json 中技能的 button")]
        [SerializeField] private CommandKey _key;

        [Tooltip("CD 秒数 / 段号使用的图片数字")]
        [SerializeField] private DigitFont _digitFont;

        [Tooltip("CD 中图标使用的材质 (黑白)")]
        [SerializeField] private Material _cooldownMaterial;

        [Tooltip("不可用 (查克拉不足 / 未配置技能) 时图标使用的材质 (黑白)")]
        [SerializeField] private Material _lockedMaterial;

        [Tooltip("可用时显示的光圈 (可不填)")]
        [SerializeField] private GameObject _readyAura;

        private SkillIconCache _icons;
        private Image _image;
        private Image _mask;
        private UIDigitRow _number;
        private int _skill = -1;
        private string _shownIcon;
        private Look _look;
        private bool _lookApplied;

        private void Awake()
        {
            _image = GetComponent<Image>();

            var maskGo = new GameObject("CooldownMask", typeof(RectTransform));
            maskGo.transform.SetParent(transform, false);
            var maskRt = (RectTransform)maskGo.transform;
            maskRt.anchorMin = Vector2.zero;
            maskRt.anchorMax = Vector2.one;
            maskRt.offsetMin = Vector2.zero;
            maskRt.offsetMax = Vector2.zero;
            _mask = maskGo.AddComponent<Image>();
            _mask.sprite = _image.sprite;
            _mask.preserveAspect = true;
            _mask.color = new Color(0f, 0f, 0f, 0.5f);
            _mask.type = Image.Type.Filled;
            _mask.fillMethod = Image.FillMethod.Radial360;
            _mask.fillOrigin = (int)Image.Origin360.Top;
            _mask.fillClockwise = false;
            _mask.fillAmount = 0f;
            _mask.raycastTarget = false;

            _number = UIDigitRow.Create(transform, _digitFont);
            _number.Show(-1);
        }

        /// <summary>
        /// 绑定本机角色的技能图标缓存 (开局时调用)
        /// </summary>
        public void Bind(SkillIconCache icons)
        {
            _icons = icons;
            _skill = -1;
            _shownIcon = null;
            _lookApplied = false;
        }

        public void Refresh(SkillController skills)
        {
            if (_skill < 0 || _skill >= skills.Count || skills.GetSkill(_skill).button != _key)
            {
                _skill = skills.IndexOfButton(_key);
            }

            if (_skill < 0)
            {
                _mask.fillAmount = 0f;
                _number.Show(-1);
                SetLook(Look.Locked);
                return;
            }

            SkillInfo skill = skills.GetSkill(_skill);
            int cooldown = skills.GetCooldown(_skill);
            _mask.fillAmount = skill.cooldownTicks > 0 ? (float)cooldown / skill.cooldownTicks : 0f;

            int stage = cooldown <= 0 ? skills.GetNextStage(_skill) : 0;
            ShowIcon(skill.IconOfStage(stage));

            if (cooldown > 0)
            {
                _number.Show((cooldown + GameConfig.LOGIC_FPS - 1) / GameConfig.LOGIC_FPS);
                SetLook(Look.Cooldown);
                return;
            }

            // 下一段有自己的图标时不再显示段号
            bool stageHasIcon = stage > 0 && stage < skill.stages.Length && !string.IsNullOrEmpty(skill.stages[stage].icon);
            _number.Show(stage > 0 && !stageHasIcon ? stage + 1 : -1);

            // 查克拉只在释放第一段时消耗
            bool affordable = stage > 0 || skills.Chakra >= skill.chakraCost;
            SetLook(affordable ? Look.Ready : Look.Locked);
        }

        private void ShowIcon(string address)
        {
            if (address == _shownIcon) return;
            _shownIcon = address;

            Sprite sprite = _icons != null ? _icons.Get(address) : null;
            if (sprite == null) return;
            _image.sprite = sprite;
            _mask.sprite = sprite;
        }

        private void SetLook(Look look)
        {
            if (_lookApplied && _look == look) return;
            _look = look;
            _lookApplied = true;

            _image.material = look == Look.Cooldown ? _cooldownMaterial : look == Look.Locked ? _lockedMaterial : null;
            if (_readyAura != null) _readyAura.SetActive(look == Look.Ready);
        }
    }
}
