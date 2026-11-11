-- 回合胜负播报 (Hud 层，回合结束时由 MatchDirector 打开)：data 为本回合胜者的角色包 (C# CharacterManifest)
-- 动画：整体从大缩到原大小 → 标题 4 张字图从左到右依次弹出 → 胜者头像与名字淡入 → 停留 → 压扁消失后关闭
local Panel = require "ui.base.Panel"
local Tween = require "ui.base.Tween"

local InTime = 0.2
local TitleDelay = 0.15
local TitleStep = 0.08
local TitleTime = 0.12
local TitleScale = 1.8
local WinnerDelay = 0.6
local WinnerTime = 0.2
local OutAt = 2.1
local OutTime = 0.15
local StartScale = 1.6

local M = class("BattleResultPopup", Panel)

function M:OnCreate()
    self.tween = Tween.new()
end

function M:OnOpen(manifest)
    self.winnerCard:Bind(manifest)
    self.charName.text = manifest ~= nil and manifest.displayName or ""

    local tr = self.transform
    local tween = self.tween
    tween:Clear()
    Tween.SetScale(tr, StartScale)
    tween:Add(0, InTime, function(t) Tween.SetScale(tr, Tween.Lerp(StartScale, 1, t)) end, Tween.OutQuad)

    local titles = self.title
    for i = 1, #titles do
        local img = titles[i]
        local itr = img.transform
        Tween.SetAlpha(img, 0)
        tween:Add(TitleDelay + (i - 1) * TitleStep, TitleTime, function(t)
            Tween.SetAlpha(img, t)
            Tween.SetScale(itr, Tween.Lerp(TitleScale, 1, t))
        end, Tween.OutQuad)
    end

    local card, name = self.winnerCard.transform, self.charName
    Tween.SetScale(card, 0)
    Tween.SetAlpha(name, 0)
    tween:Add(WinnerDelay, WinnerTime, function(t)
        Tween.SetScale(card, t)
        Tween.SetAlpha(name, t)
    end, Tween.OutBack)

    tween:Add(OutAt, OutTime, function(t) Tween.SetScale(tr, 1, 1 - t) end, Tween.InQuad, function() self:Close() end)
end

function M:OnClose()
    self.tween:Clear()
    Tween.SetScale(self.transform, 1)
end

function M:Update(dt)
    self.tween:Update(dt)
end

return M
