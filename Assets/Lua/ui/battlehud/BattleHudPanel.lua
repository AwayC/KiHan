-- 战斗 HUD (Hud 层)：GameFlow 在战斗加载完成后带参数 (C# BattleHudArgs) 打开；训练场专用的按钮与设置在 TrainingPanel
-- - 顶部：双方角色状态 (按槽位 P1 / P2)；3v3 换人后 (BattleManager.RosterVersion 变化) 重新绑定角色状态与本机按键
-- - 本机按键：技能 / 秘卷 CD 与接段、Buff 倒计时 (buffs 的 button 配置在普攻等按键上)、替身可用次数、通灵数量；按键输入由各按键上的 VirtualButton / VirtualJoystick 直接写入 InputManager
--   (秘卷键与通灵键的父节点是底图，图标在子节点 @SecretBtn / @SummonBtn 上；秘卷图标与 CD 由 @SecretBtn 上的 SkillButtonView 显示)
-- - 对战部分 (@PvP，训练场隐藏)：顶栏回合数与倒计时；联机才有表情 (发送 / 屏蔽对方 / 气泡) 与双方延迟
-- - 3v3 演出 (转场、Loading、播报、结算) 由 MatchDirector 按对局阶段驱动
-- - 返回：通用确认弹窗，确认后 GameFlow.ExitBattle
-- 之后每帧只读角色与对局状态，各 C# 部件只在数值变化时修改 UI
local Panel = require "ui.base.Panel"
local Tween = require "ui.base.Tween"
local MatchDirector = require "ui.battlehud.MatchDirector"

local ExitConfirmText = "确定退出战斗吗？"
local BattleMode = CS.BattleMode
local Phase = CS.KiHan.Logic.MatchPhase
local RoundSeconds = CS.KiHan.Logic.BattleMatch.RoundTicks // CS.KiHan.Logic.GameConfig.LOGIC_FPS

-- 表情气泡：弹出 (回弹) → 停留 → 缩小消失
local EmojiPopTime = 0.5
local EmojiHoldTime = 2
local EmojiHideTime = 0.15
-- 屏蔽按钮图 (@EmojiPopup 上的 UISpriteSet)：0 = 关 (点击屏蔽对方表情)，1 = 开 (点击恢复)
local ForbidSpriteOff = 0
local ForbidSpriteOn = 1
local NoPingText = "--"

local M = class("BattleHudPanel", Panel)

function M:OnCreate()
    -- 本机角色的技能图标 (打开及换人时按 skills.json 预载)
    self.icons = CS.View.UI.SkillIconCache()
    self.director = MatchDirector.new()
    self:OnClick(self.backButton, function()
        UI.Confirm(ExitConfirmText, function() CS.GameFlow.Instance:ExitBattle() end)
    end)

    self.bubbles = {
        [1] = { pop = self.p1EmojiPop, emoji = self.p1Emoji, tween = Tween.new() },
        [2] = { pop = self.p2EmojiPop, emoji = self.p2Emoji, tween = Tween.new() },
    }
    self:OnClick(self.emojiBtn, self.OnEmojiBtnClicked)
    for i = 1, #self.emoji do
        self:OnClick(self.emoji[i], function(s) s:OnEmojiClicked(i - 1) end)
    end
    self:OnClick(self.emojiForbid, self.OnForbidClicked)
end

function M:OnOpen(args)
    local bm = CS.BattleManager.Instance
    self.mode = args.Mode
    self.match = bm.Match
    self.rosterVersion = bm.RosterVersion
    self:BindRoster(args.P1, args.P2, args.Local, args.LocalLoadout)

    local pvp = self.mode ~= BattleMode.Training
    self.pvp:SetActive(pvp)
    local online = self.mode == BattleMode.Online
    self.emojiBtn.gameObject:SetActive(online)
    self.p1Signal.gameObject:SetActive(online)
    self.p2Signal.gameObject:SetActive(online)
    self.emojiPopup.gameObject:SetActive(false)
    self.blockEmoji = false
    self:RefreshForbid()
    self.p1Ping.text = NoPingText
    self.p2Ping.text = NoPingText
    for slot = 1, 2 do
        local b = self.bubbles[slot]
        b.tween:Clear()
        b.pop.gameObject:SetActive(false)
    end
    self.shownRound = -1
    self.shownSeconds = -1

    if online then
        local lockstep = CS.LockstepManager.Instance
        self:Listen(lockstep, "OnEmojiReceived", self.OnEmojiReceived)
        self:Listen(lockstep, "OnPingUpdated", self.OnPingUpdated)
    end

    if self.match ~= nil then self.director:Start(self.match) else self.director:Stop() end
    self:Refresh()
end

-- 绑定双方角色状态与本机按键 (开局与换人后)
function M:BindRoster(p1, p2, localCharacter, loadout)
    self.p1Status:Bind(p1)
    self.p2Status:Bind(p2)

    local icons = self.icons
    self.localCharacter = localCharacter
    icons:Clear()
    local scroll = loadout ~= nil and CS.KiHan.Config.SecretScrollRegistry.Find(loadout.secret) or nil
    if scroll ~= nil then icons:Register(scroll.IconKey, scroll.icon) end
    if localCharacter ~= nil and localCharacter.Skills ~= nil then
        icons:Preload(localCharacter.Skills, CS.KiHan.Config.CharacterRegistry.Find(localCharacter.CharacterId))
    end
    for i = 1, #self.skillButtons do self.skillButtons[i]:Bind(icons) end
    self.secretBtn:Bind(icons)

    -- 通灵系统尚未实现：佩戴的通灵都还没有使用
    local summons = (loadout ~= nil and loadout.summons ~= nil) and loadout.summons.Length or 0
    self.summon:Show(summons, summons)
end

function M:OnClose()
    self.director:Stop()
    self.match = nil
    self.localCharacter = nil
    self.icons:Clear()
end

function M:Update(dt)
    local bubbles = self.bubbles
    bubbles[1].tween:Update(dt)
    bubbles[2].tween:Update(dt)
    self:Refresh()
    self.director:Update()
end

function M:Refresh()
    local match = self.match
    if match ~= nil then
        local bm = CS.BattleManager.Instance
        local version = bm.RosterVersion
        if version ~= self.rosterVersion then
            self.rosterVersion = version
            self:BindRoster(bm.P1, bm.P2, bm.LocalCharacter, bm:CurrentLoadout(bm.LocalSlot))
        end
        self:RefreshTopbar(match)
    end

    self.p1Status:Refresh()
    self.p2Status:Refresh()

    local localCharacter = self.localCharacter
    if localCharacter == nil then return end
    local buffs = localCharacter.Buffs
    self.attackTimer:Refresh(buffs)

    local skills = localCharacter.Skills
    if skills == nil then return end
    local buttons = self.skillButtons
    for i = 1, #buttons do buttons[i]:Refresh(skills, buffs) end
    self.secretBtn:Refresh(skills, buffs)
    self.substitution:Refresh(skills)
end

-- 顶栏：回合数 (个位) 与倒计时 (两位，回合开始前显示整回合时长，结束后停在剩余时间)；只在数值变化时换图
function M:RefreshTopbar(match)
    local digits = self.topbar
    local round = match.Round
    if round ~= self.shownRound then
        self.shownRound = round
        self.round.sprite = digits:Get(round % 10)
    end

    local phase = match.Phase
    local running = phase == Phase.Fighting or phase == Phase.RoundEnd or phase == Phase.Finished
    local seconds = running and match.RemainingSeconds or RoundSeconds
    if seconds > 99 then seconds = 99 end
    if seconds ~= self.shownSeconds then
        self.shownSeconds = seconds
        self.timerDigit[1].sprite = digits:Get(seconds // 10)
        self.timerDigit[2].sprite = digits:Get(seconds % 10)
    end
end

-- 表情

function M:OnEmojiBtnClicked()
    local go = self.emojiPopup.gameObject
    go:SetActive(not go.activeSelf)
end

function M:OnEmojiClicked(emojiId)
    self.emojiPopup.gameObject:SetActive(false)
    CS.LockstepManager.Instance:SendEmoji(emojiId)
end

function M:OnForbidClicked()
    self.blockEmoji = not self.blockEmoji
    self:RefreshForbid()
    UI.Toast(self.blockEmoji and "已屏蔽对方表情" or "已恢复对方表情")
end

function M:RefreshForbid()
    local sprite = self.emojiPopup:Get(self.blockEmoji and ForbidSpriteOn or ForbidSpriteOff)
    if sprite ~= nil then self.emojiForbid.image.sprite = sprite end
end

function M:OnEmojiReceived(gameId, emojiId)
    local myGameId = CS.LockstepManager.Instance.MyGameId
    if self.blockEmoji and gameId ~= myGameId then return end
    local item = self.emoji[emojiId + 1]
    local bubble = self.bubbles[gameId]
    if item == nil or bubble == nil then return end

    bubble.emoji.sprite = item.image.sprite
    local pop = bubble.pop
    local tr = pop.transform
    pop.gameObject:SetActive(true)
    local tween = bubble.tween
    tween:Clear()
    Tween.SetScale(tr, 0)
    tween:Add(0, EmojiPopTime, function(t) Tween.SetScale(tr, t) end, Tween.OutElastic)
    tween:Add(EmojiPopTime + EmojiHoldTime, EmojiHideTime, function(t) Tween.SetScale(tr, 1 - t) end, Tween.InQuad,
        function() pop.gameObject:SetActive(false) end)
end

function M:OnPingUpdated(gameId, ms)
    local text = gameId == 1 and self.p1Ping or gameId == 2 and self.p2Ping or nil
    if text ~= nil then text.text = ms .. "ms" end
end

return M
