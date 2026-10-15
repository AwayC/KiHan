using UnityEngine;
using UnityEngine.UI;
using KiHan.Logic;
using System.Collections.Generic;
using Managers;

namespace View.UI
{
    /// <summary>
    /// 战斗 UI 面板管理类
    /// 自动将背景装饰图转化为功能按键，并处理图标加载
    /// </summary>
    public class BattleUIPanel : BasePanel
    {
        private static BattleUIPanel _instance;
        public static BattleUIPanel Instance => _instance;

        public VirtualButton BtnAttack { get; private set; }
        public VirtualButton BtnSkill1 { get; private set; }
        public VirtualButton BtnSkill2 { get; private set; }
        public VirtualButton BtnUltimate { get; private set; }
        public VirtualButton BtnSubstitution { get; private set; }

        /// <summary>
        /// 替身键位置 (相对 Buttons 节点)：预制体中没有替身键时复制技能 1 键放在其左侧
        /// </summary>
        private static readonly Vector2 SubstitutionPosition = new Vector2(-180f, -60f);

        /// <summary>
        /// 查克拉显示位置 (相对 Buttons 节点，奥义键上方)
        /// </summary>
        private static readonly Vector2 ChakraPosition = new Vector2(30f, 185f);

        private GameObject _backPanel;
        private readonly List<SkillButtonView> _skillViews = new List<SkillButtonView>(4);
        private ChakraView _chakraView;

        /// <summary>
        /// 本机角色的技能图标 (面板创建时按 skills.json 预载)
        /// </summary>
        private readonly SkillIconCache _icons = new SkillIconCache();

        private void Awake()
        {
            _instance = this;
            Debug.Log("[UI] BattleUIPanel 启动，执行组件绑定...");
            AutoBindManualLayout();
            BindBackPanel();
        }

        private void BindBackPanel()
        {
            _backPanel = transform.Find("BackPanel")?.gameObject ?? FindChildRecursive(transform, "BackPanel")?.gameObject;
            if (_backPanel != null)
            {
                Debug.Log("[UI] BattleUIPanel: 成功找到 BackPanel");
                Transform container = _backPanel.transform.Find("container");
                if (container != null)
                {
                    Button xBtn = container.Find("XBtn")?.GetComponent<Button>() ?? FindChildRecursive(container, "XBtn")?.GetComponent<Button>();
                    
                    Transform btnsRoot = container.Find("Btns");
                    Button cancelBtn = btnsRoot?.Find("CancelBtn")?.GetComponent<Button>() ?? FindChildRecursive(container, "CancelBtn")?.GetComponent<Button>();
                    Button confirmBtn = btnsRoot?.Find("ConfirmBtn")?.GetComponent<Button>() ?? FindChildRecursive(container, "ConfirmBtn")?.GetComponent<Button>();

                    xBtn?.onClick.AddListener(() => {
                        Debug.Log("[UI] BattleUIPanel: 点击了 BackPanel XBtn");
                        HidePopup(_backPanel);
                    });
                    cancelBtn?.onClick.AddListener(() => {
                        Debug.Log("[UI] BattleUIPanel: 点击了 BackPanel CancelBtn");
                        HidePopup(_backPanel);
                    });
                    confirmBtn?.onClick.AddListener(() => {
                        Debug.Log("[UI] BattleUIPanel: 点击了 BackPanel ConfirmBtn");
                        HidePopup(_backPanel);
                        GameApp.Instance.ExitGame();
                    });
                }
                _backPanel.SetActive(false);
            }
            else
            {
                Debug.LogError("[UI] BattleUIPanel: 未能找到 BackPanel 对象！");
            }
        }

        private void ShowPopup(GameObject popup)
        {
            UIPanelAnim.Show(this, popup);
        }

        private void HidePopup(GameObject popup)
        {
            UIPanelAnim.Hide(this, popup);
        }

        private void AutoBindManualLayout()
        {
            // 1. 寻找摇杆
            Transform joyRoot = transform.Find("JoyStick") ?? FindChildRecursive(transform, "JoyStick");
            if (joyRoot != null)
            {
                var joystick = joyRoot.gameObject.GetComponent<VirtualJoystick>() ?? joyRoot.gameObject.AddComponent<VirtualJoystick>();
                
                Transform joyBg = joyRoot.Find("JoyStick_BG") ?? joyRoot.Find("bg");
                if (joyBg != null) joystick.Bg = joyBg.GetComponent<RectTransform>();
                
                Transform joyKnob = joyRoot.Find("JoyStick_Knob") ?? joyRoot.Find("knob");
                if (joyKnob != null) joystick.Knob = joyKnob.GetComponent<RectTransform>();

                joystick.MaxRadius = 50f;
            }

            // 2. 寻找按键 (Buttons)
            Transform buttonsRoot = transform.Find("Buttons") ?? FindChildRecursive(transform, "Buttons");
            if (buttonsRoot != null)
            {
                BtnAttack = BindAction(buttonsRoot, "Attack", ButtonMask.Attack);
                BtnSkill1 = BindAction(buttonsRoot, "Button1", ButtonMask.Skill1);
                BtnSkill2 = BindAction(buttonsRoot, "Button2", ButtonMask.Skill2);
                BtnUltimate = BindAction(buttonsRoot, "Button3", ButtonMask.Ultimate);
                BtnSubstitution = BindAction(buttonsRoot, "Substitution", ButtonMask.Substitution) ?? CreateSubstitutionButton(buttonsRoot);

                CharacterObj local = BattleManager.Instance.LocalCharacter;
                if (local != null && local.Skills != null) _icons.Preload(local.Skills);

                AddSkillView(BtnSkill1, CommandKey.Skill1, string.Empty);
                AddSkillView(BtnSkill2, CommandKey.Skill2, string.Empty);
                AddSkillView(BtnUltimate, CommandKey.Ultimate, string.Empty);
                AddSkillView(BtnSubstitution, CommandKey.Substitution, "替身");
                _chakraView = ChakraView.Create(buttonsRoot, ChakraPosition);
            }

            // 3. 寻找返回主按钮 (通常在根部或 Buttons 容器内)
            Button backBtn = transform.Find("BackBtn")?.GetComponent<Button>() ?? FindChildRecursive(transform, "BackBtn")?.GetComponent<Button>();
            if (backBtn != null)
            {
                Debug.Log("[UI] BattleUIPanel: 成功绑定 BackBtn");
                backBtn.onClick.RemoveAllListeners();
                backBtn.onClick.AddListener(() => {
                    Debug.Log("[UI] BattleUIPanel: 点击了战斗返回按钮，尝试显示面板");
                    if (_backPanel != null) {
                        ShowPopup(_backPanel);
                    }
                    else
                    {
                        Debug.LogError("[UI] BattleUIPanel: BackPanel 为空，无法弹出！");
                    }
                });
            }
            else
            {
                Debug.LogError("[UI] BattleUIPanel: 未能找到 BackBtn 按钮！");
            }
        }

        private VirtualButton BindAction(Transform root, string name, ButtonMask action)
        {
            Transform target = root.Find(name) ?? FindChildRecursive(root, name);

            if (target == null) return null;

            // 基础 UGUI 兼容性设置
            var img = target.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = true;
                img.preserveAspect = true;
            }

            // 挂载脚本并初始化
            var btn = target.gameObject.GetComponent<VirtualButton>() ?? target.gameObject.AddComponent<VirtualButton>();
            btn.Action = action;
            btn.EnsureScaleInit();
            
            return btn;
        }

        /// <summary>
        /// 预制体中没有替身键时，复制技能 1 键作为替身键 (位置见 SubstitutionPosition，可在预制体中加入名为 Substitution 的按键替代)
        /// </summary>
        private VirtualButton CreateSubstitutionButton(Transform root)
        {
            if (BtnSkill1 == null) return null;

            GameObject go = Instantiate(BtnSkill1.gameObject, root);
            go.name = "Substitution";
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = SubstitutionPosition;
            return BindAction(root, "Substitution", ButtonMask.Substitution);
        }

        private void AddSkillView(VirtualButton button, CommandKey key, string title)
        {
            if (button != null) _skillViews.Add(SkillButtonView.Attach(button, key, _icons, title));
        }

        /// <summary>
        /// 只读本机角色的技能状态刷新按键 CD 与查克拉
        /// </summary>
        private void Update()
        {
            CharacterObj local = BattleManager.Instance.LocalCharacter;
            SkillController skills = local != null ? local.Skills : null;
            if (skills == null) return;

            for (int i = 0; i < _skillViews.Count; i++)
            {
                _skillViews[i].Refresh(skills);
            }
            if (_chakraView != null) _chakraView.Refresh(skills);
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
