using UnityEngine;

namespace Managers
{
    public class BasePanel : MonoBehaviour
    {
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