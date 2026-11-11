-- 大厅：玩家昵称 / UID、胜率 / 胜场 / 在线人数 (LobbyManager 回调刷新)，开始匹配、训练场、人机对战 (先选人)，返回 / 关闭确认后登出
-- - 需要创建角色时打开 CreateRolePopup，开始匹配打开 MatchingPopup
-- - 文本只在数值变化时刷新
local Panel = require "ui.base.Panel"

local LogoutConfirmText = "确定返回登录界面吗？"
local WinCountSuffix = "场"

local mathx = require "core.mathx"
local RateTexts = {}

local M = class("LobbyPanel", Panel)

function M:OnCreate()
    self.shownOnlineCount = -1
    self.shownWinCount = -1
    self.shownWinRate = -1
    self.shownUid = nil
    self.shownNickname = nil

    self:OnClick(self.closeBtn, self.OnBackClicked)
    self:OnClick(self.backBtn, self.OnBackClicked)
    self:OnClick(self.startBtn, self.OnStartMatchClicked)
    self:OnClick(self.offlineStartBtn, self.OnOfflineStartClicked)
    self:OnClick(self.vsAIBtn, function() CS.GameFlow.Instance:OpenVsAIPrepare() end)
end

function M:OnOpen()
    local lobby = CS.KiHan.Network.LobbyManager.Instance
    if lobby == nil then return end

    self:Listen(lobby, "OnPlayerDataUpdated", self.OnPlayerDataUpdated)
    self:Listen(lobby, "OnCreateRoleRequired", self.OnCreateRoleRequired)
    self:Listen(lobby, "OnOnlineCountUpdated", self.OnOnlineCountUpdated)

    -- 已有缓存数据时直接刷新一次
    if lobby.MyPlayerInfo ~= nil then self:OnPlayerDataUpdated(lobby.MyPlayerInfo) end

    -- 主动拉取在线人数
    lobby:RequestGetOnlineCount()

    -- 打开大厅前就已经收到了创建角色通知
    if lobby.NeedsCreateRole then self:OnCreateRoleRequired() end
end

-- 数据回调

function M:OnPlayerDataUpdated(info)
    if info == nil then return end
    self:SetProfile(info.nickname, info.uid)

    local total, win = 0, 0
    local dataJson = info.data_json
    if dataJson ~= nil and dataJson ~= "" then
        local ok, data = pcall(json.decode, dataJson)
        if ok and type(data) == "table" then
            total = tonumber(data.total_battle_count) or 0
            win = tonumber(data.win_count) or 0
        else
            CS.UnityEngine.Debug.LogError("[LobbyPanel] 解析玩家数据失败: " .. tostring(data))
        end
    end

    self:SetWinCount(win)
    self:SetWinRate(total > 0 and mathx.RoundToInt(win / total * 100) or 0)
end

function M:OnCreateRoleRequired()
    UI.Open("CreateRolePopup")
end

function M:OnOnlineCountUpdated(count)
    if count == self.shownOnlineCount then return end
    self.shownOnlineCount = count
    self.onlineCnt.text = tostring(count)
end

-- 显示

function M:SetProfile(nickname, uid)
    if nickname ~= self.shownNickname then
        self.shownNickname = nickname
        self.nickname.text = nickname or ""
    end

    if uid ~= self.shownUid then
        self.shownUid = uid
        self.uid.text = tostring(uid)
    end
end

function M:SetWinCount(count)
    if count == self.shownWinCount then return end
    self.shownWinCount = count
    self.winCnt.text = tostring(math.tointeger(count) or count) .. WinCountSuffix
end

function M:SetWinRate(percent)
    percent = mathx.Clamp(percent, 0, 100)
    if percent == self.shownWinRate then return end
    self.shownWinRate = percent
    local s = RateTexts[percent]
    if s == nil then
        s = percent .. "%"
        RateTexts[percent] = s
    end
    self.winRate.text = s
end

-- 按钮

function M:OnBackClicked()
    UI.Confirm(LogoutConfirmText, function() CS.GameFlow.Instance:Logout() end)
end

function M:OnStartMatchClicked()
    UI.Open("MatchingPopup")
end

function M:OnOfflineStartClicked()
    CS.GameFlow.Instance:StartTraining()
end

return M
