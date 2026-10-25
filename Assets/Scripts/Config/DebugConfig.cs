using KiHan.Config;
using UnityEngine;

namespace KiHan.DebugTools
{
    [CreateAssetMenu(fileName = "LocalDebugConfig", menuName = "KiHan/Create Debug Config")]

    public class DebugConfig : ScriptableObject
    {
        static public string DefaultPath = "LocalDebugConfig";
        [Header("--- 训练场快速调试 ---")]
        public bool isBattleDebug = false;

        [Tooltip("单机训练场无限血：受击照常扣血与显示伤害，生命值归零时立即回满")]
        public bool infiniteHp = false;

        [Header("--- 出战配置 (单机) ---")]
        public PlayerLoadout player1 = new PlayerLoadout();
        public PlayerLoadout player2 = new PlayerLoadout();
    }
}
