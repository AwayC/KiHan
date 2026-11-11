-- 密码框显示 / 隐藏切换 (登录、注册弹窗共用)：隐藏时显示 open 图标，显示时显示 close 图标
local ContentType = CS.TMPro.TMP_InputField.ContentType

local M = {}

function M.Apply(input, openIcon, closeIcon, visible)
    input.contentType = visible and ContentType.Standard or ContentType.Password
    input:ForceLabelUpdate()
    openIcon:SetActive(not visible)
    closeIcon:SetActive(visible)
end

return M
