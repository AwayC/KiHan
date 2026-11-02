using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// 图片数字字体：0~9 的切片精灵 (按数值下标排列，与切图顺序无关) + 排版参数
    /// 用于伤害跳字 (世界空间 SpriteRenderer) 与技能 CD 数字 (UGUI Image)
    /// </summary>
    [CreateAssetMenu(fileName = "DigitFont", menuName = "KiHan/Digit Font")]
    public class DigitFont : ScriptableObject
    {
        [Tooltip("下标 0~9 依次为数字 0~9 的精灵")]
        public Sprite[] digits = new Sprite[10];

        [Tooltip("相邻数字的间距 (原图像素，负数为重叠)")]
        public float spacing;

        [Tooltip("显示缩放：精灵原尺寸的倍数")]
        public float scale = 1f;

        public Sprite Get(int digit) => digit >= 0 && digit < digits.Length ? digits[digit] : null;
    }
}
