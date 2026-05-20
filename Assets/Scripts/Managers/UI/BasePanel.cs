using UnityEngine;

namespace Managers
{
    public class BasePanel : MonoBehaviour
    {
        public virtual int SortingPriority => 0; // 0 为普通自动层级，大于 0 则为固定高层级

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