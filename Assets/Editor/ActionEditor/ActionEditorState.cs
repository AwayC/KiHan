using System;
using System.Collections.Generic;
using KiHan.Logic;
using UnityEngine;

namespace KiHan.Editor
{
    /// <summary>
    /// 动作编辑器的可撤销编辑数据 (不落盘)：一个 actions.json 的全部动作，以及每个动作读入时的原文 (写回时保持原排版)
    /// </summary>
    public class ActionEditorState : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ActionData action = new ActionData();

            /// <summary>
            /// 读入时该动作在文件中的原文；新建的动作为空 (按稀疏写法输出)
            /// </summary>
            public string source;
        }

        /// <summary>
        /// actions.json 的工程相对路径
        /// </summary>
        public string jsonPath;

        /// <summary>
        /// 读入时的整个文件原文 (保留 data 以外的根节点键)
        /// </summary>
        public string rootSource;

        public List<Entry> entries = new List<Entry>();
    }
}
