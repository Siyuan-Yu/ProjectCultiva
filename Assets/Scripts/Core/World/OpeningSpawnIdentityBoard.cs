using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.World
{
    /// <summary>
    /// OpeningScenario spawn 的稳定 authored identity：<c>EntityId → SpawnStableKey</c>。
    ///
    /// <para>
    /// <b>SpawnStableKey ≠ DefinitionId。</b> 同一 Definition 在同一 Site 多次 spawn 时，
    /// DefinitionId 不唯一；把 DefinitionId 当 spawnKey 会让第二个实例直接吃第一个实例的
    /// baked anchor（first-match）。因此 GameStart 时按 authored spawn 顺序为每个 spawn 建立
    /// 稳定 key，并允许从 spawned Entity 反查。
    /// </para>
    ///
    /// <para>
    /// key 形式：<c>definitionId</c>（该 Definition 的 authored index = 0，兼容既有 checked-in
    /// content）或 <c>definitionId#n</c>（n ≥ 1）。确定性来源是 Content 的 spawn 顺序，
    /// <b>绝不</b>使用 Unity InstanceId／随机数／runtime 到达顺序。
    /// </para>
    ///
    /// <para>Session-only：不进入 Snapshot schema。旧存档缺 key 时调用方按 index 0 回退。</para>
    /// </summary>
    public sealed class OpeningSpawnIdentityBoard
    {
        public const char IndexSeparator = '#';

        readonly Dictionary<ulong, string> _byEntity = new Dictionary<ulong, string>();
        readonly Dictionary<string, ulong> _byKey = new Dictionary<string, ulong>(StringComparer.Ordinal);

        public int Count => _byEntity.Count;

        public void Clear()
        {
            _byEntity.Clear();
            _byKey.Clear();
        }

        /// <summary>稳定 key 构造：index 0 省略后缀（既有 checked-in content 的 key 形态）。</summary>
        public static string BuildStableKey(string definitionId, int authoredIndex)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return string.Empty;
            return authoredIndex <= 0
                ? definitionId
                : definitionId + IndexSeparator + authoredIndex;
        }

        /// <summary>解析 key 内的 authored index；无后缀＝0。非法（空／负／非数字后缀）→ false。</summary>
        public static bool TryParseAuthoredIndex(string spawnKey, string definitionId, out int authoredIndex)
        {
            authoredIndex = 0;
            if (string.IsNullOrWhiteSpace(spawnKey) || string.IsNullOrWhiteSpace(definitionId))
                return false;
            if (string.Equals(spawnKey, definitionId, StringComparison.Ordinal))
                return true;

            var prefix = definitionId + IndexSeparator;
            if (!spawnKey.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            var tail = spawnKey.Substring(prefix.Length);
            if (tail.Length == 0)
                return false;
            for (var i = 0; i < tail.Length; i++)
                if (tail[i] < '0' || tail[i] > '9')
                    return false;
            if (!int.TryParse(tail, out authoredIndex) || authoredIndex < 0)
                return false;
            return true;
        }

        public void Register(EntityId id, string spawnKey)
        {
            if (id.IsNone || string.IsNullOrWhiteSpace(spawnKey))
                return;
            _byEntity[id.Value] = spawnKey;
            _byKey[spawnKey] = id.Value;
        }

        public bool TryGetSpawnKey(EntityId id, out string spawnKey)
        {
            spawnKey = string.Empty;
            if (id.IsNone)
                return false;
            return _byEntity.TryGetValue(id.Value, out spawnKey) && !string.IsNullOrEmpty(spawnKey);
        }

        public bool TryGetEntity(string spawnKey, out EntityId id)
        {
            id = EntityId.None;
            if (string.IsNullOrWhiteSpace(spawnKey) || !_byKey.TryGetValue(spawnKey, out var value))
                return false;
            id = new EntityId(value);
            return true;
        }
    }
}
