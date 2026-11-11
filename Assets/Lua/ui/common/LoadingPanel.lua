-- 流程切换 Loading (GameFlow.RunTransition)：进度条与百分比 (百分比字符串按需生成后缓存)
local Panel = require "ui.base.Panel"

local mathx = require "core.mathx"
local Percents = {}

local function PercentText(percent)
    local s = Percents[percent]
    if s == nil then
        s = percent .. "%"
        Percents[percent] = s
    end
    return s
end

local M = class("LoadingPanel", Panel)

function M:OnOpen()
    self.shown = -1
    self:SetProgress(0)
end

-- progress：0 ~ 1
function M:SetProgress(progress)
    local percent = mathx.Clamp(mathx.RoundToInt(progress * 100), 0, 100)
    if percent == self.shown then return end
    self.shown = percent
    self.slider.value = percent / 100
    self.percent.text = PercentText(percent)
end

return M
