using System.Text;
using XianXia.Core.Combat;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>
    /// Development-only Runtime Mutable Truth fingerprint for Snapshot Before/After Diff.
    /// Excludes Presentation InstanceId／GameObject／Derived CurrentHex.
    /// </summary>
    public static class SnapshotRuntimeFingerprint
    {
        public static string Build(SimulationWorld world, PlayerPartyRuntime party = null)
        {
            var sb = new StringBuilder(2048);
            if (world == null)
                return "world=null";

            sb.Append("tick=").Append(world.Tick.Value);
            sb.Append(" inventoryFilled=").Append(CountFilledInventorySlots(world));
            sb.Append(" relationshipEvents=").Append(world.Relationships?.EventCount ?? 0);
            sb.AppendLine();

            if (party != null && party.HasActive)
            {
                sb.Append("party.active=").Append(party.ActiveCharacterId.Value);
                sb.Append(" members=");
                for (var i = 0; i < party.Members.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(party.Members[i].Value);
                }

                sb.AppendLine();
            }

            foreach (var entity in world.Entities.All)
            {
                if (entity == null || (entity.Tags & EntityTag.Character) == 0)
                    continue;
                AppendCharacter(sb, world, entity);
            }

            if (world.Strategic?.FormalArmies != null)
            {
                foreach (var kv in world.Strategic.FormalArmies.Armies)
                {
                    var army = kv.Value;
                    if (army == null)
                        continue;
                    sb.Append("army=").Append(army.ArmyId);
                    sb.Append(" faction=").Append(army.FactionId ?? string.Empty);
                    sb.Append(" leader=").Append(army.LeaderCharacterId.Value);
                    sb.Append(" state=").Append((int)army.State);
                    sb.Append(" members=").Append(army.MemberCharacterIds.Count);
                    sb.AppendLine();
                }
            }

            if (world.Strategic?.Sites != null)
            {
                foreach (var kv in world.Strategic.Sites.Sites)
                {
                    var site = kv.Value;
                    if (site == null)
                        continue;
                    sb.Append("site=").Append(site.SiteId);
                    sb.Append(" owner=").Append(site.OwnerFactionId ?? string.Empty);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        static void AppendCharacter(StringBuilder sb, SimulationWorld world, Entity entity)
        {
            sb.Append("char=").Append(entity.Id.Value);
            sb.Append(" def=").Append(entity.DefinitionId);

            if (entity.TryGet<FactionMembershipComponent>(out var faction) && faction != null)
            {
                sb.Append(" faction=").Append(faction.FactionId ?? string.Empty);
                sb.Append('/').Append((int)faction.Role);
            }
            else
                sb.Append(" faction=-");

            if (entity.TryGet<LifecycleComponent>(out var life) && life != null)
            {
                sb.Append(" life=").Append((int)life.State);
                sb.Append('@').Append(life.BleedOutAfterTick);
            }
            else
                sb.Append(" life=-");

            if (entity.TryGet<CombatVitalsComponent>(out var vitals) && vitals != null)
            {
                sb.Append(" hp=").Append(vitals.CurrentHp);
                if (vitals.PoolsInitialized)
                    sb.Append('*');
                sb.Append(" sp=").Append(vitals.CurrentSpiritPower);
            }
            else
                sb.Append(" hp=-");

            if (world.WorldPresence != null &&
                world.WorldPresence.TryGet(entity.Id, out var presence) &&
                presence != null)
            {
                sb.Append(" presence=").Append((int)presence.Mode);
                if (!string.IsNullOrEmpty(presence.SiteId))
                    sb.Append(':').Append(presence.SiteId);
            }

            if (entity.TryGet<ArmyMembershipComponent>(out var armyMem) &&
                armyMem != null &&
                armyMem.IsInArmy)
                sb.Append(" army=").Append(armyMem.ArmyId);

            sb.AppendLine();
        }

        static int CountFilledInventorySlots(SimulationWorld world)
        {
            if (world.Inventory == null)
                return 0;
            var n = 0;
            var slots = world.Inventory.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                    n++;
            }

            return n;
        }
    }
}
