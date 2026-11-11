-- 注册弹窗：用户名 / 密码 / 确认密码 / 邮箱 HTTP 注册 (注册成功即登录)，成功后关闭并回调 data.OnRegistered
local Panel = require "ui.base.Panel"
local PasswordVisibility = require "ui.login.PasswordVisibility"

local Ok = CS.System.Convert.ToInt32(CS.KiHan.Config.HttpErrCode.Ok)

local M = class("RegisterPopup", Panel)

function M:OnCreate()
    self.passwordVisible = false
    self.confirmPasswordVisible = false
    self:OnClick(self.closeBtn, self.Close)
    self:OnClick(self.registerBtn, self.OnRegisterClick)
    self:OnClick(self.passwordToggleBtn, function(s)
        s.passwordVisible = not s.passwordVisible
        PasswordVisibility.Apply(s.passwordInput, s.passwordOpen, s.passwordClose, s.passwordVisible)
    end)
    self:OnClick(self.confirmPasswordToggleBtn, function(s)
        s.confirmPasswordVisible = not s.confirmPasswordVisible
        PasswordVisibility.Apply(s.confirmPasswordInput, s.confirmPasswordOpen, s.confirmPasswordClose, s.confirmPasswordVisible)
    end)
    PasswordVisibility.Apply(self.passwordInput, self.passwordOpen, self.passwordClose, false)
    PasswordVisibility.Apply(self.confirmPasswordInput, self.confirmPasswordOpen, self.confirmPasswordClose, false)
end

function M:OnOpen(data)
    self.data = data
end

function M:OnRegisterClick()
    local http = CS.Managers.HttpClient.Instance
    if not http.IsVersionValid then
        UI.Toast("版本不对，请更新游戏")
        return
    end

    local username = self.usernameInput.text
    local email = self.emailInput.text
    local password = self.passwordInput.text
    local confirmPassword = self.confirmPasswordInput.text

    if username == nil or username == "" then
        UI.Toast("用户名不能为空！")
        return
    end
    if password == nil or password == "" then
        UI.Toast("密码不能为空！")
        return
    end
    if password ~= confirmPassword then
        UI.Toast("两次输入的密码不一致！")
        return
    end

    CS.UnityEngine.Debug.Log("[RegisterPopup] 正在注册: " .. username .. ", Email: " .. tostring(email))

    http:Register(username, password, email,
        function(res)
            if res.code == Ok then
                UI.Toast("注册成功！")
                local onRegistered = self.data and self.data.OnRegistered
                self.data = nil
                self:Close()
                if onRegistered then onRegistered() end
            else
                UI.Toast("注册失败: " .. tostring(res.msg))
            end
        end,
        function(err)
            CS.UnityEngine.Debug.LogWarning("[RegisterPopup] 请求失败: " .. tostring(err))
            UI.Toast("网络问题")
        end)
end

return M
