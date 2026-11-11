using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace KiHan.Editor
{
    /// <summary>
    /// .lua 导入为 TextAsset：可直接按地址 "Lua/..." 加载并打进 lua 包
    /// </summary>
    [ScriptedImporter(1, "lua")]
    public sealed class LuaImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("main", text);
            ctx.SetMainObject(text);
        }
    }
}
