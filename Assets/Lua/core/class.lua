-- 最简类：cls.__index = cls，实例 setmetatable({}, cls)；继承通过元表回落到 super
function class(name, super)
    local cls = { __cname = name, super = super }
    cls.__index = cls
    if super then
        setmetatable(cls, { __index = super })
    end
    return cls
end
