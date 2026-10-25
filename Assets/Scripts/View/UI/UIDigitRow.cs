using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// UGUI 图片数字：预建固定位数的 Image，按数值替换精灵并居中排列；数值不变时不修改节点 (0 GC)
    /// </summary>
    public class UIDigitRow : MonoBehaviour
    {
        private const int MaxDigits = 3;

        private readonly Image[] _digits = new Image[MaxDigits];
        private readonly int[] _values = new int[MaxDigits];
        private DigitFont _font;
        private int _shown = int.MinValue;

        /// <summary>
        /// 在 parent 中央创建一行数字 (只用于显示，不接收点击)
        /// </summary>
        public static UIDigitRow Create(Transform parent, DigitFont font)
        {
            var go = new GameObject("Digits", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;

            var row = go.AddComponent<UIDigitRow>();
            row._font = font;
            for (int i = 0; i < MaxDigits; i++)
            {
                var digitGo = new GameObject("digit", typeof(RectTransform));
                digitGo.transform.SetParent(go.transform, false);
                Image image = digitGo.AddComponent<Image>();
                image.raycastTarget = false;
                image.enabled = false;
                row._digits[i] = image;
            }
            return row;
        }

        /// <summary>
        /// 显示非负整数，小于 0 时隐藏
        /// </summary>
        public void Show(int value)
        {
            if (value == _shown) return;
            _shown = value;

            int count = 0;
            if (value >= 0)
            {
                int v = Mathf.Min(value, 999);
                count = 1;
                for (int t = v / 10; t > 0; t /= 10) count++;
                for (int i = count - 1; i >= 0; i--)
                {
                    _values[i] = v % 10;
                    v /= 10;
                }
            }

            // 先算总宽度，再从左到右以行中心为原点排列
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                Sprite s = _font.Get(_values[i]);
                total += (s != null ? s.rect.width : 0f) * _font.scale + (i > 0 ? _font.spacing * _font.scale : 0f);
            }

            float x = -total * 0.5f;
            for (int i = 0; i < MaxDigits; i++)
            {
                Image image = _digits[i];
                Sprite sprite = i < count ? _font.Get(_values[i]) : null;
                image.enabled = sprite != null;
                if (sprite == null) continue;

                if (i > 0) x += _font.spacing * _font.scale;
                Vector2 size = sprite.rect.size * _font.scale;
                image.sprite = sprite;
                RectTransform rt = image.rectTransform;
                rt.sizeDelta = size;
                rt.anchoredPosition = new Vector2(x + size.x * 0.5f, 0f);
                x += size.x;
            }
        }
    }
}
