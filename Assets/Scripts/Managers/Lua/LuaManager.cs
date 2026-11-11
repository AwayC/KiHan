using System;
using KiHan.Asset;
using KiHan.UI;
using UnityEngine;
using XLua;

namespace KiHan.Lua
{
    /// <summary>
    /// Lua 虚拟机：脚本按地址 "Lua/a/b/c" 经 AssetManager 加载 (编辑器读 Assets/Lua，AB 模式读 lua 包，可热更)。
    /// 不显式 Dispose：仍有 Lua 回调挂在 C# 委托上时 Dispose 会抛异常，进程退出 / 域重载时自然回收。
    /// </summary>
    public sealed class LuaManager : UnitySingleton<LuaManager>
    {
        private const float TickInterval = 1f;

        private float _nextTick;
        private Func<string, LuaPanel, LuaTable> _createPanel;

        public LuaEnv Env { get; private set; }

        public void Init()
        {
            if (Env != null) return;
            Env = new LuaEnv();
            Env.AddLoader(Load);
            Env.DoString("require 'main'", "@boot");
            _createPanel = Env.Global.GetInPath<Func<string, LuaPanel, LuaTable>>("UI.CreatePanel");
        }

        /// <summary>
        /// 创建面板的 Lua 实例 (ui/UI.lua 的 UI.CreatePanel)
        /// </summary>
        public LuaTable CreatePanel(string module, LuaPanel panel) => _createPanel(module, panel);

        private static byte[] Load(ref string filepath)
        {
            string module = filepath.Replace('.', '/');
            var asset = AssetManager.Instance.TryLoad<TextAsset>(AssetAddress.LuaPrefix + module);
            if (asset == null) return null;
            filepath = AssetAddress.LuaRoot + module + ".lua";
            return asset.bytes;
        }

        private void Update()
        {
            if (Env == null || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + TickInterval;
            Env.Tick();
        }
    }
}
