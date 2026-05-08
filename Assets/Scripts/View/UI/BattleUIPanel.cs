using UnityEngine;
using UnityEngine.UI;
using KiHan.Logic;
using System.Linq;

namespace View.UI
{
    /// <summary>
    /// 战斗 UI 面板管理类
    /// 负责在运行时识别你摆放好的 Image 物体，并填充功能与图标
    /// </summary>
    public class BattleUIPanel : MonoBehaviour
    {
        public VirtualButton BtnAttack { get; private set; }
        public VirtualButton BtnSkill1 { get; private set; }
        public VirtualButton BtnSkill2 { get; private set; }
        public VirtualButton BtnUltimate { get; private set; }

        private void Awake()
        {
            // 自动搜索并绑定你手动摆放的物体
            AutoBindManualLayout();
        }

        private void AutoBindManualLayout()
        {
            // 1. 绑定摇杆 (新结构: JoyStick 节点挂脚本)
            Transform joyRoot = transform.Find("JoyStick");
            if (joyRoot != null)
            {
                var joystick = joyRoot.gameObject.GetComponent<VirtualJoystick>() ?? joyRoot.gameObject.AddComponent<VirtualJoystick>();

                // 查找摇杆头 (JoyStick_Knob)
                Transform joyKnob = joyRoot.Find("JoyStick_Knob");
                if (joyKnob != null) joystick.Knob = joyKnob.GetComponent<RectTransform>();

                joystick.MaxRadius = 50f;
                joystick.InitScale(); // 显式初始化缩放
            }

            // 2. 绑定你在 Buttons 节点下手动摆放的按键
            Transform buttonsRoot = transform.Find("Buttons");
            if (buttonsRoot != null)
            {
                // 注意：在预制体中，Attack 似乎是在 Buttons/ButtonTemp/Attack 路径下
                // 我们尝试先直接找子级，找不到再找孙子级
                BtnAttack = BindAction(buttonsRoot, "Attack", ButtonMask.Attack);
                if (BtnAttack == null) BtnAttack = BindAction(buttonsRoot, "ButtonTemp/Attack", ButtonMask.Attack);

                BtnSkill1 = BindAction(buttonsRoot, "Button1", ButtonMask.Skill1);
                BtnSkill2 = BindAction(buttonsRoot, "Button2", ButtonMask.Skill2);
                BtnUltimate = BindAction(buttonsRoot, "Button3", ButtonMask.Ultimate);
            }
            else
            {
                Debug.LogWarning("[UI] 找不到名为 'Buttons' 的根节点，请检查 Canvas 层级。");
            }
        }

        private VirtualButton BindAction(Transform root, string name, ButtonMask action)
        {
            Transform target = root.Find(name);
            if (target == null) return null;

            // 挂载交互脚本，会自动使用该物体上已有的 Image
            var btn = target.gameObject.GetComponent<VirtualButton>() ?? target.gameObject.AddComponent<VirtualButton>();
            btn.Action = action;
            btn.InitScale(); // 显式初始化缩放，防止 Start 延迟

            // 确保 Image 开启 Preserve Aspect 保持比例
            var img = target.GetComponent<Image>();
            if (img != null) img.preserveAspect = true;

            return btn;
        }


        /// <summary>
        /// 外部调用：根据角色 ID 替换对应的技能图片
        /// </summary>
        public void SetupIcons(int characterId)
        {
            string path = $"UI/SkillButton/{characterId}_SkillIconAtlas";
            Sprite[] allSprites = Resources.LoadAll<Sprite>(path);

            if (allSprites == null || allSprites.Length == 0)
            {
                Debug.LogWarning($"[UI] 找不到技能图集: {path}，请确保资源已移动到 Resources 目录下。");
                return;
            }

            // 替换图片，保持所有大小参数不变
            if (BtnSkill1 != null) BtnSkill1.SetIcon(allSprites.FirstOrDefault(s => s.name.EndsWith("_0")));
            if (BtnSkill2 != null) BtnSkill2.SetIcon(allSprites.FirstOrDefault(s => s.name.EndsWith("_1")));
            if (BtnUltimate != null) BtnUltimate.SetIcon(allSprites.FirstOrDefault(s => s.name.EndsWith("_2")));
            
            Debug.Log($"[UI] 已成功为角色 {characterId} 替换技能图标贴图。");
        }
    }
}
