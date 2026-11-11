-- 开战播报 (Hud 层，回合播报之后由 MatchDirector 打开)
-- 动画：放大 + 透明 → 原大小不透明 → 停留 → 高度压扁消失后关闭
local Panel = require "ui.base.Panel"
local Tween = require "ui.base.Tween"

local InTime = 0.2
local HoldTime = 0.5
local OutTime = 0.18
local StartScale = 2

local M = class("BattleStartPopup", Panel)

function M:OnCreate()
    self.tween = Tween.new()
end

function M:OnOpen()
    local img = self.startImg
    local tr = img.transform
    local tween = self.tween
    tween:Clear()
    Tween.SetScale(tr, StartScale)
    Tween.SetAlpha(img, 0)
    tween:Add(0, InTime, function(t)
        Tween.SetScale(tr, Tween.Lerp(StartScale, 1, t))
        Tween.SetAlpha(img, t)
    end, Tween.OutQuad)
    tween:Add(InTime + HoldTime, OutTime, function(t) Tween.SetScale(tr, 1, 1 - t) end, Tween.InQuad, function() self:Close() end)
end

function M:OnClose()
    self.tween:Clear()
end

function M:Update(dt)
    self.tween:Update(dt)
end

return M
