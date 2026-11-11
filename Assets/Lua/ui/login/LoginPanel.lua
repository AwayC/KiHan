-- 登录主界面：视频背景、未登录 / 已登录两组按钮、版本校验、开始游戏 (连接网关)、退出登录、单机模式 (不登录，打开 SingleModePanel)。
-- 登录 / 注册在 LoginPopup / RegisterPopup 中完成，成功后回调本界面切到已登录状态
local Panel = require "ui.base.Panel"

local VideoFileName = "LoginTen.mp4"
local GatewayHost = "127.0.0.1"
local GatewayPort = 9000
local TipInvalidVersion = "版本不对，请更新游戏"
local TipNetworkError = "网络问题"

local InvalidVersion = CS.System.Convert.ToInt32(CS.KiHan.Config.HttpErrCode.InvalidVersion)

local M = class("LoginPanel", Panel)

function M:OnCreate()
    self:SetupVideoBackground()

    self.isLoggedIn = false
    local onLoggedIn = function() self:OnLoggedIn() end
    self.loginArgs = { OnLoggedIn = onLoggedIn }
    self.registerArgs = { OnRegistered = onLoggedIn }

    self:OnClick(self.loginBtn, function(s) UI.Open("LoginPopup", s.loginArgs) end)
    self:OnClick(self.registerBtn, function(s) UI.Open("RegisterPopup", s.registerArgs) end)
    self:OnClick(self.singleModeBtn, function() UI.Open("SingleModePanel") end)
    self:OnClick(self.startBtn, self.OnStartGameClick)
    self:OnClick(self.logoutBtn, self.OnLogoutClick)
end

function M:OnOpen()
    self.isLoggedIn = false
    self:RefreshState()

    self.videoPlayer:Play()
    self:CheckAppVersion()

    local gateway = CS.KiHan.Network.GatewayManager.Instance
    if gateway then
        self:Listen(gateway, "OnAuthSuccess", self.OnGatewayAuthSuccess)
        self:Listen(gateway, "OnAuthFailed", self.OnGatewayAuthFailed)
    end
end

function M:OnClose()
    self.videoPlayer:Stop()
end

function M:OnRelease()
    if self.videoRT then
        self.videoPlayer.targetTexture = nil
        self.videoBg.texture = nil
        self.videoRT:Release()
        CS.UnityEngine.Object.Destroy(self.videoRT)
        self.videoRT = nil
    end
end

-- 视频背景：节点 (RawImage + AspectRatioFitter + VideoPlayer) 在预制体里，这里只创建 RenderTexture 并配置播放
function M:SetupVideoBackground()
    self.videoBg.color = CS.UnityEngine.Color.white
    self.videoBg.raycastTarget = false

    -- 填满且不拉伸：视频完整拉伸到 RenderTexture，由 AspectRatioFitter 按视频原始比例还原
    self.videoFitter = self.videoBg:GetComponent(typeof(CS.UnityEngine.UI.AspectRatioFitter))
    self.videoFitter.aspectMode = CS.UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent
    self.videoFitter.aspectRatio = 16 / 9

    self.videoRT = CS.UnityEngine.RenderTexture(1920, 1080, 0)
    self.videoBg.texture = self.videoRT

    local vp = self.videoBg:GetComponent(typeof(CS.UnityEngine.Video.VideoPlayer))
    self.videoPlayer = vp
    vp.playOnAwake = false
    vp.isLooping = true
    vp.renderMode = CS.UnityEngine.Video.VideoRenderMode.RenderTexture
    vp.targetTexture = self.videoRT
    vp.aspectRatio = CS.UnityEngine.Video.VideoAspectRatio.Stretch
    vp:prepareCompleted("+", function(player) self:OnVideoPrepared(player) end)
    vp.url = CS.UnityEngine.Application.streamingAssetsPath .. "/" .. VideoFileName
end

function M:OnVideoPrepared(vp)
    if vp.height > 0 then self.videoFitter.aspectRatio = vp.width / vp.height end
end

function M:CheckAppVersion()
    CS.Managers.HttpClient.Instance:CheckVersion(CS.KiHan.Config.HttpConfig.AppVersion,
        function(res)
            if res.code == InvalidVersion then UI.Toast(TipInvalidVersion) end
        end,
        function(err)
            CS.UnityEngine.Debug.LogWarning("[LoginPanel] 版本校验请求失败(网络问题): " .. tostring(err))
            UI.Toast(TipNetworkError)
        end)
end

function M:OnLoggedIn()
    self.isLoggedIn = true
    self:RefreshState()
end

function M:RefreshState()
    local loggedIn = self.isLoggedIn
    -- 未登录时显示
    self.unloginGroup:SetActive(not loggedIn)
    self.centerBtns:SetActive(not loggedIn)
    self.rights:SetActive(not loggedIn)

    -- 登录后显示
    self.loginedGroup:SetActive(loggedIn)
    self.startBtns:SetActive(loggedIn)
end

function M:OnStartGameClick()
    -- HTTP 登录缓存下来的 Token
    local token = CS.Managers.HttpClient.Instance.Token
    if token == nil or token == "" then
        UI.Toast("没有找到 Token，请重新登录！")
        return
    end

    -- 本地测试先写死网关地址；鉴权结果由 OnGatewayAuthSuccess / OnGatewayAuthFailed 处理
    CS.KiHan.Network.GatewayManager.Instance:Connect(GatewayHost, GatewayPort, token)
end

function M:OnLogoutClick()
    self.isLoggedIn = false
    self:RefreshState()
    CS.GameFlow.Instance:ResetSession()
end

function M:OnGatewayAuthSuccess()
    UI.Toast("连接网关成功！")
    CS.GameFlow.Instance:EnterLobby()
end

function M:OnGatewayAuthFailed(msg)
    UI.Toast("连接网关失败: " .. tostring(msg))
end

return M
