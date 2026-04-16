using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;

namespace KiHan.Logic
{
    [System.Serializable]
    public class SpriteFrameData 
    {
        public string Name;      
        public Sprite Sprite;
        public Vector2 Offset;   
        
        public List<LogicBox> HurtBoxes = new List<LogicBox>();
        public List<LogicBox> HitBoxes = new List<LogicBox>();
        public List<EffectLayerInfo> ExtraLayers = new List<EffectLayerInfo>();
    }
}

[CreateAssetMenu(fileName = "NewSpriteLibrary", menuName = "Naruto/Sprite Library")]
public class CharacterSpriteLibrary : ScriptableObject
{
    [Header("判定盒模板")]
    public LogicBox DefaultHurtBox = new LogicBox(Vector2.zero, new Vector2(0.5f, 1.8f), 1.0f);
    public LogicBox DefaultHitBox = new LogicBox(Vector2.zero, new Vector2(0.5f, 0.5f), 1.0f);

    // --- 新增：厚度预设列表 ---
    [Header("厚度预设 (Side Presets)")]
    public List<float> SidePresets = new List<float> { 0.5f, 1.0f, 1.5f, 2.0f };

    public List<SpriteFrameData> AllFrames = new List<SpriteFrameData>();

    public SpriteFrameData GetFrame(int index)
    {
        if (index >= 0 && index < AllFrames.Count) return AllFrames[index];
        return null;
    }
}
