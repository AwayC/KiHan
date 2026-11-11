-- 训练场换人弹窗 (战斗 HUD 角色状态上的换人按钮打开)：上方页签切换忍者页 / 秘卷页，各页是上下滑动的网格
-- - 忍者页列出角色注册表中已启用的忍者，秘卷页列出秘卷表中的秘卷；打开时选中该槽位当前的忍者与秘卷 (选中框呼吸灯)
-- - 点卡片切换选中；再点已选中的秘卷取消佩戴
-- - 确定后用选中的忍者 + 秘卷重开训练场 (都没变只关闭)；点击空白处关闭
-- data：Slot (1 = P1 玩家，2 = P2 敌人)、CurrentCharacterId、CurrentSecret (没有为 nil)
-- 列表下标与 UIScrollList 一致，从 0 起，-1 表示未选中
local Panel = require "ui.base.Panel"

local ManifestList = CS.System.Collections.Generic.List(CS.KiHan.Config.CharacterManifest)
local ScrollList = CS.System.Collections.Generic.List(CS.KiHan.Config.SecretScrollEntry)

local Page = { Character = 1, Secret = 2 }

local M = class("CharSelectPopup", Panel)

function M:OnCreate()
    self.characters = ManifestList()
    self.scrolls = ScrollList()
    self.characterIndex = -1
    self.scrollIndex = -1
    self.onSelectCharacter = function(index) self:OnSelectCharacter(index) end
    self.onSelectScroll = function(index) self:OnSelectScroll(index) end
    self.bindCharacter = function(view, index)
        view:Bind(self.characters[index], index, index == self.characterIndex, self.onSelectCharacter)
    end
    self.bindScroll = function(view, index)
        view:Bind(self.scrolls[index], index, index == self.scrollIndex, self.onSelectScroll)
    end

    self:OnClick(self.closeBtn, self.Close)
    self:OnClick(self.confirmBtn, self.OnConfirm)
    self.charPage:Clicked("+", function() self:ShowPage(Page.Character) end)
    self.secretPage:Clicked("+", function() self:ShowPage(Page.Secret) end)
end

function M:OnOpen(args)
    self.slot = args.Slot
    self.currentCharacterId = args.CurrentCharacterId
    local secret = args.CurrentSecret
    self.currentSecret = (secret ~= nil and secret ~= "") and secret or nil

    local characters = self.characters
    characters:Clear()
    CS.KiHan.Config.CharacterRegistry.CollectEnabled(characters)
    self.characterIndex = -1
    for i = 0, characters.Count - 1 do
        if characters[i].id == self.currentCharacterId then
            self.characterIndex = i
            break
        end
    end

    local scrolls = self.scrolls
    scrolls:Clear()
    local registry = CS.KiHan.Config.SecretScrollRegistry.Instance
    if registry ~= nil then scrolls:AddRange(registry.scrolls) end
    self.scrollIndex = -1
    if self.currentSecret ~= nil then
        for i = 0, scrolls.Count - 1 do
            if scrolls[i].id == self.currentSecret then
                self.scrollIndex = i
                break
            end
        end
    end

    self:ShowPage(Page.Character)
    self.charList:SetCount(characters.Count, self.bindCharacter)
    if self.characterIndex >= 0 then self.charList:ScrollTo(self.characterIndex, false) end
    self.secretList:SetCount(scrolls.Count, self.bindScroll)
    if self.scrollIndex >= 0 then self.secretList:ScrollTo(self.scrollIndex, false) end
end

function M:ShowPage(page)
    local character = page == Page.Character
    self.charPage:SetSelected(character)
    self.secretPage:SetSelected(not character)
    local charGo = self.charList.gameObject
    local secretGo = self.secretList.gameObject
    if charGo.activeSelf ~= character then charGo:SetActive(character) end
    if secretGo.activeSelf == character then secretGo:SetActive(not character) end
end

function M:OnSelectCharacter(index)
    if index == self.characterIndex then return end
    self.characterIndex = index
    self.charList:RefreshItems()
end

function M:OnSelectScroll(index)
    self.scrollIndex = index == self.scrollIndex and -1 or index
    self.secretList:RefreshItems()
end

function M:OnConfirm()
    local characterId = self.characterIndex >= 0 and self.characters[self.characterIndex].id or self.currentCharacterId
    local secret = self.scrollIndex >= 0 and self.scrolls[self.scrollIndex].id or nil
    self:Close()
    if characterId ~= self.currentCharacterId or secret ~= self.currentSecret then
        CS.GameFlow.Instance:RestartTraining(self.slot, characterId, secret)
    end
end

return M
