using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Content 驱动的 FormalArmy 定义（Phase 5S：Prototype Bandit 迁移）。
    /// 只声明 stable runtime ids / faction / 成员组成；Attack、Defense、MaxHp、Realm
    /// 全部来自成员 <see cref="CharacterDefinition"/>，禁止在 Army 定义里复制。
    /// </summary>
    public sealed class FormalArmyDefinition
    {
        public DefinitionId Id { get; set; }

        /// <summary>展示名（WorldMap ArmyStack 显示）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>当前 authored fixed army 的稳定 runtime id（与迁移前完全一致）。</summary>
        public string RuntimeArmyId { get; set; } = string.Empty;

        /// <summary>ArmyStack 兼容视图 id（与迁移前完全一致）。</summary>
        public string RuntimeStackId { get; set; } = string.Empty;

        public string FactionId { get; set; } = string.Empty;

        /// <summary>创建成员时先放置于该 Site，再由 scenario placement policy 移至实际 Hex。</summary>
        public string AssemblySiteId { get; set; } = string.Empty;

        public List<FormalArmyMemberDefinition> Members { get; set; }
            = new List<FormalArmyMemberDefinition>();
    }

    public sealed class FormalArmyMemberDefinition
    {
        public string CharacterDefinitionId { get; set; } = string.Empty;

        /// <summary>同名 CharacterDefinition 可被多个成员实例复用，靠 DisplayName 区分。</summary>
        public string DisplayName { get; set; } = string.Empty;

        public bool Leader { get; set; }
    }
}
