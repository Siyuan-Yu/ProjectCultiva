import { useMemo, useState } from 'react';
import type { JsonDict, PackageState } from '../lib/types';
import { asArray, suggestQuestFile, upsertDefinition } from '../lib/packageIo';
import { ConditionList, OutcomeList } from '../components/ConditionOutcome';

export function QuestPage(props: {
  state: PackageState;
  selectedId?: string;
  onChange: (next: PackageState, dirtyId: string) => void;
  dirty: boolean;
  onSave: () => void;
}) {
  const quests = useMemo(() => props.state.defs.filter((d) => d.type === 'quest'), [props.state]);
  const [qid, setQid] = useState(props.selectedId || quests[0]?.id || '');
  const quest = quests.find((q) => q.id === qid) || quests[0];
  const locationIds = useMemo(() => {
    const ids: string[] = [];
    for (const d of props.state.defs) {
      if (d.type !== 'worldRegion') continue;
      for (const loc of asArray(d.raw.locations)) {
        if (typeof loc.id === 'string') ids.push(loc.id);
      }
    }
    return ids;
  }, [props.state]);

  const patch = (raw: JsonDict, previousId?: string) => {
    if (!quest) return;
    const nextId = String(raw.id ?? quest.id);
    props.onChange(
      upsertDefinition(
        props.state,
        {
          ...quest,
          raw,
          name: String(raw.name ?? quest.name),
          id: nextId
        },
        previousId || quest.id
      ),
      nextId
    );
    if (nextId !== qid) setQid(nextId);
  };

  if (!quest) return <div className="page"><p>无任务定义。</p></div>;

  return (
    <div className="page">
      <header className="toolbar">
        <label>
          任务
          <select value={quest.id} onChange={(e) => setQid(e.target.value)}>
            {quests.map((q) => (
              <option key={q.id} value={q.id}>{q.id}</option>
            ))}
          </select>
        </label>
        <button
          type="button"
          onClick={() => {
            const id = `base:quest_new_${Date.now().toString(36)}`;
            const raw: JsonDict = {
              id,
              type: 'quest',
              name: '新任务',
              description: '',
              autoOffer: true,
              offerConditions: [],
              completeConditions: [],
              failConditions: [],
              rewards: [],
              failResults: []
            };
            const next = upsertDefinition(props.state, {
              id,
              type: 'quest',
              name: '新任务',
              filePath: suggestQuestFile(props.state),
              index: -1,
              raw
            });
            props.onChange(next, id);
            setQid(id);
          }}
        >
          + 新任务
        </button>
        <button type="button" className="primary" disabled={!props.dirty} onClick={props.onSave}>
          保存到磁盘
        </button>
      </header>

      <div className="meta">
        <label>
          id
          <input value={String(quest.raw.id ?? '')} onChange={(e) => patch({ ...quest.raw, id: e.target.value })} />
        </label>
        <label>
          name
          <input value={String(quest.raw.name ?? '')} onChange={(e) => patch({ ...quest.raw, name: e.target.value })} />
        </label>
        <label className="check">
          <input
            type="checkbox"
            checked={Boolean(quest.raw.autoOffer)}
            onChange={(e) => patch({ ...quest.raw, autoOffer: e.target.checked })}
          />
          autoOffer（条件满足自动接取）
        </label>
      </div>

      <label className="full">
        description
        <textarea
          value={String(quest.raw.description ?? '')}
          onChange={(e) => patch({ ...quest.raw, description: e.target.value })}
          rows={3}
        />
      </label>

      <ConditionList
        title="接取条件 offerConditions"
        rows={asArray(quest.raw.offerConditions)}
        locationIds={locationIds}
        onChange={(offerConditions) => patch({ ...quest.raw, offerConditions })}
      />
      <ConditionList
        title="完成条件 completeConditions"
        rows={asArray(quest.raw.completeConditions)}
        locationIds={locationIds}
        onChange={(completeConditions) => patch({ ...quest.raw, completeConditions })}
      />
      <OutcomeList
        title="奖励 rewards"
        rows={asArray(quest.raw.rewards)}
        onChange={(rewards) => patch({ ...quest.raw, rewards })}
      />
      <ConditionList
        title="失败条件 failConditions"
        rows={asArray(quest.raw.failConditions)}
        locationIds={locationIds}
        onChange={(failConditions) => patch({ ...quest.raw, failConditions })}
      />
      <OutcomeList
        title="失败结果 failResults"
        rows={asArray(quest.raw.failResults)}
        onChange={(failResults) => patch({ ...quest.raw, failResults })}
      />
    </div>
  );
}
