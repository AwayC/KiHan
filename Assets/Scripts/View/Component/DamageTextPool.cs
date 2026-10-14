using System;
using System.Collections.Generic;
using KiHan.Asset;
using UnityEngine;

namespace View.Component
{
    /// <summary>
    /// 伤害跳字对象池：加载阶段预载数字精灵并预建节点，局内只做复用 (不 Load、不新建)
    /// 池耗尽时复用最早显示的节点，保证局内不产生新实例
    /// </summary>
    public class DamageTextPool
    {
        private const string WhiteFontPath = "UI/Number/White_Font";
        private const string RedFontPath = "UI/Number/Red_Font";

        private readonly Sprite[] _whiteFont = new Sprite[10];
        private readonly Sprite[] _redFont = new Sprite[10];
        private readonly Stack<DamageTextNode> _free;
        private readonly List<DamageTextNode> _active;
        private readonly Transform _root;

        public DamageTextPool(int capacity)
        {
            LoadFont(WhiteFontPath, _whiteFont);
            LoadFont(RedFontPath, _redFont);

            _free = new Stack<DamageTextNode>(capacity);
            _active = new List<DamageTextNode>(capacity);
            _root = new GameObject("[DamageTextPool]").transform;

            Action<DamageTextNode> release = Release;
            for (int i = 0; i < capacity; i++)
            {
                _free.Push(DamageTextNode.Create(_root, release));
            }
        }

        /// <param name="byLocalPlayer">本机玩家造成的伤害用白字，否则红字</param>
        public void Show(int damage, bool byLocalPlayer, Vector3 worldPos, int hitDirectionX)
        {
            DamageTextNode node;
            if (_free.Count > 0)
            {
                node = _free.Pop();
            }
            else
            {
                // 池耗尽：复用最早显示的节点 (不扩容)
                node = _active[0];
                _active.RemoveAt(0);
            }

            _active.Add(node);
            node.Show(damage, byLocalPlayer ? _whiteFont : _redFont, worldPos, hitDirectionX);
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _free.Clear();
            _active.Clear();
        }

        private void Release(DamageTextNode node)
        {
            _active.Remove(node);
            _free.Push(node);
        }

        private static void LoadFont(string path, Sprite[] font)
        {
            for (int i = 0; i < font.Length; i++)
            {
                font[i] = AssetManager.Instance.Load<Sprite>($"{path}/{i}");
                if (font[i] == null) Debug.LogError($"[DamageTextPool] 未能加载数字图片: {path}/{i}");
            }
        }
    }
}
