using UnityEngine;
using System.Collections.Generic;
using KiHan.Logic;
using View;

namespace Managers
{
    /// <summary>
    /// 表现层实体管理器
    /// 维护逻辑实体与表现层根节点（ViewRoot）的映射关系
    /// 用于特效挂载和视觉同步
    /// </summary>
    public class ViewManager : UnitySingleton<ViewManager>
    {
        private Dictionary<LogicEntity, Transform> _entityToViewMap = new Dictionary<LogicEntity, Transform>();
        private Dictionary<LogicEntity, EntityView> _entityToEntityViewMap = new Dictionary<LogicEntity, EntityView>();

        /// <summary>
        /// 注册一个逻辑实体对应的表现层根节点
        /// （表现层根节点负责随逻辑平滑移动和翻转）
        /// </summary>
        public void RegisterView(LogicEntity entity, Transform displayRoot, EntityView entityView = null)
        {
            if (entity != null && displayRoot != null)
            {
                _entityToViewMap[entity] = displayRoot;
                if (entityView != null)
                {
                    _entityToEntityViewMap[entity] = entityView;
                }
            }
        }

        public void UnregisterView(LogicEntity entity)
        {
            if (entity != null)
            {
                _entityToViewMap.Remove(entity);
                _entityToEntityViewMap.Remove(entity);
            }
        }

        /// <summary>
        /// 获取实体的表现层根节点
        /// </summary>
        public Transform GetViewRoot(LogicEntity entity)
        {
            if (entity != null && _entityToViewMap.TryGetValue(entity, out Transform root))
            {
                return root;
            }
            return null;
        }

        /// <summary>
        /// 获取实体的表现层脚本
        /// </summary>
        public EntityView GetEntityView(LogicEntity entity)
        {
            if (entity != null && _entityToEntityViewMap.TryGetValue(entity, out EntityView view))
            {
                return view;
            }
            return null;
        }

        public void ClearAll()
        {
            foreach (var view in _entityToEntityViewMap.Values)
            {
                if (view != null && view.gameObject != null)
                {
                    Destroy(view.gameObject);
                }
            }
            _entityToViewMap.Clear();
            _entityToEntityViewMap.Clear();
        }
    }
}
