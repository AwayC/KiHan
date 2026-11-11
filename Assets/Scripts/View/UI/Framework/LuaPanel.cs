using System;
using System.Collections.Generic;
using KiHan.Lua;
using UnityEngine;
using XLua;
using Object = UnityEngine.Object;

namespace KiHan.UI
{
    /// <summary>
    /// 面板 (全屏界面 / 弹窗 / HUD)：预制体根节点挂本组件，填 Lua 模块名、层级与缓存；由 UIManager 按地址加载、挂到层级下并驱动生命周期。
    /// - @ 节点由 KiHan/UI/生成全部绑定 写入引用表 (_bindings)，Lua 实例上以字段名访问 (@StartBtn → self.startBtn，数组从 1 起)
    /// - 生命周期转给 Lua 实例的 _Create / _Open / _Close / _Release (ui/base/Panel.lua)：实例化后 (按钮已挂 UIPressBounce) 创建一次、
    ///   每次打开 (显示动画前)、每次关闭 (隐藏动画前)、销毁前；Lua 实例定义 Update(dt) 时打开期间每帧调用
    /// - 进入 Lua 的调用出错只打日志，不打断 UIManager
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LuaPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public string name;
            public bool isArray;
            public Object[] targets;
        }

        [Tooltip("Lua 模块名，如 ui.login.LoginPanel (对应 Assets/Lua/ui/login/LoginPanel.lua)")]
        [SerializeField] private string _script;

        [SerializeField] private UILayer _layer = UILayer.Normal;

        [Tooltip("关闭后保留实例 (再次打开直接复用)；false 时关闭即销毁")]
        [SerializeField] private bool _cache = true;

        [Tooltip("由 KiHan/UI/生成全部绑定 根据 @ 节点生成，请勿手动修改")]
        [SerializeField] private Binding[] _bindings = Array.Empty<Binding>();

        private UITransition _transition;
        private bool _created;
        private Action<LuaTable> _create;
        private Action<LuaTable, object> _open;
        private Action<LuaTable> _close;
        private Action<LuaTable> _release;
        private Action<LuaTable, float> _update;
        private readonly Dictionary<string, LuaFunction> _methods = new Dictionary<string, LuaFunction>();

        public string Script => _script;
        public UILayer PanelLayer => _layer;
        public bool Cache => _cache;
        public Binding[] Bindings => _bindings;

        public bool IsOpen { get; private set; }

        /// <summary>所在层级 (创建时确定)</summary>
        public UILayer Layer { get; private set; }

        /// <summary>打开时使用的预制体地址 (UIManager 设置)</summary>
        public string Address { get; internal set; }

        /// <summary>Lua 实例</summary>
        public LuaTable Table { get; private set; }

        /// <summary>关闭自己</summary>
        public void Close() => UIManager.Instance.Close(this);

        /// <summary>调用 Lua 实例方法 self:method()</summary>
        public void Call(string method)
        {
            LuaFunction fn = Method(method);
            if (fn == null) return;
            try
            {
                fn.Action(Table);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        /// <summary>调用 Lua 实例方法 self:method(arg)</summary>
        public void Call<T>(string method, T arg)
        {
            LuaFunction fn = Method(method);
            if (fn == null) return;
            try
            {
                fn.Action(Table, arg);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        internal void Create()
        {
            if (_created) return;
            _created = true;
            Layer = _layer;
            _transition = GetComponent<UITransition>();
            Managers.UIPressBounce.AttachToButtons(gameObject);

            try
            {
                Table = LuaManager.Instance.CreatePanel(_script, this);
                if (Table == null) return;
                _create = Table.Get<Action<LuaTable>>("_Create");
                _open = Table.Get<Action<LuaTable, object>>("_Open");
                _close = Table.Get<Action<LuaTable>>("_Close");
                _release = Table.Get<Action<LuaTable>>("_Release");
                _update = Table.Get<Action<LuaTable, float>>("Update");
                _create?.Invoke(Table);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        internal void Open(object data)
        {
            IsOpen = true;
            gameObject.SetActive(true);
            if (_open != null)
            {
                try
                {
                    _open(Table, data);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
            if (_transition != null) _transition.Play(true, null);
        }

        internal void Close(Action onHidden)
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_close != null)
            {
                try
                {
                    _close(Table);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
            if (_transition != null && gameObject.activeInHierarchy)
            {
                _transition.Play(false, onHidden);
            }
            else
            {
                onHidden?.Invoke();
            }
        }

        internal void Release()
        {
            try
            {
                _release?.Invoke(Table);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            foreach (LuaFunction fn in _methods.Values) fn?.Dispose();
            _methods.Clear();
            _create = _close = _release = null;
            _open = null;
            _update = null;
            Table?.Dispose();
            Table = null;
        }

        private void Update()
        {
            if (!IsOpen || _update == null) return;
            try
            {
                _update(Table, Time.unscaledDeltaTime);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private LuaFunction Method(string method)
        {
            if (Table == null) return null;
            if (!_methods.TryGetValue(method, out LuaFunction fn))
            {
                fn = Table.Get<LuaFunction>(method);
                _methods[method] = fn;
            }
            if (fn == null) Debug.LogError($"[LuaPanel] {_script} 没有方法 {method}", this);
            return fn;
        }
    }
}
