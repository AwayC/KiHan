-- 飘字提示 (UI.Toast)：消息排队依次显示，每条淡入 0.2 秒、停留 1.5 秒、淡出 0.2 秒，全部显示完后关闭自己；不挡点击
local Panel = require "ui.base.Panel"

local FadeSec = 0.2
local HoldSec = 1.5

local M = class("ToastPanel", Panel)

function M:OnCreate()
    self.queue = {}
    self.head = 1
    self.elapsed = -1
    self.group = self.gameObject:GetComponent(typeof(CS.UnityEngine.CanvasGroup))
    self.group.blocksRaycasts = false
    self.group.interactable = false
end

function M:OnOpen()
    self.group.alpha = 0
end

function M:OnClose()
    self.queue = {}
    self.head = 1
    self.elapsed = -1
end

-- 加入一条消息 (当前没有在显示时立即显示)
function M:Enqueue(message)
    self.queue[#self.queue + 1] = message or ""
    if self.elapsed < 0 then self:ShowNext() end
end

function M:ShowNext()
    local message = self.queue[self.head]
    if message == nil then
        self.elapsed = -1
        self:Close()
        return
    end
    self.queue[self.head] = nil
    self.head = self.head + 1
    self.text.text = message
    self.elapsed = 0
    self.group.alpha = 0
end

function M:Update(dt)
    if self.elapsed < 0 then return end

    self.elapsed = self.elapsed + dt
    local e = self.elapsed
    if e < FadeSec then
        self.group.alpha = e / FadeSec
    elseif e < FadeSec + HoldSec then
        self.group.alpha = 1
    elseif e < FadeSec * 2 + HoldSec then
        self.group.alpha = 1 - (e - FadeSec - HoldSec) / FadeSec
    else
        self:ShowNext()
    end
end

return M
