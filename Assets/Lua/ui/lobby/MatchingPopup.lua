-- 匹配等待弹窗：打开时发起匹配并计时，取消 / 关闭时取消匹配；
-- 匹配成功立即进入联机战斗 (不等界面动画)，1 秒后关闭自己；匹配失败自动取消
local Panel = require "ui.base.Panel"

local SearchingText = "正在寻找旗鼓相当的对手..."
local MatchedText = "匹配成功，正在进入战斗..."
local SecondSuffix = "秒"

-- 暂定角色 ID
local CharacterId = 1

local CloseDelayAfterMatched = 1
local MaxCachedSeconds = 3600
local MaxRoomId = 4294967295

local LoadedState = CS.UnityEngine.Animator.StringToHash("loaded")
local SecondTexts = {}

local function SecondsText(seconds)
    if seconds >= MaxCachedSeconds then seconds = MaxCachedSeconds - 1 end
    local s = SecondTexts[seconds]
    if s == nil then
        s = seconds .. SecondSuffix
        SecondTexts[seconds] = s
    end
    return s
end

-- uint.TryParse：非负整数且不超过 uint 上限，否则 nil
local function ParseRoomId(s)
    local n = tonumber(s)
    if n == nil or n < 0 or n > MaxRoomId or n ~= math.floor(n) then return nil end
    return math.tointeger(n)
end

local State = { Idle = 0, Matching = 1, Matched = 2 }

local M = class("MatchingPopup", Panel)

function M:OnCreate()
    self.state = State.Idle
    self:OnClick(self.cancelBtn, self.OnCancelClicked)
end

function M:OnOpen()
    local lobby = CS.KiHan.Network.LobbyManager.Instance
    if lobby == nil then return end

    self:Listen(lobby, "OnMatchNtfReceived", self.OnMatchNtf)
    self:Listen(lobby, "OnMatchCancelResponse", self.OnMatchCancelResponse)

    self.state = State.Matching
    self.elapsed = 0
    self.shownSeconds = 0
    self.info.text = SearchingText
    self.waitTime.text = SecondsText(0)

    lobby:RequestMatch(CharacterId)
end

function M:OnClose()
    local lobby = CS.KiHan.Network.LobbyManager.Instance
    if lobby == nil then return end

    -- 匹配中被关闭 (返回键 / 流程切换)：通知服务器取消，避免关闭后仍被匹配进房间
    if self.state == State.Matching then lobby:CancelMatch() end
    self.state = State.Idle
end

function M:Update(dt)
    if self.state == State.Matching then
        self.elapsed = self.elapsed + dt
        local seconds = math.floor(self.elapsed)
        if seconds ~= self.shownSeconds then
            self.shownSeconds = seconds
            self.waitTime.text = SecondsText(seconds)
        end
    elseif self.state == State.Matched then
        self.closeCountdown = self.closeCountdown - dt
        if self.closeCountdown <= 0 then
            self.state = State.Idle
            self:Close()
        end
    end
end

function M:OnCancelClicked()
    local lobby = CS.KiHan.Network.LobbyManager.Instance
    if lobby then lobby:CancelMatch() end
end

function M:OnMatchCancelResponse(errCode, success)
    if not success or self.state ~= State.Matching then return end
    self.state = State.Idle
    self:Close()
end

function M:OnMatchNtf(ntf)
    if ntf.err_code ~= 0 then
        CS.UnityEngine.Debug.LogError("[MatchingPopup] 匹配失败: " .. tostring(ntf.err_code))
        local lobby = CS.KiHan.Network.LobbyManager.Instance
        if lobby then lobby:CancelMatch() end
        return
    end

    self.state = State.Matched
    self.closeCountdown = CloseDelayAfterMatched
    self.info.text = MatchedText
    self.loading:Play(LoadedState)

    -- 立即启动战斗初始化 (注册帧同步回调)，不能等界面动画
    local roomId = ParseRoomId(ntf.room_id)
    if roomId then
        CS.GameFlow.Instance:StartOnlineMatch(roomId)
    else
        CS.UnityEngine.Debug.LogError("[MatchingPopup] 房间号无效: " .. tostring(ntf.room_id))
    end
end

return M
