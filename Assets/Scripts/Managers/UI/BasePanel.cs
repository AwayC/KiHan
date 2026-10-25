using UnityEngine;

namespace Managers
{
    public class BasePanel : MonoBehaviour
    {
        public virtual int SortingPriority => 0; // 0 为普通自动层级，大于 0 则为固定高层级

        /// <summary>
        /// 面板内所有 Button 统一挂上 Q 弹按压效果；子类重写时必须先调用 base.Awake()
        /// </summary>
        protected virtual void Awake()
        {
            UIPressBounce.AttachToButtons(gameObject);
        }

        public virtual void OnOpen(object data = null)
        {
            UIPanelAnim.Show(this, gameObject);
        }

        public virtual void OnClose()
        {
            UIPanelAnim.Hide(this, gameObject);
        }
    }
}
