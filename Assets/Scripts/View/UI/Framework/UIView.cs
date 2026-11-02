using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 可复用部件基类 (如 HpBarView、SkillButtonView)：对外只提供 Bind / Refresh 等少量入口，外部不操作其子节点。
    /// 也是自动绑定的边界：面板生成绑定时不深入部件内部，部件自己的 @ 节点由部件的 .Bind.cs 绑定
    /// </summary>
    public abstract class UIView : MonoBehaviour
    {
    }
}
