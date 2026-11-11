-- 全局 UI 入口：创建面板实例 (C# LuaPanel 调用) 与打开 / 关闭面板
local UIDef = require "ui.UIDef"

local UIManager = CS.KiHan.UI.UIManager

local UI = {}

-- C# LuaPanel.OnCreate 调用：创建 Lua 实例并注入 @ 节点 (数组从 1 起)
function UI.CreatePanel(module, comp)
    local self = setmetatable({}, require(module))
    self.panel = comp
    self.gameObject = comp.gameObject
    self.transform = comp.transform
    local bindings = comp.Bindings
    for i = 0, bindings.Length - 1 do
        local b = bindings[i]
        local targets = b.targets
        if b.isArray then
            local arr = {}
            for j = 0, targets.Length - 1 do arr[j + 1] = targets[j] end
            self[b.name] = arr
        else
            self[b.name] = targets[0]
        end
    end
    return self
end

-- 打开面板，返回其 Lua 实例 (失败为 nil)
function UI.Open(name, data)
    local panel = UIManager.Instance:Open(UIDef[name], data)
    return panel and panel.Table
end

function UI.Close(name)
    UIManager.Instance:Close(UIDef[name])
end

function UI.IsOpen(name)
    return UIManager.Instance:IsOpen(UIDef[name])
end

-- 已创建的面板的 Lua 实例 (未创建为 nil)
function UI.Get(name)
    local panel = UIManager.Instance:Get(UIDef[name])
    return panel and panel.Table
end

-- 飘字提示 (排队依次显示)
function UI.Toast(msg)
    local t = UI.IsOpen("ToastPanel") and UI.Get("ToastPanel") or UI.Open("ToastPanel")
    if t then t:Enqueue(msg) end
end

-- 通用确认弹窗
function UI.Confirm(text, onConfirm, onCancel)
    UI.Open("ConfirmPopup", { Text = text, OnConfirm = onConfirm, OnCancel = onCancel })
end

return UI
