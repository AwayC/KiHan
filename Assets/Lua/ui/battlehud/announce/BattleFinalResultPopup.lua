-- 整场结算 (Hud 层，对局结束时由 MatchDirector 打开)：data = { Result = "win" | "lose" | "draw", Perfect = 完胜 (一个都没输) }
-- 胜：win (完胜为 wenzi-wansheng)，底图 bg-win；负：lose，底图 bg-lose；平：wenzi-pinju。图在 @ResultSprites (下标见 Sprite)
-- 动画：从大缩到原大小 (回弹)；出现 ClickDelay 秒后点击任意处退出战斗 (GameFlow.ExitBattle)
local Panel = require "ui.base.Panel"
local Tween = require "ui.base.Tween"

local Sprite = { Win = 0, Draw = 1, Perfect = 2, Lose = 3, BgWin = 4, BgLose = 5 }
local InTime = 0.3
local StartScale = 3
local ClickDelay = 0.8

local M = class("BattleFinalResultPopup", Panel)

function M:OnCreate()
    self.tween = Tween.new()
    self:OnClick(self.closeArea, self.OnCloseAreaClicked)
end

function M:OnOpen(args)
    local sprites = self.resultSprites
    local result = args.Result
    local text = result == "lose" and Sprite.Lose or result == "draw" and Sprite.Draw or (args.Perfect and Sprite.Perfect or Sprite.Win)
    self.resultText.sprite = sprites:Get(text)
    self.resultText:SetNativeSize()
    self.resultBg.sprite = sprites:Get(result == "lose" and Sprite.BgLose or Sprite.BgWin)

    self.clickable = false
    local tr = self.resultBg.transform
    local tween = self.tween
    tween:Clear()
    Tween.SetScale(tr, StartScale)
    tween:Add(0, InTime, function(t) Tween.SetScale(tr, Tween.Lerp(StartScale, 1, t)) end, Tween.OutBack)
    tween:Call(ClickDelay, function() self.clickable = true end)
end

function M:OnCloseAreaClicked()
    if not self.clickable then return end
    self.clickable = false
    CS.GameFlow.Instance:ExitBattle()
end

function M:OnClose()
    self.tween:Clear()
end

function M:Update(dt)
    self.tween:Update(dt)
end

return M
