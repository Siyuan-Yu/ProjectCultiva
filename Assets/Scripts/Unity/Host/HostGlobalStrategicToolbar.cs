using System;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 大地图 Global Strategic Toolbar：系统级战略入口（不依赖 Node／镜头）。
    /// 本轮：Character + Army。未来可注册 Territory / FactionDiplomacy / Trade 等模块。
    /// </summary>
    public sealed class HostGlobalStrategicToolbar
    {
        /// <summary>Global Strategic UI 模块 id（可扩展；未实现模块勿注册到 Entries）。</summary>
        public enum ModuleId
        {
            None = 0,
            Character = 1,
            Army = 2,
            // Reserved — NOT IMPLEMENTED:
            // Territory = 3,
            // FactionDiplomacy = 4,
            // Trade = 5,
            // Mission = 6,
        }

        readonly struct ModuleEntry
        {
            public readonly ModuleId Id;
            public readonly string Label;
            public readonly float Width;

            public ModuleEntry(ModuleId id, string label, float width)
            {
                Id = id;
                Label = label;
                Width = width;
            }
        }

        static readonly ModuleEntry[] ImplementedEntries =
        {
            new ModuleEntry(ModuleId.Character, "角色", 56f),
            new ModuleEntry(ModuleId.Army, "军队", 56f),
        };

        ModuleId _active = ModuleId.None;

        public ModuleId ActiveModule => _active;

        public float LastDrawnWidth { get; private set; }

        public void SetActive(ModuleId moduleId) => _active = moduleId;

        public void CloseAll() => _active = ModuleId.None;

        public void SyncFromPanels(bool characterOpen, bool armyOpen)
        {
            if (characterOpen)
                _active = ModuleId.Character;
            else if (armyOpen)
                _active = ModuleId.Army;
            else
                _active = ModuleId.None;
        }

        /// <summary>绘制工具栏；返回被点击的模块（单击 Toggle 由 Host 处理）。</summary>
        public ModuleId Draw(float x, float y, GUIStyle buttonStyle)
        {
            LastDrawnWidth = 0f;
            var clicked = ModuleId.None;
            var cursor = x;

            GUI.Label(new Rect(cursor, y + 4f, 36f, 22f), "战略", buttonStyle);
            cursor += 40f;
            LastDrawnWidth += 40f;

            for (var i = 0; i < ImplementedEntries.Length; i++)
            {
                var entry = ImplementedEntries[i];
                var selected = _active == entry.Id;
                var prev = GUI.color;
                if (selected)
                    GUI.color = new Color(0.85f, 0.92f, 1f, 1f);

                var rect = new Rect(cursor, y, entry.Width, 26f);
                if (GUI.Button(rect, entry.Label))
                    clicked = entry.Id;

                GUI.color = prev;
                cursor += entry.Width + 8f;
                LastDrawnWidth += entry.Width + 8f;
            }

            return clicked;
        }

        public static bool IsImplemented(ModuleId moduleId)
        {
            for (var i = 0; i < ImplementedEntries.Length; i++)
            {
                if (ImplementedEntries[i].Id == moduleId)
                    return true;
            }

            return false;
        }
    }
}
