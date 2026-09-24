using System;
using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Results;

namespace XianXia.Data.Content
{
    // Deliberately local to ContentEvent: not a general graph framework.
    internal static class ContentEventStructureValidator
    {
        public static void Validate(ContentEventDefinition evt, ValidationReport report)
        {
            var ctx = evt.Id.ToString();
            Action<string> error = message => report.Add(ErrorCode.ContentLoadFailed, message, ctx);
            if (evt.OnceScope != "global" && evt.OnceScope != "perTarget" && evt.OnceScope != "perActorTarget")
                error("onceScope must be global / perTarget / perActorTarget.");
            if (string.Equals(evt.Trigger, "onInspect", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(evt.WorldObjectKind))
                    error("onInspect requires worldObjectKind.");
                if (!WorldObjectKinds.Contains(evt.WorldObjectKind ?? string.Empty))
                    error("worldObjectKind is invalid: " + evt.WorldObjectKind);
            }
            var steps = new Dictionary<string, ContentEventStepSpec>(StringComparer.Ordinal);
            foreach (var step in evt.Steps)
                if (string.IsNullOrWhiteSpace(step.Id) || steps.ContainsKey(step.Id)) error("step.id must be non-empty and unique: " + step.Id);
                else steps.Add(step.Id, step);
            if (evt.Steps.Count == 0)
            {
                if (!string.IsNullOrEmpty(evt.EntryStepId)) error("entryStepId requires steps.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var c in evt.Choices)
                {
                    ValidateChoice(c.Id, c.UnavailableMode, c.NextStepId, c.Outcomes, ids, steps, error);
                }
                return;
            }
            if (!string.IsNullOrWhiteSpace(evt.Body) || evt.Choices.Count > 0) error("steps cannot coexist with legacy body / choices.");
            if (!steps.ContainsKey(evt.EntryStepId ?? "")) error("entryStepId must reference a step.");
            foreach (var step in evt.Steps)
            {
                CheckNext(step.NextStepId, steps, error);
                if (step.Choices.Count > 0 && !string.IsNullOrEmpty(step.NextStepId)) error("Step with choices must not set nextStepId: " + step.Id);
                // Starting a minigame is an explicit terminal choice, never a step side effect.
                if (HasMinigame(step.Outcomes)) error("startMinigame must be on a terminal choice: " + step.Id);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var c in step.Choices)
                    ValidateChoice(c.Id, c.UnavailableMode, c.NextStepId, c.Outcomes, ids, steps, error);
            }
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in steps.Keys) Visit(id, steps, visiting, done, error);
            if (steps.ContainsKey(evt.EntryStepId ?? "") && !HasTerminal(evt.EntryStepId, steps, new HashSet<string>(StringComparer.Ordinal)))
                error("Entry step must reach at least one terminal.");
        }

        static void ValidateChoice(string id, string mode, string next, IReadOnlyList<ContentOutcome> outcomes,
            HashSet<string> ids, Dictionary<string, ContentEventStepSpec> steps, Action<string> error)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) error("choice.id must be non-empty and unique within its step: " + id);
            if (mode != "disabled" && mode != "hidden") error("unavailableMode must be disabled / hidden: " + id);
            CheckNext(next, steps, error);
            if (!string.IsNullOrEmpty(next) && HasMinigame(outcomes)) error("startMinigame choice must be terminal: " + id);
        }
        static bool HasMinigame(IReadOnlyList<ContentOutcome> outcomes)
        {
            foreach (var o in outcomes) if (string.Equals(o.Kind?.Trim(), "startMinigame", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        static void CheckNext(string next, Dictionary<string, ContentEventStepSpec> steps, Action<string> error)
        {
            if (!string.IsNullOrEmpty(next) && !steps.ContainsKey(next)) error("nextStepId missing in this event: " + next);
        }
        static IEnumerable<string> Next(ContentEventStepSpec step)
        {
            if (step.Choices.Count == 0) yield return step.NextStepId;
            else foreach (var choice in step.Choices) yield return choice.NextStepId;
        }
        static void Visit(string id, Dictionary<string, ContentEventStepSpec> steps, HashSet<string> visiting, HashSet<string> done, Action<string> error)
        {
            if (string.IsNullOrEmpty(id) || !steps.ContainsKey(id) || done.Contains(id)) return;
            if (!visiting.Add(id)) { error("Step cycle is forbidden: " + id); return; }
            foreach (var next in Next(steps[id])) Visit(next, steps, visiting, done, error);
            visiting.Remove(id); done.Add(id);
        }
        static bool HasTerminal(string id, Dictionary<string, ContentEventStepSpec> steps, HashSet<string> seen)
        {
            if (!seen.Add(id) || !steps.TryGetValue(id, out var step)) return false;
            foreach (var next in Next(step))
                if (string.IsNullOrEmpty(next) || HasTerminal(next, steps, seen)) return true;
            return false;
        }

        internal static readonly HashSet<string> WorldObjectKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "controlCore", "factionFlag", "farmPlot", "destructible",
            "housing", "workArea", "recoverySpot", "storageRoom", "opportunityObject"
        };
    }
}
