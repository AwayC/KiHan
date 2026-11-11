-- 界面补间：按时间排列的片段 (从添加时刻起 delay 秒后开始，duration 秒内每帧回调进度)，面板在 Update(dt) 里推进
-- 用 unscaledDeltaTime，不用协程；只做界面表现，不参与逻辑
--   local tw = Tween.new()
--   tw:Add(0, 0.2, function(t) ... end, Tween.OutBack, onDone)   -- t 为缓动后的进度 0→1，结束时一定回调 t = 1
--   tw:Call(1.5, function() ... end)                             -- 1.5 秒后调用一次
--   tw:Update(dt) / tw:Clear() / tw:IsBusy()
local Tween = {}
Tween.__index = Tween

local Vector3 = CS.UnityEngine.Vector3
local Vector2 = CS.UnityEngine.Vector2

function Tween.new()
    return setmetatable({ time = 0, tracks = {} }, Tween)
end

function Tween:Add(delay, duration, fn, ease, onDone)
    local tracks = self.tracks
    tracks[#tracks + 1] = { start = self.time + delay, duration = duration, fn = fn, ease = ease, onDone = onDone }
end

function Tween:Call(delay, fn)
    self:Add(delay, 0, nil, nil, fn)
end

function Tween:Clear()
    local tracks = self.tracks
    for i = #tracks, 1, -1 do tracks[i] = nil end
end

function Tween:IsBusy()
    return #self.tracks > 0
end

function Tween:Update(dt)
    local time = self.time + dt
    self.time = time
    local tracks = self.tracks
    -- 回调中新加的片段从下一帧开始推进
    local n = #tracks
    local i = 1
    while i <= n do
        local tr = tracks[i]
        local finished = false
        if time >= tr.start then
            local t = tr.duration > 0 and math.min((time - tr.start) / tr.duration, 1) or 1
            if tr.fn then tr.fn(tr.ease and tr.ease(t) or t) end
            finished = t >= 1
        end
        if finished then
            table.remove(tracks, i)
            n = n - 1
            if tr.onDone then tr.onDone() end
        else
            i = i + 1
        end
    end
end

-- 缓动

function Tween.OutQuad(t) return 1 - (1 - t) * (1 - t) end
function Tween.InQuad(t) return t * t end

function Tween.OutBack(t)
    local c1 = 1.70158
    local c3 = c1 + 1
    local u = t - 1
    return 1 + c3 * u * u * u + c1 * u * u
end

function Tween.OutElastic(t)
    if t <= 0 or t >= 1 then return t end
    return 2 ^ (-10 * t) * math.sin((t * 10 - 0.75) * (2 * math.pi / 3)) + 1
end

function Tween.Lerp(a, b, t) return a + (b - a) * t end

-- 常用设置

function Tween.SetScale(transform, x, y)
    transform.localScale = Vector3(x, y or x, 1)
end

function Tween.SetX(rectTransform, x)
    rectTransform.anchoredPosition = Vector2(x, rectTransform.anchoredPosition.y)
end

-- Image / TMP 的透明度
function Tween.SetAlpha(graphic, a)
    local c = graphic.color
    c.a = a
    graphic.color = c
end

return Tween
