using System;
using System.Diagnostics;
using XianXia.Core.Social;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Spawn faction 解析结果来源（debug / warning 分级用）。</summary>
    public enum FactionAssignmentSource
    {
        CharacterDefault,
        ScenarioOverride,
        ExplicitUnaffiliated,
        /// <summary>旧 Content：无 factionMode 但有 factionId/factionRole（或 assignOpeningFaction）。</summary>
        Legacy
    }

    /// <summary>一个 Spawn 最终开局势力意图（不直接写 Entity；由调用方 Apply）。</summary>
    public readonly struct ResolvedFactionAssignment
    {
        public ResolvedFactionAssignment(bool isAffiliated, string factionId, FactionRoleKind role, FactionAssignmentSource source)
        {
            IsAffiliated = isAffiliated;
            FactionId = factionId ?? string.Empty;
            Role = role;
            Source = source;
        }

        public bool IsAffiliated { get; }
        public string FactionId { get; }
        public FactionRoleKind Role { get; }
        public FactionAssignmentSource Source { get; }

        public static readonly ResolvedFactionAssignment Unaffiliated =
            new ResolvedFactionAssignment(false, string.Empty, FactionRoleKind.None, FactionAssignmentSource.ExplicitUnaffiliated);
    }

    /// <summary>
    /// Spawn 势力归属唯一 resolver：
    /// 优先级：① Override → Spawn faction；② Unaffiliated → 明确无势力；③ 否则 → Character default。
    /// 兼容分支（低于正式字段）：无 factionMode 但有 factionId = Legacy Explicit Override；
    /// assignOpeningFaction/openingFactionId = 更老 legacy。
    /// 只解析意图；运行后归属真源始终是 FactionMembershipComponent。
    /// </summary>
    public static class OpeningFactionAssignmentResolver
    {
        /// <summary>错误码：Override 但缺 factionId/factionRole。</summary>
        public const string ErrorMissingOverrideFaction = "Spawn factionMode=Override requires non-empty factionId and a non-None factionRole.";
        public const string ErrorUnaffiliatedWithFaction = "Spawn factionMode=Unaffiliated must not carry factionId/factionRole.";
        public const string ErrorInvalidRole = "factionRole must parse as non-None FactionRoleKind.";

        public static ResolvedFactionAssignment Resolve(
            OpeningSpawnEntry entry,
            CharacterDefinition character,
            string scenarioOpeningFactionId)
        {
            if (entry == null)
                return ResolvedFactionAssignment.Unaffiliated;

            switch (entry.FactionMode)
            {
                case OpeningFactionMode.Override:
                    if (string.IsNullOrWhiteSpace(entry.FactionId))
                        throw new InvalidOperationException(ErrorMissingOverrideFaction + " context=" + entry.DefinitionId);
                    if (!TryParseFactionRole(entry.FactionRole, out var overrideRole))
                        throw new InvalidOperationException(ErrorInvalidRole + " context=" + entry.DefinitionId + ":" + entry.FactionRole);
                    return new ResolvedFactionAssignment(
                        true, entry.FactionId.Trim(), overrideRole, FactionAssignmentSource.ScenarioOverride);

                case OpeningFactionMode.Unaffiliated:
                    if (!string.IsNullOrWhiteSpace(entry.FactionId) || !string.IsNullOrWhiteSpace(entry.FactionRole))
                        throw new InvalidOperationException(ErrorUnaffiliatedWithFaction + " context=" + entry.DefinitionId);
                    return ResolvedFactionAssignment.Unaffiliated;

                case OpeningFactionMode.CharacterDefault:
                default:
                    // Legacy compatibility branch 1: explicit factionId without factionMode → treat as Override.
                    if (!string.IsNullOrWhiteSpace(entry.FactionId))
                    {
                        if (!TryParseFactionRole(entry.FactionRole, out var legacyRole))
                            throw new InvalidOperationException(ErrorInvalidRole + " context=" + entry.DefinitionId + ":" + entry.FactionRole);
                        WarnOnce(
                            "[ContentLegacy] spawn factionId/factionRole without factionMode is deprecated; treating as Override. " +
                            entry.DefinitionId);
                        return new ResolvedFactionAssignment(
                            true, entry.FactionId.Trim(), legacyRole, FactionAssignmentSource.Legacy);
                    }

                    // Legacy compatibility branch 2: assignOpeningFaction / scenario.openingFactionId (oldest).
                    if (entry.AssignOpeningFaction)
                    {
                        if (!TryParseFactionRole(entry.FactionRole, out var legacyRole2))
                            throw new InvalidOperationException(ErrorInvalidRole + " context=" + entry.DefinitionId + ":" + entry.FactionRole);
                        var legacyFactionId = string.IsNullOrWhiteSpace(scenarioOpeningFactionId)
                            ? SocialAlphaConstants.OpeningFactionId
                            : scenarioOpeningFactionId.Trim();
                        WarnOnce(
                            "[ContentLegacy] assignOpeningFaction/openingFactionId is deprecated; use factionMode + factionId/factionRole or CharacterDefault. " +
                            entry.DefinitionId);
                        return new ResolvedFactionAssignment(
                            true, legacyFactionId, legacyRole2, FactionAssignmentSource.Legacy);
                    }

                    // Formal path: inherit CharacterDefinition default.
                    if (character != null && !string.IsNullOrWhiteSpace(character.DefaultFactionId))
                    {
                        if (!TryParseFactionRole(character.DefaultFactionRole, out var defaultRole))
                            throw new InvalidOperationException(
                                "CharacterDefinition.defaultFactionId requires a non-None defaultFactionRole. context=" +
                                character.Id);
                        return new ResolvedFactionAssignment(
                            true, character.DefaultFactionId.Trim(), defaultRole, FactionAssignmentSource.CharacterDefault);
                    }

                    // Character default empty → stays unaffiliated.
                    return new ResolvedFactionAssignment(
                        false, string.Empty, FactionRoleKind.None, FactionAssignmentSource.CharacterDefault);
            }
        }

        static readonly System.Collections.Generic.HashSet<string> WarnedLegacyContexts =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        internal static void WarnOnce(string message)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            if (WarnedLegacyContexts.Add(message))
                Trace.TraceWarning(message);
#endif
        }

        internal static bool TryParseFactionRole(string text, out FactionRoleKind role)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                role = FactionRoleKind.None;
                return false;
            }
            return Enum.TryParse(text.Trim(), ignoreCase: true, out role) && role != FactionRoleKind.None;
        }
    }
}
