import { useMemo, useState } from 'react';
import type { JsonDict, PackageState } from '../lib/types';
import { asArray, suggestEventFile, upsertDefinition } from '../lib/packageIo';
import { ConditionList, OutcomeList } from '../components/ConditionOutcome';
import { EVENT_TRIGGERS } from '../lib/schemaFields';

export function EventPage(props: {
  state: PackageState;
  selectedId?: string;
  onChange: (next: PackageState, dirtyId: string) => void;
  dirty: boolean;
  onSave: () => void;
}) {
  const events = useMemo(() => props.state.defs.filter((d) => d.type === 'contentEvent'), [props.state]);
  const [eid, setEid] = useState(props.selectedId || events[0]?.id || '');
  const ev = events.find((e) => e.id === eid) || events[0];
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

  const patch = (raw: JsonDict) => {
    if (!ev) return;
    const nextId = String(raw.id ?? ev.id);
    props.onChange(
      upsertDefinition(
        props.state,
        {
          ...ev,
          raw,
          name: String(raw.name ?? ev.name),
          id: nextId
        },
        ev.id
      ),
      nextId
    );
    if (nextId !== eid) setEid(nextId);
  };

  if (!ev) return <div className="page"><p>无事件定义。</p></div>;

  const choices = asArray(ev.raw.choices);

  return (
    <div className="page">
      <header className="toolbar">
        <label>
          事件
          <select value={ev.id} onChange={(e) => setEid(e.target.value)}>
            {events.map((e) => (
              <option key={e.id} value={e.id}>{e.id}</option>
            ))}
          </select>
        </label>
        <button
          type="button"
          onClick={() => {
            const id = `base:event_new_${Date.now().toString(36)}`;
            const raw: JsonDict = {
              id,
              type: 'contentEvent',
              name: '新事件',
              body: '',
              trigger: 'manual',
              once: true,
              conditions: [],
              choices: [{ id: 'choice_a', text: '选项 A', conditions: [], outcomes: [] }]
            };
            props.onChange(
              upsertDefinition(props.state, {
                id,
                type: 'contentEvent',
                name: '新事件',
                filePath: suggestEventFile(props.state),
                index: -1,
                raw
              }),
              id
            );
            setEid(id);
          }}
        >
          + 新事件
        </button>
        <button type="button" className="primary" disabled={!props.dirty} onClick={props.onSave}>
          保存到磁盘
        </button>
      </header>

      <div className="meta">
        <label>
          id
          <input value={String(ev.raw.id ?? '')} onChange={(e) => patch({ ...ev.raw, id: e.target.value })} />
        </label>
        <label>
          name
          <input value={String(ev.raw.name ?? '')} onChange={(e) => patch({ ...ev.raw, name: e.target.value })} />
        </label>
        <label>
          trigger
          <select
            value={String(ev.raw.trigger ?? 'manual')}
            onChange={(e) => patch({ ...ev.raw, trigger: e.target.value })}
          >
            {EVENT_TRIGGERS.map((t) => (
              <option key={t} value={t}>{t}</option>
            ))}
          </select>
        </label>
        <label>
          locationId
          <select
            value={String(ev.raw.locationId ?? '')}
            onChange={(e) => {
              const v = e.target.value;
              const next = { ...ev.raw };
              if (v) next.locationId = v;
              else delete next.locationId;
              patch(next);
            }}
          >
            <option value="">（无）</option>
            {locationIds.map((id) => (
              <option key={id} value={id}>{id}</option>
            ))}
          </select>
        </label>
        <label>
          questId
          <input
            value={String(ev.raw.questId ?? '')}
            onChange={(e) => {
              const next = { ...ev.raw };
              if (e.target.value) next.questId = e.target.value;
              else delete next.questId;
              patch(next);
            }}
          />
        </label>
        <label className="check">
          <input
            type="checkbox"
            checked={ev.raw.once !== false}
            onChange={(e) => patch({ ...ev.raw, once: e.target.checked })}
          />
          once
        </label>
      </div>

      <label className="full">
        body（正文）
        <textarea
          value={String(ev.raw.body ?? '')}
          onChange={(e) => patch({ ...ev.raw, body: e.target.value })}
          rows={5}
        />
      </label>

      <ConditionList
        title="触发条件 conditions"
        rows={asArray(ev.raw.conditions)}
        locationIds={locationIds}
        onChange={(conditions) => patch({ ...ev.raw, conditions })}
      />

      <div className="block">
        <div className="block-head">
          <strong>选项 choices</strong>
          <button
            type="button"
            onClick={() =>
              patch({
                ...ev.raw,
                choices: [
                  ...choices,
                  {
                    id: `choice_${choices.length + 1}`,
                    text: '新选项',
                    conditions: [],
                    outcomes: []
                  }
                ]
              })
            }
          >
            + 选项
          </button>
        </div>
        {choices.map((ch, i) => (
          <div className="card" key={i}>
            <div className="meta">
              <label>
                id
                <input
                  value={String(ch.id ?? '')}
                  onChange={(e) => {
                    const next = choices.map((c, idx) => (idx === i ? { ...c, id: e.target.value } : c));
                    patch({ ...ev.raw, choices: next });
                  }}
                />
              </label>
              <label className="grow">
                text
                <input
                  value={String(ch.text ?? '')}
                  onChange={(e) => {
                    const next = choices.map((c, idx) => (idx === i ? { ...c, text: e.target.value } : c));
                    patch({ ...ev.raw, choices: next });
                  }}
                />
              </label>
              <button
                type="button"
                className="danger"
                onClick={() => patch({ ...ev.raw, choices: choices.filter((_, j) => j !== i) })}
              >
                删选项
              </button>
            </div>
            <ConditionList
              title="选项可见条件 conditions"
              rows={asArray(ch.conditions)}
              locationIds={locationIds}
              onChange={(conditions) => {
                const next = choices.map((c, idx) => (idx === i ? { ...c, conditions } : c));
                patch({ ...ev.raw, choices: next });
              }}
            />
            <OutcomeList
              title="结果 outcomes"
              rows={asArray(ch.outcomes)}
              onChange={(outcomes) => {
                const next = choices.map((c, idx) => (idx === i ? { ...c, outcomes } : c));
                patch({ ...ev.raw, choices: next });
              }}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
