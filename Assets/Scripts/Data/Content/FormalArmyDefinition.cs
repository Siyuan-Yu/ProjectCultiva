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

        /// <summary>
        /// 创建成员时放置的合法 assembly Site，也是没有 InitialHex 时的默认开局物理位置
        /// （FormalArmy.WorldMotion = AtWorldSite）。
        /// </summary>
        public string AssemblySiteId { get; set; } = string.Empty;

        /// <summary>
        /// Optional authored initial wilderness deployment：创建完成且注册后立刻把
        /// FormalArmy 部署到该 Hex（FormalArmy.WorldMotion = Hex authority）。
        /// 以 null 为 presence authority（(0,0) 也是合法 hex）。
        /// </summary>
        public FormalArmyInitialHexDefinition InitialHex { get; set; }

        public List<FormalArmyMemberDefinition> Members { get; set; }
            = new List<FormalArmyMemberDefinition>();
    }

    /// <summary>FormalArmy 可选初始 Hex（axial q/r）。</summary>
    public sealed class FormalArmyInitialHexDefinition
    {
        public int Q { get; set; }
        public int R { get; set; }
    }

    public sealed class FormalArmyMemberDefinition
    {
        public string CharacterDefinitionId { get; set; } = string.Empty;

        /// <summary>同名 CharacterDefinition 可被多个成员实例复用，靠 DisplayName 区分。</summary>
        public string DisplayName { get; set; } = string.Empty;

        public bool Leader { get; set; }
    }
}
