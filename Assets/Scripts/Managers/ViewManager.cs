using System.Collections.Generic;
using KiHan.View.Render;

namespace Managers
{
    /// <summary>
    /// 表现层实体管理器：逻辑实体 EntityId -> 角色视图
    /// </summary>
    public class ViewManager : UnitySingleton<ViewManager>
    {
        private readonly Dictionary<int, ActorView> _views = new Dictionary<int, ActorView>();

        public Dictionary<int, ActorView>.ValueCollection Views => _views.Values;

        public void Register(ActorView view)
        {
            if (view != null && view.Character != null)
            {
                _views[view.Character.EntityId] = view;
            }
        }

        public ActorView GetView(int entityId)
        {
            return _views.TryGetValue(entityId, out ActorView view) ? view : null;
        }

        /// <summary>
        /// 注销视图 (实体移出战场，视图由调用方隐藏或复用)
        /// </summary>
        public ActorView Unregister(int entityId)
        {
            if (!_views.TryGetValue(entityId, out ActorView view)) return null;
            _views.Remove(entityId);
            return view;
        }

        /// <summary>
        /// 归还视图占用的池化资源并销毁全部视图
        /// </summary>
        public void ClearAll()
        {
            foreach (ActorView view in _views.Values)
            {
                if (view == null) continue;
                view.Release();
                Destroy(view.gameObject);
            }
            _views.Clear();
        }
    }
}
