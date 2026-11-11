-- 单机模式 (登录界面的单机模式按钮打开，不需要登录)：训练场 / 人机对战 (先选人)
local Panel = require "ui.base.Panel"

local M = class("SingleModePanel", Panel)

function M:OnCreate()
    self:OnClick(self.closeBtn, self.Close)
    self:OnClick(self.trainingBtn, self.OnTrainingClicked)
    self:OnClick(self.vsAIBtn, self.OnVsAIClicked)
end

function M:OnTrainingClicked()
    self:Close()
    CS.GameFlow.Instance:StartTraining()
end

function M:OnVsAIClicked()
    self:Close()
    CS.GameFlow.Instance:OpenVsAIPrepare()
end

return M
