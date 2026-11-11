-- 3v3 对局演出 (战斗 HUD 持有，每帧 Update)：按对局 (C# BattleMatch) 的阶段与阶段帧数打开转场、Loading 与播报
-- 阶段与帧数由逻辑帧确定性推进，演出只读不写，联机双端一致；每个阶段内的演出各触发一次 (帧数用 >=，一帧推进多个逻辑帧也不漏)
--   Loading  结束前 WipeLead 帧转场，盖住时关闭 3v3 Loading、镜头从画面中间移向本机角色
--   Intro    回合播报 → 开战播报 (Intro 最后 StartAnnounceTicks 帧，即开战播报后 1 秒开始对战)
--   RoundEnd KO 等败者倒地后再播回合胜负；时间耗尽先播"时间耗尽"，停顿后播回合胜负 (平局为"不分胜负")
--   Swap     开始时转场，盖住时打开 1v1 Loading (下一回合双方出战者，换人在其后 SwapAtTick 执行)；结束前 WipeLead 帧转场关闭
--   Finished 整场结算 (按本机视角)
local BattleMatch = CS.KiHan.Logic.BattleMatch
local Phase = CS.KiHan.Logic.MatchPhase
local MatchResult = CS.KiHan.Logic.MatchResult
local RoundEndReason = CS.KiHan.Logic.RoundEndReason
local CharacterRegistry = CS.KiHan.Config.CharacterRegistry

local FPS = CS.KiHan.Logic.GameConfig.LOGIC_FPS
-- 转场从开始到盖满约 0.3 秒 (ScreenWipePanel.InTime)
local WipeLead = FPS * 3 // 10
local RoundPopupAt = FPS * 4 // 10
local StartPopupAt = BattleMatch.IntroTicks - BattleMatch.StartAnnounceTicks
local KoResultAt = FPS * 3 // 2
local TimeUpResultAt = FPS * 2

local M = {}
M.__index = M

function M.new()
    local self = setmetatable({ fired = {} }, M)
    self.onLoadingCovered = function()
        UI.Close("BattleLoading3v3Panel")
        CS.BattleManager.Instance:FocusCameraFromCenter()
    end
    self.onSwapCovered = function()
        local bm = CS.BattleManager.Instance
        UI.Open("BattleLoading1v1Panel", { P1 = bm:CurrentLoadout(1), P2 = bm:CurrentLoadout(2) })
    end
    self.onSwapEndCovered = function()
        UI.Close("BattleLoading1v1Panel")
        CS.BattleManager.Instance:FocusCameraFromCenter()
    end
    return self
end

function M:Start(match)
    self.match = match
    self.phase = nil
end

function M:Stop()
    self.match = nil
end

-- 本阶段内 key 是否第一次满足条件
function M:Once(key, ok)
    if not ok or self.fired[key] then return false end
    self.fired[key] = true
    return true
end

function M:Update()
    local match = self.match
    if match == nil then return end

    local phase = match.Phase
    if phase ~= self.phase then
        self.phase = phase
        local fired = self.fired
        for k in pairs(fired) do fired[k] = nil end
    end
    local tick = match.PhaseTick

    if phase == Phase.Loading then
        if self:Once("wipe", tick >= BattleMatch.LoadingTicks - WipeLead) then
            CS.GameFlow.Instance:PlayWipe(self.onLoadingCovered)
        end
    elseif phase == Phase.Intro then
        if self:Once("round", tick >= RoundPopupAt) then UI.Open("RoundPopup", match.Round) end
        if self:Once("start", tick >= StartPopupAt) then UI.Open("BattleStartPopup") end
    elseif phase == Phase.Fighting then
        -- 兜底：转场没有执行到时 (如编辑器暂停) 不让 Loading 挡住战斗
        if self:Once("loading", true) then
            UI.Close("BattleLoading3v3Panel")
            UI.Close("BattleLoading1v1Panel")
        end
    elseif phase == Phase.RoundEnd then
        local timeUp = match.EndReason == RoundEndReason.TimeUp
        if self:Once("timeEnd", timeUp) then UI.Open("BattleTimeEndPopup") end
        if self:Once("result", tick >= (timeUp and TimeUpResultAt or KoResultAt)) then self:ShowRoundResult(match) end
    elseif phase == Phase.Swap then
        if self:Once("wipeIn", true) then CS.GameFlow.Instance:PlayWipe(self.onSwapCovered) end
        if self:Once("wipeOut", tick >= BattleMatch.SwapTicks - WipeLead) then
            CS.GameFlow.Instance:PlayWipe(self.onSwapEndCovered)
        end
    elseif phase == Phase.Finished then
        if self:Once("final", true) then self:ShowFinalResult(match) end
    end
end

function M:ShowRoundResult(match)
    local result = match.RoundResult
    if result == MatchResult.Draw then
        UI.Open("BattleDrawPopup")
        return
    end
    local bm = CS.BattleManager.Instance
    local winner = result == MatchResult.P1Win and bm.P1 or bm.P2
    UI.Open("BattleResultPopup", winner ~= nil and CharacterRegistry.Find(winner.CharacterId) or nil)
end

function M:ShowFinalResult(match)
    local localSlot = CS.BattleManager.Instance.LocalSlot
    local result = match.Result
    local text
    if result == MatchResult.Draw then
        text = "draw"
    elseif (result == MatchResult.P1Win) == (localSlot == 1) then
        text = "win"
    else
        text = "lose"
    end
    UI.Open("BattleFinalResultPopup", { Result = text, Perfect = match:IsPerfectWin(localSlot) })
end

return M
