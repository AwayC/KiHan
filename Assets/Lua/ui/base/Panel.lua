-- 面板基类。实例由 UI.CreatePanel 创建：self.panel (C# LuaPanel)、self.gameObject、self.transform、@ 节点字段
-- 可重写：OnCreate() / OnOpen(data) / OnClose() / OnRelease()；需要每帧逻辑时定义 Update(dt) (dt 为 unscaledDeltaTime)
-- 下划线开头的方法由 C# 调用，面板不要重写
local Panel = class("Panel")

function Panel:OnCreate() end
function Panel:OnOpen(data) end
function Panel:OnClose() end
function Panel:OnRelease() end

function Panel:_Create()
    self._listeners = {}
    self:OnCreate()
end

function Panel:_Open(data)
    self:_RemoveListeners()
    self:OnOpen(data)
end

function Panel:_Close()
    self:OnClose()
    self:_RemoveListeners()
end

function Panel:_Release()
    self:OnRelease()
end

-- 关闭自己
function Panel:Close()
    self.panel:Close()
end

-- 按钮点击：fn(self)
function Panel:OnClick(button, fn)
    button.onClick:AddListener(function() fn(self) end)
end

-- 订阅 C# 委托字段 (如 LobbyManager.OnPlayerDataUpdated)：fn(self, ...)；关闭 / 重新打开时自动移除
-- C# 的 event 不走这里：在 OnCreate 里 obj:Event('+', fn)，随实例存活
function Panel:Listen(obj, field, fn)
    local h = function(...) return fn(self, ...) end
    local cur = obj[field]
    obj[field] = cur and (cur + h) or h
    self._listeners[#self._listeners + 1] = { obj, field, h }
end

function Panel:_RemoveListeners()
    for i = 1, #self._listeners do
        local l = self._listeners[i]
        local obj, field, h = l[1], l[2], l[3]
        local cur = obj[field]
        if cur then obj[field] = cur - h end
        self._listeners[i] = nil
    end
end

return Panel
