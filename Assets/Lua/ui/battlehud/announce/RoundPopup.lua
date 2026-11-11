-- 回合播报 (Hud 层，对局开场由 MatchDirector 打开)：data 为回合数 (1~6，图在 @RoundSprites)
-- 动画：很大快速缩到原大小 → 停留 → 快速缩小消失后关闭
local Panel = require "ui.base.Panel"
local Tween = require "ui.base.Tween"

local InTime = 0.15
local HoldTime = 0.7
local OutTime = 0.15
local StartScale = 4

local M = class("RoundPopup", Panel)

function M:OnCreate()
    self.tween = Tween.new()
end

function M:OnOpen(round)
    local sprite = self.roundSprites:Get(round - 1)
    if sprite ~= nil then self.roundNumber.sprite = sprite end

    local tr = self.transform
    local tween = self.tween
    tween:Clear()
    Tween.SetScale(tr, StartScale)
    tween:Add(0, InTime, function(t) Tween.SetScale(tr, Tween.Lerp(StartScale, 1, t)) end, Tween.OutQuad)
    tween:Add(InTime + HoldTime, OutTime, function(t) Tween.SetScale(tr, 1 - t) end, Tween.InQuad, function() self:Close() end)
end

function M:OnClose()
    self.tween:Clear()
    Tween.SetScale(self.transform, 1)
end

function M:Update(dt)
    self.tween:Update(dt)
end

return M
