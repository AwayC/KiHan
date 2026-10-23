using System;
using System.Collections.Generic;
using System.Text;
using KiHan.Logic;
using UnityEditor;
using UnityEngine;

namespace KiHan.Editor
{
    public partial class ActionEditorWindow
    {
        private const float PreviewHeight = 300f;
        private const float TrackHeaderWidth = 92f;
        private const float RulerHeight = 20f;
        private const float LaneHeight = 18f;
        private const float EdgeGrabWidth = 6f;

        /// <summary>
        /// 区间条目两端在条目外侧额外可抓取的宽度 (单帧的窄条目也能拉伸)
        /// </summary>
        private const float EdgeOutside = 4f;

        /// <summary>
        /// 时间轴控件 ID 的固定哈希：控件数量在帧间变化时拖动状态 (hotControl) 仍对得上
        /// </summary>
        private static readonly int TimelineControlHint = "KiHan.ActionEditor.Timeline".GetHashCode();

        /// <summary>
        /// 动画帧上限的约定写法：区间终点写成 999 等大数表示"到动画结束"
        /// </summary>
        private const int OpenEndThreshold = 999;

        private enum TrackKind
        {
            AttackPhase,
            DefensePhase,
            Armor,
            BeCancelled,
            InputAccept,
            RootMotion,
            Event,
            CancelTag,
        }

        [Serializable]
        private struct ItemRef
        {
            public bool Valid;
            public TrackKind Kind;
            public int Index;

            /// <summary>
            /// attackPhase / defensePhase 的第几个 frameRanges
            /// </summary>
            public int Sub;
        }

        private struct TrackInfo
        {
            public TrackKind Kind;
            public string Name;
            public string Field;
            public Color Color;
            public bool Point;
            public string Tip;
        }

        private struct TimelineItem
        {
            public TrackKind Kind;
            public int Index;
            public int Sub;
            public int X;
            public int Y;
            public string Label;
            public int Lane;
        }

        private enum DragMode { None, Move, Left, Right, Scrub }

        private static readonly TrackInfo[] Tracks =
        {
            new TrackInfo { Kind = TrackKind.AttackPhase, Name = "攻击阶段", Field = "attackPhase", Color = new Color(0.9f, 0.3f, 0.3f), Tip = "attackPhase：区间内开启攻击盒，按 attackPhase 指向 attacks 中的伤害段" },
            new TrackInfo { Kind = TrackKind.DefensePhase, Name = "受击阶段", Field = "defensePhase", Color = new Color(0.3f, 0.6f, 1f), Tip = "defensePhase：配置后仅区间内受击盒生效 (区间外无敌)；未配置时全程可被打" },
            new TrackInfo { Kind = TrackKind.Armor, Name = "霸体", Field = "armorPhase", Color = new Color(1f, 0.65f, 0.2f), Tip = "armorPhase：区间内的霸体等级" },
            new TrackInfo { Kind = TrackKind.BeCancelled, Name = "可被取消", Field = "beCancelledTag", Color = new Color(0.35f, 0.8f, 0.4f), Tip = "beCancelledTag：区间内可被带有这些 cancelTag 的动作打断" },
            new TrackInfo { Kind = TrackKind.InputAccept, Name = "移动输入", Field = "inputAcceptance", Color = new Color(0.3f, 0.75f, 0.75f), Tip = "inputAcceptance：区间内接受摇杆移动的速率与转向" },
            new TrackInfo { Kind = TrackKind.RootMotion, Name = "位移", Field = "rootMotion", Color = new Color(0.65f, 0.45f, 0.95f), Tip = "rootMotion：区间内的水平位移 / 起始帧设置的速度" },
            new TrackInfo { Kind = TrackKind.Event, Name = "事件", Field = "events", Color = new Color(0.95f, 0.85f, 0.3f), Point = true, Tip = "events：在该帧触发特效 / 生成物 / 镜头 / Buff" },
            new TrackInfo { Kind = TrackKind.CancelTag, Name = "切入帧", Field = "cancelTag", Color = new Color(0.7f, 0.7f, 0.7f), Point = true, Tip = "cancelTag.startFormFrame：以该标签切入本动作时从这一帧开始播放" },
        };

        [SerializeField] private ItemRef _selection;
        [SerializeField] private float _frameWidth = 22f;
        [SerializeField] private float _previewZoom = 1.2f;
        [SerializeField] private Vector2 _previewPan;
        [SerializeField] private bool _showBoxes = true;
        [SerializeField] private bool _loop = true;
        [SerializeField] private Vector2 _timelineScroll;

        private bool _playing;
        private double _lastPlayTick;

        private DragMode _dragMode;
        private float _dragStartX;
        private ItemRef _hover;
        private bool _hoverValid;
        private int _dragUndoGroup = -1;
        private int _dragOrigX;
        private int _dragOrigY;

        #region 帧与播放

        private int TotalFrames(ActionData a)
        {
            if (a == null) return 1;
            if (!string.IsNullOrEmpty(a.animKey) && _anims.TryGetValue(a.animKey, out AnimationFrameData anim) && anim.Steps != null && anim.Steps.Count > 0)
            {
                int sum = 0;
                foreach (AnimationFrameData.AnimStep step in anim.Steps) sum += Mathf.Max(1, step.Duration);
                return sum;
            }

            // 动画缺失：按配置中出现的最大帧推算，保证时间轴可编辑
            int max = 0;
            var items = new List<TimelineItem>();
            foreach (TrackInfo track in Tracks)
            {
                items.Clear();
                CollectItems(a, track.Kind, items);
                foreach (TimelineItem item in items)
                {
                    max = Mathf.Max(max, item.X);
                    if (item.Y < OpenEndThreshold) max = Mathf.Max(max, item.Y);
                }
            }
            return Mathf.Max(10, max + 1);
        }

        private AnimationFrameData CurrentAnim(ActionData a)
        {
            return a != null && !string.IsNullOrEmpty(a.animKey) && _anims.TryGetValue(a.animKey, out AnimationFrameData anim) ? anim : null;
        }

        private static int StepIndexOfFrame(AnimationFrameData anim, int frame)
        {
            int accumulated = 0;
            for (int i = 0; i < anim.Steps.Count; i++)
            {
                accumulated += Mathf.Max(1, anim.Steps[i].Duration);
                if (frame < accumulated) return i;
            }
            return anim.Steps.Count - 1;
        }

        private void TogglePlay()
        {
            _playing = !_playing;
            _lastPlayTick = EditorApplication.timeSinceStartup;
            ActionData a = Current;
            if (_playing && a != null && _frame >= TotalFrames(a) - 1) _frame = 0;
        }

        private void OnEditorUpdate()
        {
            if (!_playing) return;
            ActionData a = Current;
            if (a == null)
            {
                _playing = false;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastPlayTick < GameConfig.RENDER_TICK_TIME) return;
            _lastPlayTick = now;

            int total = TotalFrames(a);
            if (_frame + 1 < total)
            {
                _frame++;
            }
            else if (_loop || a.keepPlayingAnim)
            {
                _frame = 0;
            }
            else
            {
                _playing = false;
            }
            Repaint();
        }

        #endregion

        #region 预览

        private void DrawPreview(ActionData a)
        {
            int total = TotalFrames(a);
            _frame = Mathf.Clamp(_frame, 0, total - 1);

            Rect rect = GUILayoutUtility.GetRect(0, PreviewHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));

            Event e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel)
                {
                    _previewZoom = Mathf.Clamp(_previewZoom - e.delta.y * 0.05f, 0.2f, 6f);
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
                {
                    _previewPan += e.delta;
                    e.Use();
                }
            }

            GUI.BeginClip(rect);
            Vector2 origin = new Vector2(rect.width * 0.5f, rect.height * 0.82f) + _previewPan;
            Handles.color = new Color(1f, 1f, 1f, 0.18f);
            Handles.DrawLine(new Vector3(0, origin.y), new Vector3(rect.width, origin.y));
            Handles.DrawLine(new Vector3(origin.x, 0), new Vector3(origin.x, rect.height));

            AnimationFrameData anim = CurrentAnim(a);
            SpriteFrameData frame = anim != null && anim.Steps.Count > 0 ? anim.GetCurrentFrameData(StepIndexOfFrame(anim, _frame)) : null;
            if (frame != null) DrawFrame(a, frame, origin);
            GUI.EndClip();

            DrawFrameStatus(a, rect, anim, frame);
        }

        private void DrawFrame(ActionData a, SpriteFrameData frame, Vector2 origin)
        {
            float zoom = _previewZoom;
            DrawSprite(frame.Sprite, origin, frame.Offset, zoom, Color.white);
            if (frame.ExtraLayers != null)
            {
                foreach (EffectLayerInfo layer in frame.ExtraLayers) DrawSprite(layer.Sprite, origin, frame.Offset + layer.Offset, zoom, layer.TintColor);
            }

            if (!_showBoxes) return;

            if (frame.HurtBoxes != null)
            {
                foreach (LogicBox box in frame.HurtBoxes)
                {
                    bool active = HurtBoxActive(a, box, _frame);
                    DrawBox(origin, box, zoom, new Color(0f, 0.5f, 1f, active ? 0.3f : 0.06f), new Color(0.3f, 0.8f, 1f, active ? 1f : 0.35f));
                }
            }
            if (frame.HitBoxes != null)
            {
                foreach (LogicBox box in frame.HitBoxes)
                {
                    bool active = HitBoxActive(a, box, _frame);
                    DrawBox(origin, box, zoom, new Color(1f, 0f, 0f, active ? 0.35f : 0.06f), new Color(1f, 0.3f, 0.3f, active ? 1f : 0.35f));
                }
            }
            if (frame.EffectAnchors != null)
            {
                Handles.color = Color.yellow;
                foreach (EffectAnchorInfo anchor in frame.EffectAnchors)
                {
                    Vector2 p = origin + new Vector2(anchor.Position.x, -anchor.Position.y) * zoom;
                    Handles.DrawLine(p + new Vector2(-5, 0), p + new Vector2(5, 0));
                    Handles.DrawLine(p + new Vector2(0, -5), p + new Vector2(0, 5));
                    GUI.Label(new Rect(p.x + 4, p.y - 16, 120, 16), anchor.Name, EditorStyles.miniLabel);
                }
            }
        }

        private static void DrawSprite(Sprite sprite, Vector2 origin, Vector2 offset, float zoom, Color tint)
        {
            if (sprite == null || sprite.texture == null) return;
            Rect tr = sprite.textureRect;
            float w = tr.width * zoom;
            float h = tr.height * zoom;
            var dst = new Rect(origin.x + offset.x * zoom - w * 0.5f, origin.y - offset.y * zoom - h * 0.5f, w, h);
            var uv = new Rect(tr.x / sprite.texture.width, tr.y / sprite.texture.height, tr.width / sprite.texture.width, tr.height / sprite.texture.height);
            Color old = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(dst, sprite.texture, uv, true);
            GUI.color = old;
        }

        private static void DrawBox(Vector2 origin, LogicBox box, float zoom, Color fill, Color outline)
        {
            if (!box.IsValid) return;
            var r = new Rect(origin.x + (box.Center.x - box.Size.x * 0.5f) * zoom, origin.y - (box.Center.y + box.Size.y * 0.5f) * zoom, box.Size.x * zoom, box.Size.y * zoom);
            Handles.DrawSolidRectangleWithOutline(r, fill, outline);
            if (!string.IsNullOrEmpty(box.Tag)) GUI.Label(new Rect(r.x + 2, r.y, 120, 16), box.Tag, EditorStyles.miniLabel);
        }

        /// <summary>
        /// 与 CharacterObj 的判定一致：盒标签为空匹配任意开启阶段，否则需在阶段 tag 中
        /// </summary>
        private static bool TagMatch(string boxTag, string[] phaseTags)
        {
            if (string.IsNullOrEmpty(boxTag)) return true;
            if (phaseTags == null) return false;
            foreach (string t in phaseTags)
            {
                if (t == boxTag) return true;
            }
            return false;
        }

        private static bool InRanges(FrameRange[] ranges, int frame)
        {
            if (ranges == null) return false;
            foreach (FrameRange r in ranges)
            {
                if (frame >= r.x && frame <= r.y) return true;
            }
            return false;
        }

        private static bool HitBoxActive(ActionData a, LogicBox box, int frame)
        {
            if (a.attackPhase == null) return false;
            foreach (AttackBoxTurnOnInfo phase in a.attackPhase)
            {
                if (InRanges(phase.frameRanges, frame) && TagMatch(box.Tag, phase.tag)) return true;
            }
            return false;
        }

        private static bool HurtBoxActive(ActionData a, LogicBox box, int frame)
        {
            if (a.defensePhase == null || a.defensePhase.Length == 0) return true;
            foreach (BeHitBoxTurnOnInfo phase in a.defensePhase)
            {
                if (InRanges(phase.frameRanges, frame) && TagMatch(box.Tag, phase.tag)) return true;
            }
            return false;
        }

        /// <summary>
        /// 预览左上角：当前帧号与该帧生效的全部配置
        /// </summary>
        private void DrawFrameStatus(ActionData a, Rect rect, AnimationFrameData anim, SpriteFrameData frame)
        {
            int f = _frame;
            var sb = new StringBuilder();
            sb.Append($"帧 {f} / {TotalFrames(a) - 1}");
            if (anim != null && anim.Steps.Count > 0)
            {
                int step = StepIndexOfFrame(anim, f);
                sb.Append($"   Step {step + 1}/{anim.Steps.Count}");
                if (frame != null) sb.Append($"  {frame.Name}");
            }

            if (a.attackPhase != null)
            {
                foreach (AttackBoxTurnOnInfo p in a.attackPhase)
                {
                    if (InRanges(p.frameRanges, f)) sb.Append($"\n攻击  段{p.attackPhase}{TagsSuffix(p.tag)}");
                }
            }
            if (a.defensePhase != null && a.defensePhase.Length > 0)
            {
                bool any = false;
                foreach (BeHitBoxTurnOnInfo p in a.defensePhase) any |= InRanges(p.frameRanges, f);
                sb.Append(any ? "\n受击  开启" : "\n受击  关闭 (无敌)");
            }
            if (a.armorPhase != null)
            {
                foreach (ArmorPhaseData p in a.armorPhase)
                {
                    if (f >= p.range.x && f <= p.range.y) sb.Append($"\n霸体  {p.level}");
                }
            }
            if (a.beCancelledTag != null)
            {
                foreach (BeCancelledTagData p in a.beCancelledTag)
                {
                    if (f >= p.frameRange.x && f <= p.frameRange.y) sb.Append($"\n可取消  {Join(p.cancelTag)}");
                }
            }
            if (a.inputAcceptance != null)
            {
                foreach (MoveInputAcceptanceData p in a.inputAcceptance)
                {
                    if (f >= p.range.x && f <= p.range.y) sb.Append($"\n移动  x{Or0(p.rateX)} y{Or0(p.rateY)}{(p.turn ? " 可转向" : string.Empty)}");
                }
            }
            if (a.rootMotion != null)
            {
                foreach (RootMotionSegmentData p in a.rootMotion)
                {
                    if (f >= p.frameRange.x && f <= p.frameRange.y) sb.Append($"\n位移  {RootMotionLabel(p)}");
                }
            }
            if (a.events != null)
            {
                foreach (ActionEventData ev in a.events)
                {
                    if (ev.frame == f) sb.Append($"\n事件  {EventLabel(ev)}");
                }
            }

            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.92f, 0.5f) } };
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), sb.ToString(), style);
        }

        private void DrawTransport(ActionData a)
        {
            int total = TotalFrames(a);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("|◀", GUILayout.Width(32))) { _frame = 0; _playing = false; }
                if (GUILayout.Button("◀", GUILayout.Width(32))) { _frame = Mathf.Max(0, _frame - 1); _playing = false; }
                GUI.color = _playing ? new Color(1f, 0.65f, 0.65f) : new Color(0.65f, 1f, 0.65f);
                if (GUILayout.Button(_playing ? "暂停" : "播放", GUILayout.Width(52))) TogglePlay();
                GUI.color = Color.white;
                if (GUILayout.Button("▶", GUILayout.Width(32))) { _frame = Mathf.Min(total - 1, _frame + 1); _playing = false; }
                if (GUILayout.Button("▶|", GUILayout.Width(32))) { _frame = total - 1; _playing = false; }

                _loop = GUILayout.Toggle(_loop, "循环", "Button", GUILayout.Width(40));
                _showBoxes = GUILayout.Toggle(_showBoxes, "判定盒", "Button", GUILayout.Width(52));
                if (GUILayout.Button("复位视图", GUILayout.Width(64))) { _previewZoom = 1.2f; _previewPan = Vector2.zero; }

                GUILayout.FlexibleSpace();
                GUILayout.Label("帧宽", GUILayout.Width(28));
                _frameWidth = GUILayout.HorizontalSlider(_frameWidth, 8f, 40f, GUILayout.Width(90));
            }
        }

        #endregion

        #region 时间轴

        private static string Join(string[] tags) => tags == null || tags.Length == 0 ? "(空)" : string.Join(", ", tags);

        private static string TagsSuffix(string[] tags) => tags == null || tags.Length == 0 ? string.Empty : $" [{string.Join(",", tags)}]";

        private static string Or0(string s) => string.IsNullOrEmpty(s) ? "0" : s;

        private static string RootMotionLabel(RootMotionSegmentData p)
        {
            var parts = new List<string>(4);
            if (!string.IsNullOrEmpty(p.moveX) || !string.IsNullOrEmpty(p.moveY)) parts.Add($"移{Or0(p.moveX)},{Or0(p.moveY)}");
            if (!string.IsNullOrEmpty(p.velocityX) || !string.IsNullOrEmpty(p.velocityY)) parts.Add($"速{Or0(p.velocityX)},{Or0(p.velocityY)}");
            if (!string.IsNullOrEmpty(p.velocityZ)) parts.Add($"竖{p.velocityZ}");
            if (!string.IsNullOrEmpty(p.accelerationZ)) parts.Add($"加{p.accelerationZ}");
            return parts.Count > 0 ? string.Join(" ", parts) : "(空)";
        }

        private static string EventLabel(ActionEventData ev)
        {
            string type = string.IsNullOrEmpty(ev.type) ? "Effect" : ev.type;
            string target = !string.IsNullOrEmpty(ev.spawnId) ? ev.spawnId : ev.asset;
            if (!string.IsNullOrEmpty(target))
            {
                int slash = target.LastIndexOf('/');
                if (slash >= 0) target = target.Substring(slash + 1);
            }
            return string.IsNullOrEmpty(target) ? type : $"{type}:{target}";
        }

        private static void CollectItems(ActionData a, TrackKind kind, List<TimelineItem> items)
        {
            switch (kind)
            {
                case TrackKind.AttackPhase:
                    if (a.attackPhase == null) return;
                    for (int i = 0; i < a.attackPhase.Length; i++)
                    {
                        FrameRange[] ranges = a.attackPhase[i].frameRanges;
                        if (ranges == null) continue;
                        for (int j = 0; j < ranges.Length; j++)
                        {
                            items.Add(new TimelineItem { Kind = kind, Index = i, Sub = j, X = ranges[j].x, Y = ranges[j].y, Label = $"段{a.attackPhase[i].attackPhase}{TagsSuffix(a.attackPhase[i].tag)}" });
                        }
                    }
                    return;
                case TrackKind.DefensePhase:
                    if (a.defensePhase == null) return;
                    for (int i = 0; i < a.defensePhase.Length; i++)
                    {
                        FrameRange[] ranges = a.defensePhase[i].frameRanges;
                        if (ranges == null) continue;
                        for (int j = 0; j < ranges.Length; j++)
                        {
                            items.Add(new TimelineItem { Kind = kind, Index = i, Sub = j, X = ranges[j].x, Y = ranges[j].y, Label = $"受击{TagsSuffix(a.defensePhase[i].tag)}" });
                        }
                    }
                    return;
                case TrackKind.Armor:
                    if (a.armorPhase == null) return;
                    for (int i = 0; i < a.armorPhase.Length; i++)
                    {
                        items.Add(new TimelineItem { Kind = kind, Index = i, X = a.armorPhase[i].range.x, Y = a.armorPhase[i].range.y, Label = a.armorPhase[i].level });
                    }
                    return;
                case TrackKind.BeCancelled:
                    if (a.beCancelledTag == null) return;
                    for (int i = 0; i < a.beCancelledTag.Length; i++)
                    {
                        items.Add(new TimelineItem { Kind = kind, Index = i, X = a.beCancelledTag[i].frameRange.x, Y = a.beCancelledTag[i].frameRange.y, Label = Join(a.beCancelledTag[i].cancelTag) });
                    }
                    return;
                case TrackKind.InputAccept:
                    if (a.inputAcceptance == null) return;
                    for (int i = 0; i < a.inputAcceptance.Length; i++)
                    {
                        MoveInputAcceptanceData p = a.inputAcceptance[i];
                        items.Add(new TimelineItem { Kind = kind, Index = i, X = p.range.x, Y = p.range.y, Label = $"x{Or0(p.rateX)} y{Or0(p.rateY)}{(p.turn ? " 转" : string.Empty)}" });
                    }
                    return;
                case TrackKind.RootMotion:
                    if (a.rootMotion == null) return;
                    for (int i = 0; i < a.rootMotion.Length; i++)
                    {
                        items.Add(new TimelineItem { Kind = kind, Index = i, X = a.rootMotion[i].frameRange.x, Y = a.rootMotion[i].frameRange.y, Label = RootMotionLabel(a.rootMotion[i]) });
                    }
                    return;
                case TrackKind.Event:
                    if (a.events == null) return;
                    for (int i = 0; i < a.events.Length; i++)
                    {
                        items.Add(new TimelineItem { Kind = kind, Index = i, X = a.events[i].frame, Y = a.events[i].frame, Label = EventLabel(a.events[i]) });
                    }
                    return;
                case TrackKind.CancelTag:
                    if (a.cancelTag == null) return;
                    for (int i = 0; i < a.cancelTag.Length; i++)
                    {
                        items.Add(new TimelineItem { Kind = kind, Index = i, X = a.cancelTag[i].startFormFrame, Y = a.cancelTag[i].startFormFrame, Label = a.cancelTag[i].tag });
                    }
                    return;
            }
        }

        private static void SetRange(ActionData a, ItemRef item, int x, int y)
        {
            switch (item.Kind)
            {
                case TrackKind.AttackPhase: a.attackPhase[item.Index].frameRanges[item.Sub] = new FrameRange { x = x, y = y }; break;
                case TrackKind.DefensePhase: a.defensePhase[item.Index].frameRanges[item.Sub] = new FrameRange { x = x, y = y }; break;
                case TrackKind.Armor: a.armorPhase[item.Index].range = new FrameRange { x = x, y = y }; break;
                case TrackKind.BeCancelled: a.beCancelledTag[item.Index].frameRange = new FrameRange { x = x, y = y }; break;
                case TrackKind.InputAccept: a.inputAcceptance[item.Index].range = new FrameRange { x = x, y = y }; break;
                case TrackKind.RootMotion: a.rootMotion[item.Index].frameRange = new FrameRange { x = x, y = y }; break;
                case TrackKind.Event: a.events[item.Index].frame = x; break;
                case TrackKind.CancelTag: a.cancelTag[item.Index].startFormFrame = x; break;
            }
        }

        private static TrackInfo TrackOf(TrackKind kind)
        {
            foreach (TrackInfo t in Tracks)
            {
                if (t.Kind == kind) return t;
            }
            return Tracks[0];
        }

        private bool SelectionValid()
        {
            ActionData a = Current;
            if (!_selection.Valid || a == null) return false;
            var items = new List<TimelineItem>();
            CollectItems(a, _selection.Kind, items);
            foreach (TimelineItem item in items)
            {
                if (item.Index == _selection.Index && item.Sub == _selection.Sub) return true;
            }
            return false;
        }

        private bool IsSelected(TimelineItem item) => _selection.Valid && _selection.Kind == item.Kind && _selection.Index == item.Index && _selection.Sub == item.Sub;

        private static ItemRef RefOf(TimelineItem item) => new ItemRef { Valid = true, Kind = item.Kind, Index = item.Index, Sub = item.Sub };

        /// <summary>
        /// 重叠的条目分到不同的行，返回行数
        /// </summary>
        private static int AssignLanes(List<TimelineItem> items, int total)
        {
            var laneEnds = new List<int>();
            var order = new List<int>(items.Count);
            for (int i = 0; i < items.Count; i++) order.Add(i);
            order.Sort((l, r) => items[l].X != items[r].X ? items[l].X.CompareTo(items[r].X) : l.CompareTo(r));

            foreach (int i in order)
            {
                TimelineItem item = items[i];
                int end = Mathf.Min(item.Y, total - 1);
                int lane = 0;
                while (lane < laneEnds.Count && laneEnds[lane] >= item.X) lane++;
                if (lane == laneEnds.Count) laneEnds.Add(end);
                else laneEnds[lane] = end;
                item.Lane = lane;
                items[i] = item;
            }
            return Mathf.Max(1, laneEnds.Count);
        }

        private Rect ItemRect(TimelineItem item, float top, int total, bool point)
        {
            float fw = _frameWidth;
            float y = top + item.Lane * LaneHeight + 1;
            if (point) return new Rect(item.X * fw + 1, y, Mathf.Max(fw - 2, 6), LaneHeight - 2);
            int end = Mathf.Min(Mathf.Max(item.Y, item.X), total - 1);
            return new Rect(item.X * fw + 1, y, (end - item.X + 1) * fw - 2, LaneHeight - 2);
        }

        private int FrameAt(float x, int total) => Mathf.Clamp(Mathf.FloorToInt(x / _frameWidth), 0, total - 1);

        private void DrawTimeline(ActionData a)
        {
            int total = TotalFrames(a);
            float fw = _frameWidth;

            var rows = new List<TimelineItem>[Tracks.Length];
            var lanes = new int[Tracks.Length];
            var tops = new float[Tracks.Length];
            float height = RulerHeight;
            for (int t = 0; t < Tracks.Length; t++)
            {
                rows[t] = new List<TimelineItem>();
                CollectItems(a, Tracks[t].Kind, rows[t]);
                lanes[t] = AssignLanes(rows[t], total);
                tops[t] = height;
                height += lanes[t] * LaneHeight + 4;
            }

            const float scrollbar = 14f;
            Rect area = GUILayoutUtility.GetRect(0, height + scrollbar, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.17f, 0.17f, 0.17f));

            // 轨道名
            for (int t = 0; t < Tracks.Length; t++)
            {
                var r = new Rect(area.x, area.y + tops[t], TrackHeaderWidth, lanes[t] * LaneHeight + 4);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height - 2), Tracks[t].Color);
                GUI.Label(new Rect(r.x + 6, r.y + 1, r.width - 6, LaneHeight), new GUIContent($"{Tracks[t].Name} ({CountOf(rows[t])})", Tracks[t].Tip), EditorStyles.miniLabel);
            }

            var view = new Rect(area.x + TrackHeaderWidth, area.y, area.width - TrackHeaderWidth, height + scrollbar);
            var content = new Rect(0, 0, Mathf.Max(view.width - 2, total * fw + 60), height);
            _timelineScroll = GUI.BeginScrollView(view, _timelineScroll, content, false, false);

            // 帧列底色与标尺
            for (int f = 0; f < total; f++)
            {
                if (f % 2 == 1) EditorGUI.DrawRect(new Rect(f * fw, RulerHeight, fw, height - RulerHeight), new Color(1f, 1f, 1f, 0.025f));
            }
            EditorGUI.DrawRect(new Rect(0, 0, content.width, RulerHeight), new Color(0.22f, 0.22f, 0.22f));
            int labelEvery = fw >= 26 ? 1 : fw >= 14 ? 5 : 10;
            for (int f = 0; f < total; f++)
            {
                bool major = f % labelEvery == 0;
                EditorGUI.DrawRect(new Rect(f * fw, major ? 10 : 15, 1, major ? 10 : 5), new Color(1f, 1f, 1f, 0.35f));
                if (major) GUI.Label(new Rect(f * fw + 2, 0, 30, 14), f.ToString(), EditorStyles.miniLabel);
            }

            // 动画步切换位置 (换图帧)
            AnimationFrameData anim = CurrentAnim(a);
            if (anim != null)
            {
                int acc = 0;
                foreach (AnimationFrameData.AnimStep step in anim.Steps)
                {
                    EditorGUI.DrawRect(new Rect(acc * fw, RulerHeight - 3, Mathf.Max(1, step.Duration) * fw - 1, 2), new Color(0.4f, 0.7f, 1f, 0.5f));
                    acc += Mathf.Max(1, step.Duration);
                }
            }

            // 轨道分隔与条目
            for (int t = 0; t < Tracks.Length; t++)
            {
                EditorGUI.DrawRect(new Rect(0, tops[t] - 1, content.width, 1), new Color(0f, 0f, 0f, 0.35f));
                foreach (TimelineItem item in rows[t]) DrawItem(item, Tracks[t], tops[t], total);
            }

            // 动画结束线与播放头
            EditorGUI.DrawRect(new Rect(total * fw, 0, 1, height), new Color(1f, 1f, 1f, 0.4f));
            EditorGUI.DrawRect(new Rect(_frame * fw, 0, fw, RulerHeight), new Color(1f, 0.85f, 0.3f, 0.35f));
            EditorGUI.DrawRect(new Rect(_frame * fw + fw * 0.5f, 0, 1, height), new Color(1f, 0.85f, 0.3f, 0.9f));

            HandleTimelineInput(a, rows, lanes, tops, total, new Rect(_timelineScroll.x, 0, view.width, height));
            GUI.EndScrollView();
        }

        private static int CountOf(List<TimelineItem> items)
        {
            // attackPhase / defensePhase 的多个 frameRanges 属于同一条配置
            var seen = new HashSet<int>();
            foreach (TimelineItem item in items) seen.Add(item.Index);
            return seen.Count;
        }

        private void DrawItem(TimelineItem item, TrackInfo track, float top, int total)
        {
            Rect r = ItemRect(item, top, total, track.Point);
            bool selected = IsSelected(item);
            bool hovered = !selected && _hoverValid && _hover.Kind == item.Kind && _hover.Index == item.Index && _hover.Sub == item.Sub;
            Color c = track.Color;
            c.a = selected ? 0.95f : hovered ? 0.8f : 0.6f;
            EditorGUI.DrawRect(r, c);

            if (!track.Point)
            {
                // 头尾手柄：可拖动调整起止帧
                float grip = Mathf.Min(4f, r.width * 0.25f);
                Color gripColor = selected ? Color.white : new Color(0f, 0f, 0f, 0.35f);
                EditorGUI.DrawRect(new Rect(r.x, r.y, grip, r.height), gripColor);
                if (item.Y < total) EditorGUI.DrawRect(new Rect(r.xMax - grip, r.y, grip, r.height), gripColor);
            }
            if (selected) Handles.DrawSolidRectangleWithOutline(r, Color.clear, Color.white);

            if (Event.current.type == EventType.Repaint && GUIUtility.hotControl == 0)
            {
                if (track.Point)
                {
                    EditorGUIUtility.AddCursorRect(r, MouseCursor.MoveArrow);
                }
                else
                {
                    float grab = EdgeGrab(r);
                    EditorGUIUtility.AddCursorRect(new Rect(r.x + grab, r.y, Mathf.Max(0f, r.width - grab * 2), r.height), MouseCursor.MoveArrow);
                    EditorGUIUtility.AddCursorRect(new Rect(r.x - EdgeOutside, r.y, grab + EdgeOutside, r.height), MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(new Rect(r.xMax - grab, r.y, grab + EdgeOutside, r.height), MouseCursor.ResizeHorizontal);
                }
            }

            var style = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, normal = { textColor = Color.black } };
            string range = item.X == item.Y ? $"{item.X}" : item.Y >= OpenEndThreshold ? $"{item.X}-末" : $"{item.X}-{item.Y}";
            if (track.Point)
            {
                // 点事件的文字画在标记右侧
                var labelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = track.Color } };
                GUI.Label(new Rect(r.xMax + 2, r.y, 220, r.height), selected ? $"{item.Label} @{item.X}" : item.Label, labelStyle);
                return;
            }

            string label = selected ? $"[{range}] {item.Label}" : item.Label;
            if (item.Y >= total) label += " →";
            GUI.Label(new Rect(r.x + 4, r.y, r.width - 6, r.height), new GUIContent(label, $"[{item.X}, {item.Y}] {item.Label}\n拖动中间平移，拖动两端调整起止帧"), style);

            // 拖动中在条目上方实时显示区间
            if (selected && _dragMode != DragMode.None && _dragMode != DragMode.Scrub)
            {
                var tipStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.white } };
                GUI.Label(new Rect(r.x, r.y - 14, 120, 14), $"[{item.X}, {item.Y}]", tipStyle);
            }
        }

        /// <summary>
        /// 条目两端的拖动手柄宽度 (条目内侧部分)；窄条目按比例缩小，保证中间仍可平移
        /// </summary>
        private static float EdgeGrab(Rect r) => Mathf.Min(EdgeGrabWidth, r.width * 0.3f);

        /// <param name="visible">滚动视图中可见的区域 (滚动视图外的点击同样会传进来，必须排除)</param>
        private void HandleTimelineInput(ActionData a, List<TimelineItem>[] rows, int[] lanes, float[] tops, int total, Rect visible)
        {
            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(TimelineControlHint, FocusType.Passive);

            if (GUIUtility.hotControl == controlId && e.type == EventType.Repaint)
            {
                // 拖动中整个时间轴保持对应光标
                EditorGUIUtility.AddCursorRect(visible, _dragMode == DragMode.Left || _dragMode == DragMode.Right ? MouseCursor.ResizeHorizontal : _dragMode == DragMode.Move ? MouseCursor.MoveArrow : MouseCursor.Arrow);
            }

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseMove:
                {
                    bool had = _hoverValid;
                    ItemRef old = _hover;
                    _hoverValid = false;
                    if (visible.Contains(e.mousePosition))
                    {
                        int t = TrackAt(e.mousePosition.y, lanes, tops);
                        if (t >= 0 && TryHitItem(rows[t], Tracks[t], tops[t], total, e.mousePosition, out TimelineItem hover, out _))
                        {
                            _hover = RefOf(hover);
                            _hoverValid = true;
                        }
                    }
                    if (had != _hoverValid || old.Kind != _hover.Kind || old.Index != _hover.Index || old.Sub != _hover.Sub) Repaint();
                    return;
                }
                case EventType.MouseDown:
                {
                    if (!visible.Contains(e.mousePosition)) return;
                    int frame = FrameAt(e.mousePosition.x, total);

                    if (e.mousePosition.y < RulerHeight)
                    {
                        if (e.button != 0) return;
                        _frame = frame;
                        _playing = false;
                        _dragMode = DragMode.Scrub;
                        GUIUtility.hotControl = controlId;
                        e.Use();
                        return;
                    }

                    int track = TrackAt(e.mousePosition.y, lanes, tops);
                    if (track < 0) return;

                    if (TryHitItem(rows[track], Tracks[track], tops[track], total, e.mousePosition, out TimelineItem hit, out DragMode mode))
                    {
                        _selection = RefOf(hit);
                        GUI.FocusControl(null);
                        if (e.button == 1)
                        {
                            ShowItemMenu(a, hit);
                        }
                        else if (e.button == 0)
                        {
                            _dragMode = mode;
                            _dragStartX = e.mousePosition.x;
                            _dragOrigX = hit.X;
                            _dragOrigY = hit.Y;
                            // 一次拖动 (多次 MouseDrag) 合并为一步撤销
                            Undo.IncrementCurrentGroup();
                            _dragUndoGroup = Undo.GetCurrentGroup();
                            GUIUtility.hotControl = controlId;
                        }
                        e.Use();
                        Repaint();
                        return;
                    }

                    if (e.button == 1)
                    {
                        ShowAddMenu(a, Tracks[track].Kind, frame);
                    }
                    else if (e.button == 0)
                    {
                        _selection = default;
                        _frame = frame;
                        _playing = false;
                        GUI.FocusControl(null);
                    }
                    e.Use();
                    Repaint();
                    return;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != controlId) return;
                    if (_dragMode == DragMode.Scrub)
                    {
                        _frame = FrameAt(e.mousePosition.x, total);
                    }
                    else if (_dragMode != DragMode.None && SelectionValid())
                    {
                        // 按鼠标相对按下位置的位移换算帧数 (四舍五入)，与按下时落在帧格内的位置无关
                        int delta = Mathf.RoundToInt((e.mousePosition.x - _dragStartX) / _frameWidth);
                        ApplyDrag(a, delta, total);
                    }
                    e.Use();
                    Repaint();
                    return;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != controlId) return;
                    GUIUtility.hotControl = 0;
                    _dragMode = DragMode.None;
                    if (_dragUndoGroup >= 0)
                    {
                        Undo.CollapseUndoOperations(_dragUndoGroup);
                        _dragUndoGroup = -1;
                    }
                    e.Use();
                    Repaint();
                    return;
                }
            }
        }

        private int TrackAt(float y, int[] lanes, float[] tops)
        {
            for (int t = 0; t < Tracks.Length; t++)
            {
                if (y >= tops[t] && y < tops[t] + lanes[t] * LaneHeight + 4) return t;
            }
            return -1;
        }

        /// <summary>
        /// 命中条目：选中的条目优先；区间条目两端 (含外侧 EdgeOutside 像素) 为调整起止帧，中间为平移
        /// </summary>
        private bool TryHitItem(List<TimelineItem> items, TrackInfo track, float top, int total, Vector2 mouse, out TimelineItem hit, out DragMode mode)
        {
            int best = -1;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Rect r = ItemRect(items[i], top, total, track.Point);
                if (!track.Point) r = new Rect(r.x - EdgeOutside, r.y, r.width + EdgeOutside * 2, r.height);
                if (!r.Contains(mouse)) continue;
                if (best < 0 || IsSelected(items[i])) best = i;
            }

            if (best < 0)
            {
                hit = default;
                mode = DragMode.None;
                return false;
            }

            hit = items[best];
            mode = DragMode.Move;
            if (!track.Point)
            {
                Rect r = ItemRect(hit, top, total, false);
                float grab = EdgeGrab(r);
                float dl = mouse.x - r.x;
                float dr = r.xMax - mouse.x;
                if (dl <= grab && dl <= dr) mode = DragMode.Left;
                else if (dr <= grab) mode = DragMode.Right;
            }
            return true;
        }

        /// <summary>
        /// 拖动：整体平移保持长度；终点为"到动画结束" (≥ 总帧数) 时平移不改终点；拉伸不越过另一端
        /// </summary>
        private void ApplyDrag(ActionData a, int delta, int total)
        {
            bool open = _dragOrigY >= total;
            int x = _dragOrigX;
            int y = _dragOrigY;
            switch (_dragMode)
            {
                case DragMode.Move:
                    x = Mathf.Clamp(_dragOrigX + delta, 0, total - 1);
                    if (!open) y = _dragOrigY + (x - _dragOrigX);
                    break;
                case DragMode.Left:
                    x = Mathf.Clamp(_dragOrigX + delta, 0, open ? total - 1 : _dragOrigY);
                    break;
                case DragMode.Right:
                    if (delta != 0) y = Mathf.Max(_dragOrigX, (open ? total - 1 : _dragOrigY) + delta);
                    break;
            }

            var items = new List<TimelineItem>();
            CollectItems(a, _selection.Kind, items);
            foreach (TimelineItem item in items)
            {
                if (item.Index != _selection.Index || item.Sub != _selection.Sub) continue;
                if (item.X == x && item.Y == y) return;
            }

            Undo.RecordObject(_state, "编辑时间轴");
            SetRange(a, _selection, x, y);
            MarkDirty();
            Repaint();
        }

        #endregion

        #region 新增 / 删除

        private void ShowAddMenu(ActionData a, TrackKind kind, int frame)
        {
            var menu = new GenericMenu();
            foreach (TrackInfo track in Tracks)
            {
                TrackKind k = track.Kind;
                string prefix = k == kind ? string.Empty : "其他轨道/";
                menu.AddItem(new GUIContent($"{prefix}在第 {frame} 帧添加{track.Name}"), false, () => AddItem(k, frame));
            }
            menu.ShowAsContext();
        }

        private void ShowItemMenu(ActionData a, TimelineItem item)
        {
            var menu = new GenericMenu();
            ItemRef target = RefOf(item);
            menu.AddItem(new GUIContent($"删除{TrackOf(item.Kind).Name}"), false, () =>
            {
                _selection = target;
                DeleteSelectedItem();
            });
            menu.AddItem(new GUIContent("播放头移到起点"), false, () => { _frame = item.X; Repaint(); });
            menu.ShowAsContext();
        }

        private static T[] Append<T>(T[] array, T item)
        {
            int n = array?.Length ?? 0;
            var result = new T[n + 1];
            if (n > 0) Array.Copy(array, result, n);
            result[n] = item;
            return result;
        }

        private static T[] RemoveAt<T>(T[] array, int index)
        {
            var result = new T[array.Length - 1];
            if (index > 0) Array.Copy(array, 0, result, 0, index);
            if (index < array.Length - 1) Array.Copy(array, index + 1, result, index, array.Length - index - 1);
            return result;
        }

        private void AddItem(TrackKind kind, int frame)
        {
            ActionData a = Current;
            if (a == null) return;
            int total = TotalFrames(a);
            int end = Mathf.Min(frame + 2, total - 1);
            var range = new FrameRange { x = frame, y = Mathf.Max(frame, end) };

            Undo.RecordObject(_state, $"添加{TrackOf(kind).Name}");
            int index;
            switch (kind)
            {
                case TrackKind.AttackPhase:
                {
                    // 新攻击阶段默认对应一个新的伤害段
                    int phase = a.attacks?.Length ?? 0;
                    a.attacks = Append(a.attacks, new AttackData { phase = phase, canHitSameTarget = 1 });
                    a.attackPhase = Append(a.attackPhase, new AttackBoxTurnOnInfo { frameRanges = new[] { range }, tag = Array.Empty<string>(), attackPhase = phase, priority = a.priority });
                    index = a.attackPhase.Length - 1;
                    break;
                }
                case TrackKind.DefensePhase:
                    a.defensePhase = Append(a.defensePhase, new BeHitBoxTurnOnInfo { frameRanges = new[] { range }, tag = Array.Empty<string>(), tempBeCancelledTagTurnOn = Array.Empty<string>() });
                    index = a.defensePhase.Length - 1;
                    break;
                case TrackKind.Armor:
                    a.armorPhase = Append(a.armorPhase, new ArmorPhaseData { range = range, level = ArmorLevel.Super.ToString() });
                    index = a.armorPhase.Length - 1;
                    break;
                case TrackKind.BeCancelled:
                    a.beCancelledTag = Append(a.beCancelledTag, new BeCancelledTagData { frameRange = range, cancelTag = Array.Empty<string>() });
                    index = a.beCancelledTag.Length - 1;
                    break;
                case TrackKind.InputAccept:
                    a.inputAcceptance = Append(a.inputAcceptance, new MoveInputAcceptanceData { range = range, rateX = "0", rateY = "0" });
                    index = a.inputAcceptance.Length - 1;
                    break;
                case TrackKind.RootMotion:
                    a.rootMotion = Append(a.rootMotion, new RootMotionSegmentData { frameRange = range, moveX = "0" });
                    index = a.rootMotion.Length - 1;
                    break;
                case TrackKind.Event:
                    a.events = Append(a.events, new ActionEventData { frame = frame, type = ActionEventType.Effect.ToString() });
                    index = a.events.Length - 1;
                    break;
                default:
                    a.cancelTag = Append(a.cancelTag, new CancelTag { tag = a.id, startFormFrame = frame, priority = a.priority });
                    index = a.cancelTag.Length - 1;
                    break;
            }

            _selection = new ItemRef { Valid = true, Kind = kind, Index = index, Sub = 0 };
            MarkDirty();
            Repaint();
        }

        private void DeleteSelectedItem()
        {
            ActionData a = Current;
            if (a == null || !SelectionValid()) return;

            Undo.RecordObject(_state, $"删除{TrackOf(_selection.Kind).Name}");
            int i = _selection.Index;
            switch (_selection.Kind)
            {
                case TrackKind.AttackPhase:
                    if (a.attackPhase[i].frameRanges.Length > 1) a.attackPhase[i].frameRanges = RemoveAt(a.attackPhase[i].frameRanges, _selection.Sub);
                    else a.attackPhase = RemoveAt(a.attackPhase, i);
                    break;
                case TrackKind.DefensePhase:
                    if (a.defensePhase[i].frameRanges.Length > 1) a.defensePhase[i].frameRanges = RemoveAt(a.defensePhase[i].frameRanges, _selection.Sub);
                    else a.defensePhase = RemoveAt(a.defensePhase, i);
                    break;
                case TrackKind.Armor: a.armorPhase = RemoveAt(a.armorPhase, i); break;
                case TrackKind.BeCancelled: a.beCancelledTag = RemoveAt(a.beCancelledTag, i); break;
                case TrackKind.InputAccept: a.inputAcceptance = RemoveAt(a.inputAcceptance, i); break;
                case TrackKind.RootMotion: a.rootMotion = RemoveAt(a.rootMotion, i); break;
                case TrackKind.Event: a.events = RemoveAt(a.events, i); break;
                case TrackKind.CancelTag: a.cancelTag = RemoveAt(a.cancelTag, i); break;
            }
            _selection = default;
            MarkDirty();
            Repaint();
        }

        #endregion

        #region 条目属性

        private void DrawSelectedItemInspector(ActionData a)
        {
            TrackInfo track = TrackOf(_selection.Kind);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ 动作属性", GUILayout.Width(80))) _selection = default;
                GUILayout.Label($"{track.Name} #{_selection.Index}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("删除", GUILayout.Width(48)))
                {
                    GUI.color = Color.white;
                    DeleteSelectedItem();
                    GUIUtility.ExitGUI();
                }
                GUI.color = Color.white;
            }
            EditorGUILayout.HelpBox(track.Tip, MessageType.None);

            string basePath = ActionPath(_selected);
            SerializedProperty element = _so.FindProperty($"{basePath}.{track.Field}.Array.data[{_selection.Index}]");
            if (element == null) return;
            element.isExpanded = true;
            EditorGUILayout.PropertyField(element, new GUIContent($"{track.Field}[{_selection.Index}]"), true);

            if (_selection.Kind == TrackKind.AttackPhase) DrawLinkedAttack(a, basePath);
            if (_selection.Kind == TrackKind.Event) DrawEventTypeButtons(element);
        }

        /// <summary>
        /// 攻击阶段对应的伤害段 attacks[attackPhase]
        /// </summary>
        private void DrawLinkedAttack(ActionData a, string basePath)
        {
            int phase = a.attackPhase[_selection.Index].attackPhase;
            EditorGUILayout.Space(8);
            SerializedProperty attacks = _so.FindProperty($"{basePath}.attacks");
            if (phase >= 0 && phase < attacks.arraySize)
            {
                SerializedProperty atk = attacks.GetArrayElementAtIndex(phase);
                atk.isExpanded = true;
                EditorGUILayout.PropertyField(atk, new GUIContent($"对应伤害 attacks[{phase}]"), true);
                return;
            }

            EditorGUILayout.HelpBox($"attackPhase = {phase}，但 attacks 只有 {attacks.arraySize} 段。", MessageType.Error);
            if (phase >= 0 && GUILayout.Button($"补齐 attacks 到 [{phase}]"))
            {
                Undo.RecordObject(_state, "补齐伤害段");
                while ((a.attacks?.Length ?? 0) <= phase)
                {
                    a.attacks = Append(a.attacks, new AttackData { phase = a.attacks?.Length ?? 0, canHitSameTarget = 1 });
                }
                MarkDirty();
                GUIUtility.ExitGUI();
            }
        }

        private static void DrawEventTypeButtons(SerializedProperty element)
        {
            SerializedProperty type = element.FindPropertyRelative("type");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("类型", GUILayout.Width(40));
                foreach (string name in new[] { "Effect", "Spawn", "Camera", "Buff" })
                {
                    bool on = string.Equals(type.stringValue, name, StringComparison.OrdinalIgnoreCase) || (name == "Effect" && string.IsNullOrEmpty(type.stringValue));
                    if (GUILayout.Toggle(on, name, EditorStyles.miniButton) && !on) type.stringValue = name;
                }
            }
        }

        #endregion
    }
}
