-- 训练场选图弹窗 (战斗 HUD 的场景按钮打开)：左右滑动列出地图注册表中已启用的地图，
-- 选择后局内切换背景板并关闭 (选当前地图只关闭)；点击空白处关闭
local Panel = require "ui.base.Panel"

local MapConfigList = CS.System.Collections.Generic.List(CS.KiHan.Config.MapConfig)

local M = class("MapSelectPopup", Panel)

function M:OnCreate()
    self.maps = MapConfigList()
    self.bindItem = function(view, index) view:Bind(self.maps[index], index, self.onSelect) end
    self.onSelect = function(index) self:OnSelect(index) end
    self:OnClick(self.closeBtn, self.Close)
end

function M:OnOpen()
    local maps = self.maps
    maps:Clear()
    CS.KiHan.Config.MapRegistry.CollectEnabled(maps)
    self.mapList:SetCount(maps.Count, self.bindItem)

    local current = maps:IndexOf(CS.MapManager.Instance.CurrentMap)
    if current >= 0 then self.mapList:ScrollTo(current, false) end
end

function M:OnSelect(index)
    local selected = self.maps[index]
    self:Close()
    local current = CS.MapManager.Instance.CurrentMap
    if selected ~= nil and not selected:Equals(current) then
        CS.GameFlow.Instance:ChangeMap(selected)
    end
end

return M
