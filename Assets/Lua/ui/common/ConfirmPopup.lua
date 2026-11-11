-- 通用确认弹窗 (UI.Confirm / UIManager.Confirm)：确认 / 取消 / 右上角关闭 (等同取消)
-- data：Text、OnConfirm、OnCancel (Lua 函数或 C# 委托，可为空)
local Panel = require "ui.base.Panel"

local M = class("ConfirmPopup", Panel)

function M:OnCreate()
    self:OnClick(self.confirmBtn, function(s) s:Finish("OnConfirm") end)
    self:OnClick(self.cancelBtn, function(s) s:Finish("OnCancel") end)
    self:OnClick(self.closeBtn, function(s) s:Finish("OnCancel") end)
end

function M:OnOpen(data)
    self.data = data
    self.text.text = data and data.Text or ""
end

function M:Finish(key)
    local callback = self.data and self.data[key]
    self.data = nil
    self:Close()
    if callback then callback() end
end

return M
