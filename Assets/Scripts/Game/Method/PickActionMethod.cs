using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 根据受击类别 (Catalog) 与受力方向选择具体受击动作的算法字典
    /// </summary>
    public static class PickActionMethod
    {
        public static Dictionary<string, Func<List<ActionInfo>, ForceDirection, ActionInfo>> Methods =
            new Dictionary<string, Func<List<ActionInfo>, ForceDirection, ActionInfo>>
            {
                {
                    "Hurt",
                    (candidates, dir) =>
                    {
                        foreach (ActionInfo info in candidates)
                        {
                            if (
                                (info.id == "HurtFromForward" && dir == ForceDirection.Forward) ||
                                (info.id == "HurtFromBackward" && dir == ForceDirection.Backward)
                            ) return info;
                        }
                        return candidates.Count > 0 ? candidates[0] : new ActionInfo();
                    }
                }
            };
    }
}
