-- 训练场面板 (Hud 层，叠在战斗 HUD 之上；只在单机训练场由 GameFlow 打开，联机 / 正式对局不打开)
-- - 场景：打开选图弹窗 (MapSelectPopup)
-- - 更换：P1 / P2 角色状态旁的换人按钮，打开换人弹窗 (CharSelectPopup)
-- - 设置：训练场设置 (TrainingSettingView：开关、敌人按键、伤害统计)
-- data：C# BattleHudArgs (用 P1 / P2 / Training)
-- 根节点没有图形，不挡下面 HUD 的点击
local Panel = require "ui.base.Panel"

local function OpenCharSelect(character)
    if character == nil then return end
    local slot = character.InputSlot
    UI.Open("CharSelectPopup", {
        Slot = slot,
        CurrentCharacterId = character.CharacterId,
        CurrentSecret = CS.BattleManager.Instance:SecretOf(slot),
    })
end

local M = class("TrainingPanel", Panel)

function M:OnCreate()
    self:OnClick(self.mapSelectBtn, function() UI.Open("MapSelectPopup") end)
    self:OnClick(self.settingButton, function(s) s.trainingSettingPanel:ToggleSettings() end)
    self:OnClick(self.p1ChangeCharBtn, function(s) OpenCharSelect(s.p1) end)
    self:OnClick(self.p2ChangeCharBtn, function(s) OpenCharSelect(s.p2) end)
end

function M:OnOpen(args)
    self.p1 = args.P1
    self.p2 = args.P2
    self.p1ChangeCharBtn.gameObject:SetActive(self.p1 ~= nil)
    self.p2ChangeCharBtn.gameObject:SetActive(self.p2 ~= nil)
    self.trainingSettingPanel:Bind(args.Training)
end

function M:OnClose()
    self.p1 = nil
    self.p2 = nil
    self.trainingSettingPanel:Bind(nil)
end

return M
