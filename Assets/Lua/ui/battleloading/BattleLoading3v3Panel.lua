-- 3v3 阵容 Loading (Loading 层，第一回合前)：GameFlow 在选人确认 (人机) / 收到双方阵容 (联机) 后转场打开，data 为 C# BattleLoadingArgs
-- 左 1P (蓝) / 右 2P (红) 按出战顺序显示立绘、名字与秘卷；对局 Loading 阶段结束时由战斗 HUD 转场关闭
local Panel = require "ui.base.Panel"

local M = class("BattleLoading3v3Panel", Panel)

local function BindLineup(cards, lineup)
    local count = lineup ~= nil and lineup.Length or 0
    for k = 1, #cards do
        local card = cards[k]
        local show = k <= count
        card.gameObject:SetActive(show)
        if show then card:Bind(lineup[k - 1]) end
    end
end

function M:OnOpen(args)
    BindLineup(self.p1Card, args.P1Lineup)
    BindLineup(self.p2Card, args.P2Lineup)
end

return M
