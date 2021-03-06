using System;
using System.Collections.Generic;
using HarmonyLib;
using UnhollowerBaseLib;
using UnityEngine;
using Object = Il2CppSystem.Object;
using Type = Il2CppSystem.Type;

namespace Reactor.Unstrip
{
    internal static class Patches
    {
        [HarmonyPatch(typeof(GUIUtility), nameof(GUIUtility.HasKeyFocus))]
        public static class HasKeyFocusPatch
        {
            public static bool Prefix(int controlID, out bool __result)
            {
                __result = controlID == GUIUtility.keyboardControl;
                return false;
            }
        }

        [HarmonyPatch(typeof(GUILayout), nameof(GUILayout.DoWindow), typeof(int), typeof(Rect), typeof(GUI.WindowFunction), typeof(GUIContent), typeof(GUIStyle), typeof(Il2CppReferenceArray<GUILayoutOption>))]
        public static class DoWindowPatch
        {
            private sealed class LayoutedWindow
            {
                internal LayoutedWindow(GUI.WindowFunction f, Rect screenRect, GUIContent content, GUILayoutOption[] options, GUIStyle style)
                {
                    this.m_Func = f;
                    this.m_ScreenRect = screenRect;
                    this.m_Options = options;
                    this.m_Style = style;
                }

                public void DoWindow(int windowID)
                {
                    var topLevel = GUILayoutUtility.current.topLevel;
                    var type = Event.current.type;
                    if (type != EventType.Layout)
                    {
                        topLevel.ResetCursor();
                    }
                    else
                    {
                        topLevel.resetCoords = true;
                        topLevel.rect = this.m_ScreenRect;
                        var flag = this.m_Options != null;
                        if (flag)
                        {
                            topLevel.ApplyOptions(this.m_Options);
                        }

                        topLevel.isWindow = true;
                        topLevel.windowID = windowID;
                        topLevel.style = this.m_Style;
                    }

                    this.m_Func.Invoke(windowID);
                }

                private readonly GUI.WindowFunction m_Func;
                private readonly Rect m_ScreenRect;
                private readonly GUILayoutOption[] m_Options;
                private readonly GUIStyle m_Style;
            }

            public static bool Prefix(int id, Rect screenRect, GUI.WindowFunction func, GUIContent content, GUIStyle style, Il2CppReferenceArray<GUILayoutOption> options, out Rect __result)
            {
                GUIUtility.CheckOnGUI();
                var @object = new LayoutedWindow(func, screenRect, content, options, style);
                __result = GUI.Window(id, screenRect, (GUI.WindowFunction) @object.DoWindow, content, style);

                return false;
            }
        }

        [HarmonyPatch(typeof(GUILayoutGroup), nameof(GUILayoutGroup.Add))]
        public static class AddPatch
        {
            public static bool Prefix(GUILayoutGroup __instance, GUILayoutEntry e)
            {
                __instance.entries.Add(e);
                return false;
            }
        }

        [HarmonyPatch(typeof(GUILayoutGroup), nameof(GUILayoutGroup.GetNext))]
        public static class GetNextPatch
        {
            public static bool Prefix(GUILayoutGroup __instance, ref GUILayoutEntry __result)
            {
                if (__instance.m_Cursor < __instance.entries.Count)
                {
                    __result = __instance.entries[__instance.m_Cursor];
                    __instance.m_Cursor++;
                    return false;
                }

                throw new ArgumentException(string.Concat("Getting control ", __instance.m_Cursor, "'s position in a group with only ", __instance.entries.Count, " controls when doing ", Event.current.rawType, "\nAborting"));
            }
        }

        // [HarmonyPatch(typeof(GUILayoutEntry), nameof(GUILayoutEntry.CalcWidth))]
        public static class CalcWidthPatch
        {
            public static bool Prefix(GUILayoutEntry __instance)
            {
                var cast = __instance?.TryCast<GUIWordWrapSizer>();
                if (cast != null)
                {
                    cast.CalcWidth();
                    return false;
                }

                return true;
            }
        }

        // [HarmonyPatch(typeof(GUILayoutEntry), nameof(GUILayoutEntry.CalcHeight))]
        public static class CalcHeightPatch
        {
            public static bool Prefix(GUILayoutEntry __instance)
            {
                var cast = __instance?.TryCast<GUIWordWrapSizer>();
                if (cast != null)
                {
                    cast.CalcHeight();
                    return false;
                }

                return true;
            }
        }

        [RegisterInIl2Cpp]
        internal sealed class GUIWordWrapSizer : GUILayoutEntry
        {
            private readonly GUIContent m_Content;
            private readonly float m_ForcedMinHeight;
            private readonly float m_ForcedMaxHeight;

            public GUIWordWrapSizer(IntPtr ptr) : base(ptr)
            {
                CalcWidth();
                CalcHeight();
            }

            public GUIWordWrapSizer(GUIStyle style, GUIContent content, GUILayoutOption[] options)
                : base(0.0f, 0.0f, 0.0f, 0.0f, style)
            {
                m_Content = new GUIContent(content.text, content.image, content.tooltip);
                ApplyOptions(options);
                m_ForcedMinHeight = minHeight;
                m_ForcedMaxHeight = maxHeight;

                CalcWidth();
                CalcHeight();
            }

            public new void CalcWidth()
            {
                if (this.minWidth != 0.0 && this.maxWidth != 0.0)
                    return;
                float minWidth;
                float maxWidth;
                style.CalcMinMaxWidth(m_Content, out minWidth, out maxWidth);
                minWidth = Mathf.Ceil(minWidth);
                maxWidth = Mathf.Ceil(maxWidth);
                if (this.minWidth == 0.0)
                    this.minWidth = minWidth;
                if (this.maxWidth == 0.0)
                    this.maxWidth = maxWidth;
            }

            public new void CalcHeight()
            {
                if (m_ForcedMinHeight != 0.0 && m_ForcedMaxHeight != 0.0)
                    return;
                var num = style.CalcHeight(m_Content, rect.width);
                if (m_ForcedMinHeight == 0.0)
                    minHeight = num;
                else
                    minHeight = m_ForcedMinHeight;
                if (m_ForcedMaxHeight == 0.0)
                    maxHeight = num;
                else
                    maxHeight = m_ForcedMaxHeight;
            }
        }

        [HarmonyPatch(typeof(GUILayoutUtility), nameof(GUILayoutUtility.DoGetRect), typeof(GUIContent), typeof(GUIStyle), typeof(Il2CppReferenceArray<GUILayoutOption>))]
        public static class DoGetRectPatch
        {
            public static bool Prefix(GUIContent content, GUIStyle style, Il2CppReferenceArray<GUILayoutOption> options, ref Rect __result)
            {
                GUIUtility.CheckOnGUI();
                var type = Event.current.type;
                if (type != EventType.Layout)
                {
                    if (type != EventType.Used)
                    {
                        var next = GUILayoutUtility.current.topLevel.GetNext();
                        __result = next.rect;
                    }
                    else
                    {
                        __result = GUILayoutUtility.kDummyRect;
                    }
                }
                else
                {
                    var isHeightDependantOnWidth = style.isHeightDependantOnWidth;
                    if (isHeightDependantOnWidth)
                    {
                        GUILayoutUtility.current.topLevel.Add(new GUIWordWrapSizer(style, content, options));
                    }
                    else
                    {
                        var constraints = new Vector2(0f, 0f);
                        if (options != null)
                        {
                            foreach (var guilayoutOption in options)
                            {
                                var type2 = guilayoutOption.type;
                                if (type2 != GUILayoutOption.Type.maxWidth)
                                {
                                    if (type2 == GUILayoutOption.Type.maxHeight)
                                    {
                                        constraints.y = guilayoutOption.value.Unbox<float>();
                                    }
                                }
                                else
                                {
                                    constraints.x = guilayoutOption.value.Unbox<float>();
                                }
                            }
                        }

                        var vector = style.CalcSizeWithConstraints(content, constraints);
                        vector.x = Mathf.Ceil(vector.x);
                        vector.y = Mathf.Ceil(vector.y);
                        GUILayoutUtility.current.topLevel.Add(new GUILayoutEntry(vector.x, vector.x, vector.y, vector.y, style));
                    }

                    __result = GUILayoutUtility.kDummyRect;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(GUIUtility), nameof(GUIUtility.GetStateObject))]
        public static class GetStateObjectPatch
        {
            private static Dictionary<int, Object> s_StateCache = new Dictionary<int, Object>();

            public static bool Prefix(Type t, int controlID, out Object __result)
            {
                if (!s_StateCache.TryGetValue(controlID, out __result) || __result.GetIl2CppType() != t)
                {
                    s_StateCache[controlID] = __result = t.GetConstructor(new Il2CppReferenceArray<Type>(0)).Invoke(null, new Il2CppReferenceArray<Object>(0));
                }

                return false;
            }
        }
    }
}
