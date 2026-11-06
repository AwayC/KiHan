using KiHan.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 技能按键的图标 / CD / 接段显示 (只读本机角色的 SkillController)，挂在预制体的技能按键上 (与 VirtualButton、Image 同节点)
    /// - 图标：显示轮到的那一段的图标 (该段未配置时用技能图标；都未配置时保留预制体原图)
    /// - CD 中 (完全进入冷却、不可点击)：图标变黑白，覆盖径向遮罩，中央用图片数字显示剩余秒数
    /// - 查克拉不足 (如大招) 或未配置该技能：图标变黑白，不可用
    /// - 可用：图标原色，显示就绪光圈 (可不配置)
    /// - 接段窗口中：图标切到下一段 (该段配置了图标时)，原色可点击；只用圆形进度条 (@CDTimer) 显示接段剩余时间 (按定时器相位在逻辑帧间插值)，
    ///   中央不显示数字，避免与 CD 倒计时混淆；不在接段窗口时圆形进度条显示配置在该按键上的 Buff 剩余时间
    /// 遮罩与数字在 Awake 时生成；图标在加载阶段预载，均只在显示值变化时更新 (0 GC)
    /// CD 数字按界面根节点的尺寸显示 (抵消按键自身及父节点的缩放)，不同大小的按键 (技能键 / 秘卷键) 数字一样大
    /// </summary>
    [RequireComponent(typeof(Image))]
    public partial class SkillButtonView : KiHan.UI.UIView
    {
        private enum Look
        {
            Ready,
            Cooldown,
            Locked,
        }

        [Tooltip("对应 skills.json 中技能的 button")]
        [SerializeField] private CommandKey _key;

        [Tooltip("CD 剩余秒数使用的图片数字")]
        [SerializeField] private DigitFont _digitFont;

        [Tooltip("CD 中图标使用的材质 (黑白)")]
        [SerializeField] private Material _cooldownMaterial;

        [Tooltip("不可用 (查克拉不足 / 未配置技能) 时图标使用的材质 (黑白)")]
        [SerializeField] private Material _lockedMaterial;

        // 可用时显示的光圈 (@ReadyAura)、接段 / Buff 倒计时圆环 (@CDTimer) 可不配置 (秘卷键不显示倒计时)

        private SkillIconCache _icons;
        private Image _image;
        private Image _mask;
        private UIDigitRow _number;
        private int _skill = -1;
        private string _shownIcon;
        private Look _look;
        private bool _lookApplied;
        private bool _digitScaled;

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
            _digitScaled = false;
        }

        public void Refresh(SkillController skills, BuffController buffs)
        {
            if (!_digitScaled) ScaleDigits();
            if (_skill < 0 || _skill >= skills.Count || skills.GetSkill(_skill).button != _key)
            {
                _skill = skills.IndexOfButton(_key);
            }

            if (_skill < 0)
            {
                _mask.fillAmount = 0f;
                _number.Show(-1);
                ShowTimer(ButtonTimerView.Ratio(buffs, _key));
                SetLook(Look.Locked);
                return;
            }

            SkillInfo skill = skills.GetSkill(_skill);
            int cooldown = skills.GetCooldown(_skill);
            _mask.fillAmount = skill.cooldownTicks > 0 ? (float)cooldown / skill.cooldownTicks : 0f;

            float window = WindowRatio(skills);
            ShowTimer(window > 0f ? window : ButtonTimerView.Ratio(buffs, _key));
            ShowIcon(skills.GetIcon(_skill));

            if (cooldown > 0)
            {
                _number.Show((cooldown + GameConfig.LOGIC_FPS - 1) / GameConfig.LOGIC_FPS);
                SetLook(Look.Cooldown);
                return;
            }

            _number.Show(-1);

            // 查克拉只在释放入口动作时消耗，换段期间不需要
            bool affordable = skills.GetWindow(_skill) > 0 || skills.Chakra >= skill.chakraCost;
            SetLook(affordable ? Look.Ready : Look.Locked);
        }

        /// <summary>
        /// 数字的缩放抵消本按键相对界面根节点的缩放：DigitFont.scale 即数字在界面根节点坐标下的大小 (界面打开后布局已定时计算一次)
        /// </summary>
        private void ScaleDigits()
        {
            _digitScaled = true;
            float relative = transform.lossyScale.x / transform.root.lossyScale.x;
            _number.transform.localScale = relative > 0f ? Vector3.one / relative : Vector3.one;
        }

        /// <summary>
        /// 接段窗口剩余比例 (没有换段为 0)：剩余逻辑帧减去本帧已走过的进度，再除以本次换段的时限总长
        /// </summary>
        private float WindowRatio(SkillController skills)
        {
            int window = skills.GetWindow(_skill);
            int total = skills.GetWindowTotal(_skill);
            if (window <= 0 || total <= 0) return 0f;
            return (window - LockstepManager.Instance.TickAlpha) / total;
        }

        private void ShowTimer(float ratio)
        {
            if (_cDTimer != null) _cDTimer.Show(ratio);
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
