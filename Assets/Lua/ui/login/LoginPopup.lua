-- 登录弹窗：用户名 / 密码 HTTP 登录，成功后关闭并回调 data.OnLoggedIn
local Panel = require "ui.base.Panel"
local PasswordVisibility = require "ui.login.PasswordVisibility"

local Ok = CS.System.Convert.ToInt32(CS.KiHan.Config.HttpErrCode.Ok)

local M = class("LoginPopup", Panel)

function M:OnCreate()
    self.passwordVisible = false
    self:OnClick(self.closeBtn, self.Close)
    self:OnClick(self.loginBtn, self.OnLoginClick)
    self:OnClick(self.passwordToggleBtn, function(s)
        s.passwordVisible = not s.passwordVisible
        PasswordVisibility.Apply(s.passwordInput, s.passwordOpen, s.passwordClose, s.passwordVisible)
    end)
    PasswordVisibility.Apply(self.passwordInput, self.passwordOpen, self.passwordClose, false)
end

function M:OnOpen(data)
    self.data = data
end

function M:OnLoginClick()
    local http = CS.Managers.HttpClient.Instance
    if not http.IsVersionValid then
        UI.Toast("版本不对，请更新游戏")
        return
    end

    local username = self.usernameInput.text
    local password = self.passwordInput.text

    if username == nil or username == "" then
        UI.Toast("用户名不能为空！")
        return
    end
    if password == nil or password == "" then
        UI.Toast("密码不能为空！")
        return
    end

    CS.UnityEngine.Debug.Log("[LoginPopup] 正在登录: " .. username)

    http:Login(username, password,
        function(res)
            if res.code == Ok then
                UI.Toast("登录成功！")
                local onLoggedIn = self.data and self.data.OnLoggedIn
                self.data = nil
                self:Close()
                if onLoggedIn then onLoggedIn() end
            else
                UI.Toast("登录失败: " .. tostring(res.msg))
            end
        end,
        function(err)
            CS.UnityEngine.Debug.LogWarning("[LoginPopup] 请求失败: " .. tostring(err))
            UI.Toast("网络问题")
        end)
end

return M
