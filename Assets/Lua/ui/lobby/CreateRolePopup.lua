-- 创建角色弹窗：输入昵称 → LobbyManager.RequestCreateRole，成功后关闭，失败飘字提示
local Panel = require "ui.base.Panel"

local EmptyNicknameTip = "昵称不能为空！"
local CreatingTip = "正在创建角色..."

local function Trim(s)
    return (s:gsub("^%s+", ""):gsub("%s+$", ""))
end

local M = class("CreateRolePopup", Panel)

function M:OnCreate()
    self:OnClick(self.createBtn, self.OnCreateClicked)
end

function M:OnOpen()
    local lobby = CS.KiHan.Network.LobbyManager.Instance
    if lobby == nil then return end
    self:Listen(lobby, "OnCreateRoleResponse", self.OnCreateRoleResponse)
end

function M:OnCreateClicked()
    local nickname = Trim(self.nicknameInput.text or "")
    if nickname == "" then
        UI.Toast(EmptyNicknameTip)
        return
    end

    UI.Toast(CreatingTip)
    local lobby = CS.KiHan.Network.LobbyManager.Instance
    if lobby then lobby:RequestCreateRole(nickname) end
end

function M:OnCreateRoleResponse(errCode)
    if errCode == 0 then
        self:Close()
        return
    end

    CS.UnityEngine.Debug.LogError("[CreateRolePopup] 创角失败，错误码: " .. tostring(errCode))
    UI.Toast("创角失败(" .. tostring(errCode) .. ")")
end

return M
