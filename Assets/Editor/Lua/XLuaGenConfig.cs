using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KiHan.Config;
using KiHan.Logic;
using KiHan.Network;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;
using XLua;

namespace KiHan.Editor
{
    /// <summary>
    /// xLua 代码生成配置 (XLua/Generate Code)：Lua 用到的 C# 类型与 C# 回调 Lua 的委托类型。
    /// Lua 新引用了 CS. 类型时同步加进 LuaCallCSharp (编辑器走反射可用，真机 IL2CPP 需要生成代码)。
    /// 黑名单沿用 XLua/Editor/ExampleConfig.cs。
    /// </summary>
    public static class XLuaGenConfig
    {
        private static readonly string[] UINamespaces = { "KiHan.UI", "View.UI" };

        private static IEnumerable<Type> ProjectTypes()
        {
            Assembly game = typeof(GameFlow).Assembly;
            IEnumerable<Type> ui = game.GetTypes().Where(t => t.IsPublic && !t.IsGenericTypeDefinition && UINamespaces.Contains(t.Namespace));
            Type[] extra =
            {
                typeof(GameFlow), typeof(BattleManager), typeof(MapManager),
                typeof(KiHan.Asset.AssetManager), typeof(KiHan.Lua.LuaManager),
                typeof(MapRegistry), typeof(MapConfig), typeof(CharacterRegistry), typeof(CharacterManifest),
                typeof(SecretScrollRegistry), typeof(SecretScrollEntry), typeof(PlayerLoadout), typeof(HttpConfig), typeof(HttpErrCode),
                typeof(CharacterObj), typeof(SkillController), typeof(BuffController), typeof(TrainingSession),
                typeof(GatewayManager), typeof(LobbyManager), typeof(PlayerInfo), typeof(MatchGameNtf),
                typeof(HttpClient), typeof(HttpResponse), typeof(LoginResponse), typeof(VersionResponse),
            };
            return ui.Concat(extra).Distinct();
        }

        [LuaCallCSharp]
        public static IEnumerable<Type> LuaCallCSharp
        {
            get
            {
                Type[] unity =
                {
                    typeof(UnityEngine.Object), typeof(GameObject), typeof(Component), typeof(Transform), typeof(RectTransform),
                    typeof(Behaviour), typeof(MonoBehaviour), typeof(Time), typeof(Mathf), typeof(Application), typeof(Screen),
                    typeof(Debug), typeof(Vector2), typeof(Vector3), typeof(Color), typeof(Sprite), typeof(Texture),
                    typeof(RenderTexture), typeof(Animator), typeof(CanvasGroup), typeof(CanvasRenderer),
                    typeof(Button), typeof(Button.ButtonClickedEvent), typeof(Image), typeof(RawImage), typeof(Toggle),
                    typeof(Slider), typeof(Graphic), typeof(AspectRatioFitter),
                    typeof(UnityEvent), typeof(UnityEventBase),
                    typeof(TMP_Text), typeof(TextMeshProUGUI), typeof(TMP_InputField),
                    typeof(VideoPlayer), typeof(VideoRenderMode), typeof(VideoAspectRatio), typeof(Convert),
                    typeof(AspectRatioFitter.AspectMode), typeof(TMP_InputField.ContentType),
                    typeof(List<CharacterManifest>), typeof(List<SecretScrollEntry>), typeof(List<MapConfig>),
                };
                return unity.Concat(ProjectTypes()).Distinct().ToList();
            }
        }

        [CSharpCallLua]
        public static IEnumerable<Type> CSharpCallLua
        {
            get
            {
                var result = new HashSet<Type>
                {
                    typeof(Action), typeof(Action<LuaTable>), typeof(Action<LuaTable, object>), typeof(Action<LuaTable, float>),
                    typeof(Func<string, KiHan.UI.LuaPanel, LuaTable>),
                    typeof(UnityAction), typeof(UnityAction<bool>), typeof(UnityAction<float>), typeof(UnityAction<string>), typeof(UnityAction<int>),
                    typeof(VideoPlayer.EventHandler),
                };
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (Type type in ProjectTypes())
                {
                    foreach (FieldInfo f in type.GetFields(flags)) AddDelegate(result, f.FieldType);
                    foreach (PropertyInfo p in type.GetProperties(flags)) AddDelegate(result, p.PropertyType);
                    foreach (EventInfo e in type.GetEvents(flags)) AddDelegate(result, e.EventHandlerType);
                    foreach (MethodInfo m in type.GetMethods(flags))
                    {
                        foreach (ParameterInfo p in m.GetParameters()) AddDelegate(result, p.ParameterType);
                    }
                }
                return result.ToList();
            }
        }

        /// <summary>
        /// 签名含 ref struct (Span / ReadOnlySpan，Unity 2022 新增的重载) 的成员无法生成包装代码
        /// </summary>
        [BlackList]
        public static Func<MemberInfo, bool> ByRefLikeFilter = member =>
        {
            switch (member)
            {
                case MethodBase method:
                    return method.GetParameters().Any(p => IsByRefLike(p.ParameterType)) || (method is MethodInfo mi && IsByRefLike(mi.ReturnType));
                case PropertyInfo property:
                    return IsByRefLike(property.PropertyType);
                case FieldInfo field:
                    return IsByRefLike(field.FieldType);
                default:
                    return false;
            }
        };

        private static bool IsByRefLike(Type type)
        {
            if (type.IsByRef) type = type.GetElementType();
            return type != null && type.IsByRefLike;
        }

        private static void AddDelegate(HashSet<Type> result, Type type)
        {
            if (type == null || !typeof(Delegate).IsAssignableFrom(type) || type == typeof(Delegate) || type == typeof(MulticastDelegate)) return;
            if (type.ContainsGenericParameters || !IsVisible(type)) return;
            MethodInfo invoke = type.GetMethod("Invoke");
            if (invoke == null || invoke.GetParameters().Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer)) return;
            result.Add(type);
        }

        private static bool IsVisible(Type type)
        {
            if (!type.IsVisible) return false;
            return !type.IsGenericType || type.GetGenericArguments().All(IsVisible);
        }
    }
}
