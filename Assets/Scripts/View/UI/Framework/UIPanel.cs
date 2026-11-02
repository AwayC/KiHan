using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 面板基类 (全屏界面 / 弹窗 / HUD)：由 UIManager 加载、挂到层级下并驱动生命周期，面板自己不控制显示隐藏
    /// - OnCreate：实例化后只调一次 (节点绑定已就绪，按钮已挂 UIPressBounce)
    /// - OnOpen：每次打开 (显示动画前)
    /// - OnClose：每次关闭 (隐藏动画前)
    /// - OnRelease：实例销毁前
    /// 带参数打开的面板继承 UIPanel&lt;TData&gt;；类需声明为 partial (节点字段由 KiHan/UI/生成绑定 生成)
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class UIPanel : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 所在层级 (打开时由 UIPanelAttribute 决定)
        /// </summary>
        public UILayer Layer { get; private set; }

        private UITransition _transition;
        private bool _created;

        protected virtual void OnCreate() { }
        protected virtual void OnOpen() { }
        protected virtual void OnClose() { }
        protected virtual void OnRelease() { }

        /// <summary>
        /// 关闭自己
        /// </summary>
        protected void Close() => UIManager.Instance.Close(this);

        internal void Create(UILayer layer)
        {
            if (_created) return;
            _created = true;
            Layer = layer;
            _transition = GetComponent<UITransition>();
            Managers.UIPressBounce.AttachToButtons(gameObject);
            OnCreate();
        }

        internal void Open(object data)
        {
            IsOpen = true;
            gameObject.SetActive(true);
            DispatchOpen(data);
            if (_transition != null) _transition.Play(true, null);
        }

        internal void Close(System.Action onHidden)
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnClose();
            if (_transition != null && gameObject.activeInHierarchy)
            {
                _transition.Play(false, onHidden);
            }
            else
            {
                onHidden?.Invoke();
            }
        }

        internal void Release() => OnRelease();

        private protected virtual void DispatchOpen(object data) => OnOpen();
    }

    /// <summary>
    /// 带参数打开的面板
    /// </summary>
    public abstract class UIPanel<TData> : UIPanel
    {
        protected abstract void OnOpen(TData data);

        private protected sealed override void DispatchOpen(object data) => OnOpen(data is TData d ? d : default);
    }
}
