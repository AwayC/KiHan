using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 一组按下标取用的精灵 (如回合数字 1~6、出战序号 1~3、表情屏蔽开 / 关图标)：
    /// 界面要切换的图片不在 Res 下时，由预制体序列化引用 (随预制体打包)，代码按下标取，不按路径加载
    /// </summary>
    public sealed class UISpriteSet : MonoBehaviour
    {
        [SerializeField] private Sprite[] _sprites = new Sprite[0];

        public int Count => _sprites.Length;

        /// <summary>
        /// 下标从 0 起，越界返回 null
        /// </summary>
        public Sprite Get(int index) => index >= 0 && index < _sprites.Length ? _sprites[index] : null;
    }
}
