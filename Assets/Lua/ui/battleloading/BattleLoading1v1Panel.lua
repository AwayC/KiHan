-- 1v1 Loading (Loading 层，第二回合起每次换人)：战斗 HUD 在换人转场盖住画面时打开，data = { P1 = 出战配置, P2 = 出战配置 } (C# PlayerLoadout)
-- 显示双方下一回合的出战角色；换人阶段结束时由战斗 HUD 转场关闭
local Panel = require "ui.base.Panel"

local M = class("BattleLoading1v1Panel", Panel)

function M:OnOpen(args)
    self.p1Card:Bind(args.P1)
    self.p2Card:Bind(args.P2)
end

return M
