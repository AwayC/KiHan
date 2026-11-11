-- 数学辅助 (返回 Lua 整数，拼字符串时不带 ".0")
local M = {}

-- 同 Mathf.RoundToInt：四舍六入，恰好 .5 时取偶数
function M.RoundToInt(x)
    local f = math.floor(x)
    local d = x - f
    if d > 0.5 or (d == 0.5 and f % 2 == 1) then f = f + 1 end
    return math.tointeger(f)
end

function M.Clamp(x, min, max)
    if x < min then return min end
    if x > max then return max end
    return x
end

return M
