-- 3v3 选人 (Normal 层)：人机对战 (GameFlow.OpenVsAIPrepare) 与联机进房后 (GameFlow.OnRoomEntered) 打开，data 为 C# PrepareArgs
-- - 左侧页签：忍者 / 通灵 (未开放) / 秘卷；未选中的页签上面的盖住下面的 (通灵盖住秘卷)，选中的移到最上层；内容区显示对应列表
-- - 忍者按顺序选 3 个：点未选的忍者取最小的空序号，再点取消 (其余序号不变)；列表项显示序号，左侧 3 个立绘按序号显示
-- - 秘卷同样按顺序选 (可不选)：第 k 个秘卷给第 k 个出战的忍者，下方 3 个秘卷槽按序号显示
-- - 确认：忍者不足 3 个时提示；GameFlow.ConfirmLineup (人机：转场开局；联机：提交阵容并等待对手)
local Panel = require "ui.base.Panel"

local ManifestList = CS.System.Collections.Generic.List(CS.KiHan.Config.CharacterManifest)
local ScrollList = CS.System.Collections.Generic.List(CS.KiHan.Config.SecretScrollEntry)

local LineupSize = CS.GameFlow.LineupSize
local Tab = { Character = 1, Summon = 2, Secret = 3 }
local TipNeedCharacters = "请选择 " .. LineupSize .. " 名忍者"
local TipSummonLocked = "通灵尚未开放"

local M = class("PraperPanel", Panel)

-- 按顺序多选：slots[k] 为第 k 个选中的列表下标 (从 0 起)，nil 为空
local function OrderOf(slots, index)
    for k = 1, LineupSize do
        if slots[k] == index then return k end
    end
    return 0
end

local function Toggle(slots, index)
    local order = OrderOf(slots, index)
    if order > 0 then
        slots[order] = nil
        return
    end
    for k = 1, LineupSize do
        if slots[k] == nil then
            slots[k] = index
            return
        end
    end
end

function M:OnCreate()
    self.characters = ManifestList()
    self.scrolls = ScrollList()
    self.charSlots = {}
    self.secretSlots = {}

    -- 立绘空位的图 (预制体里的"无人")
    self.emptyPortraits = {}
    for k = 1, LineupSize do self.emptyPortraits[k] = self.portrait[k].sprite end

    self.onSelectCharacter = function(index) self:OnSelectCharacter(index) end
    self.onSelectScroll = function(index) self:OnSelectScroll(index) end
    self.bindCharacter = function(view, index)
        view:BindOrdered(self.characters[index], index, OrderOf(self.charSlots, index), self.onSelectCharacter)
    end
    self.bindScroll = function(view, index)
        view:BindOrdered(self.scrolls[index], index, OrderOf(self.secretSlots, index), self.onSelectScroll)
    end

    for i = 1, #self.tab do
        self:OnClick(self.tab[i], function(s) s:OnTabClicked(i) end)
    end
    self:OnClick(self.confirmBtn, self.OnConfirm)
end

function M:OnOpen(args)
    self.mode = args.Mode
    self.submitted = false
    self.confirmBtn.interactable = true

    local characters = self.characters
    characters:Clear()
    CS.KiHan.Config.CharacterRegistry.CollectEnabled(characters)
    local scrolls = self.scrolls
    scrolls:Clear()
    local registry = CS.KiHan.Config.SecretScrollRegistry.Instance
    if registry ~= nil then scrolls:AddRange(registry.scrolls) end

    for k = 1, LineupSize do
        self.charSlots[k] = nil
        self.secretSlots[k] = nil
    end

    self:ShowTab(Tab.Character)
    self.charList:SetCount(characters.Count, self.bindCharacter)
    self.secretList:SetCount(scrolls.Count, self.bindScroll)
    self:RefreshLineup()
end

function M:OnTabClicked(tab)
    if tab == Tab.Summon then
        UI.Toast(TipSummonLocked)
        return
    end
    self:ShowTab(tab)
end

function M:ShowTab(tab)
    local tabs = self.tab
    -- 未选中的按从下到上排 (下面的页签先放)，选中的最后放到最上层
    for i = #tabs, 1, -1 do
        local on = i == tab
        self.tabOn[i].gameObject:SetActive(on)
        self.tabOff[i].gameObject:SetActive(not on)
        if not on then tabs[i].transform:SetAsLastSibling() end
    end
    tabs[tab].transform:SetAsLastSibling()

    local character = tab == Tab.Character
    local charGo = self.charList.gameObject
    local secretGo = self.secretList.gameObject
    if charGo.activeSelf ~= character then charGo:SetActive(character) end
    if secretGo.activeSelf == character then secretGo:SetActive(not character) end
end

function M:OnSelectCharacter(index)
    if self.submitted then return end
    Toggle(self.charSlots, index)
    self.charList:RefreshItems()
    self:RefreshLineup()
end

function M:OnSelectScroll(index)
    if self.submitted then return end
    Toggle(self.secretSlots, index)
    self.secretList:RefreshItems()
    self:RefreshLineup()
end

-- 左侧立绘与下方秘卷槽按序号显示
function M:RefreshLineup()
    for k = 1, LineupSize do
        local index = self.charSlots[k]
        local manifest = index ~= nil and self.characters[index] or nil
        local portrait = self.portrait[k]
        portrait.sprite = manifest ~= nil and manifest.Portrait or self.emptyPortraits[k]
        self.portraitName[k].text = manifest ~= nil and manifest.displayName or ""

        local scrollIndex = self.secretSlots[k]
        self.secretSlot[k]:Bind(scrollIndex ~= nil and self.scrolls[scrollIndex] or nil)
    end
end

function M:OnConfirm()
    if self.submitted then return end
    local ids, secrets = {}, {}
    for k = 1, LineupSize do
        local index = self.charSlots[k]
        if index == nil then
            UI.Toast(TipNeedCharacters)
            return
        end
        ids[k] = self.characters[index].id
        local scrollIndex = self.secretSlots[k]
        secrets[k] = scrollIndex ~= nil and self.scrolls[scrollIndex].id or ""
    end
    self.submitted = true
    self.confirmBtn.interactable = false
    CS.GameFlow.Instance:ConfirmLineup(ids, secrets)
end

return M
