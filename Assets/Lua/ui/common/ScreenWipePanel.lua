-- 转场 (System 层，GameFlow.PlayWipe / 对局播报调用 Play)：occlusion 从屏幕左侧外扫入盖满整屏 → 回调 onCovered (切换界面) → 继续向右扫出后关闭
-- 扫过期间挡住点击。occlusion 中间实心部分约占图宽 40%，图宽取屏宽的 WidthRatio 倍保证盖满
-- 上一次还没盖满时再次 Play：先执行上一次的 onCovered，再从头播放
local Panel = require "ui.base.Panel"
local Tween = require "ui.base.Tween"

local InTime = 0.3
local HoldTime = 0.1
local OutTime = 0.3
local WidthRatio = 2.6

local Vector2 = CS.UnityEngine.Vector2

local M = class("ScreenWipePanel", Panel)

function M:OnCreate()
    self.tween = Tween.new()
    self.onCovered = nil
end

function M:Play(onCovered)
    self:FlushCovered()
    self.onCovered = onCovered

    local screenWidth = self.transform.rect.width
    local rt = self.wipe.rectTransform
    local width = screenWidth * WidthRatio
    rt.sizeDelta = Vector2(width, 0)
    local travel = (width + screenWidth) * 0.5
    Tween.SetX(rt, -travel)

    local tween = self.tween
    tween:Clear()
    tween:Add(0, InTime, function(t) Tween.SetX(rt, -travel * (1 - t)) end, Tween.OutQuad, function() self:FlushCovered() end)
    tween:Add(InTime + HoldTime, OutTime, function(t) Tween.SetX(rt, travel * t) end, Tween.InQuad, function() self:Close() end)
end

function M:FlushCovered()
    local fn = self.onCovered
    if fn == nil then return end
    self.onCovered = nil
    fn()
end

function M:OnClose()
    self.tween:Clear()
    self:FlushCovered()
end

function M:Update(dt)
    self.tween:Update(dt)
end

return M
