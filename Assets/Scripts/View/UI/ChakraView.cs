using KiHan.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 查克拉显示 (颗数 / 上限)：文本按上限预生成，只在数值变化时切换 (0 GC)
    /// </summary>
    public class ChakraView : MonoBehaviour
    {
        private Text _label;
        private string[] _texts;
        private int _shown = -1;

        public static ChakraView Create(Transform parent, Vector2 anchoredPosition)
        {
            var go = new GameObject("Chakra", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(220f, 40f);

            var view = go.AddComponent<ChakraView>();
            view._label = SkillButtonView.CreateLabel(go.transform, 28);
            view._label.color = new Color(0.45f, 0.8f, 1f);
            return view;
        }

        public void Refresh(SkillController skills)
        {
            if (_texts == null || _texts.Length != skills.ChakraMax + 1)
            {
                _texts = new string[skills.ChakraMax + 1];
                for (int i = 0; i <= skills.ChakraMax; i++)
                {
                    _texts[i] = "查克拉 " + new string('●', i) + new string('○', skills.ChakraMax - i);
                }
                _shown = -1;
            }

            int chakra = skills.Chakra;
            if (chakra == _shown) return;
            _shown = chakra;
            _label.text = _texts[Mathf.Clamp(chakra, 0, skills.ChakraMax)];
        }
    }
}
