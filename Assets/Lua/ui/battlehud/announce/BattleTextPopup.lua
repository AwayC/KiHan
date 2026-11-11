-- 文字播报 (Hud 层)：时间耗尽 (BattleTimeEndPopup) 与不分胜负 (BattleDrawPopup) 两个预制体共用本脚本，由 MatchDirector 打开
-- 动画：很大缩到原大小 (回弹) → 停留 → 压扁消失后关闭
local Panel = require "ui.base.Panel"
local Tween = require "ui.base.Tween"

local InTime = 0.25
local HoldTime = 0.8
local OutTime = 0.15
local StartScale = 3

local M = class("BattleTextPopup", Panel)

function M:OnCreate()
    self.tween = Tween.new()
end

function M:OnOpen()
    local tr = self.img.transform
    local tween = self.tween
    tween:Clear()
    Tween.SetScale(tr, StartScale)
    tween:Add(0, InTime, function(t) Tween.SetScale(tr, Tween.Lerp(StartScale, 1, t)) end, Tween.OutBack)
    tween:Add(InTime + HoldTime, OutTime, function(t) Tween.SetScale(tr, 1, 1 - t) end, Tween.InQuad, function() self:Close() end)
end

function M:OnClose()
    self.tween:Clear()
end

function M:Update(dt)
    self.tween:Update(dt)
end

return M
