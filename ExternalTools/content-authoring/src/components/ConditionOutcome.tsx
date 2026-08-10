import type { JsonDict } from '../lib/types';
import { CONDITION_KINDS, OUTCOME_KINDS } from '../lib/schemaFields';

type Row = JsonDict;

export function ConditionList(props: {
  title: string;
  rows: Row[];
  onChange: (rows: Row[]) => void;
  locationIds?: string[];
}) {
  const rows = props.rows || [];
  const update = (i: number, patch: Row) => {
    const next = rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r));
    props.onChange(next);
  };
  return (
    <div className="block">
      <div className="block-head">
        <strong>{props.title}</strong>
        <button type="button" onClick={() => props.onChange([...rows, { kind: 'storyFlag', id: '' }])}>
          + 条件
        </button>
      </div>
      {rows.map((row, i) => (
        <div className="row-edit" key={i}>
          <select
            value={String(row.kind ?? '')}
            onChange={(e) => update(i, { kind: e.target.value })}
          >
            {CONDITION_KINDS.filter((k, idx, arr) => arr.indexOf(k) === idx).map((k) => (
              <option key={k} value={k}>{k}</option>
            ))}
          </select>
          {(String(row.kind).includes('Location') || row.kind === 'atLocation') && props.locationIds ? (
            <select value={String(row.id ?? '')} onChange={(e) => update(i, { id: e.target.value })}>
              <option value="">（选地点）</option>
              {props.locationIds.map((id) => (
                <option key={id} value={id}>{id}</option>
              ))}
            </select>
          ) : (
            <input
              placeholder="id"
              value={String(row.id ?? '')}
              onChange={(e) => update(i, { id: e.target.value })}
            />
          )}
          {row.kind === 'stockAtLeast' && (
            <input
              type="number"
              placeholder="amount"
              value={Number(row.amount ?? 0)}
              onChange={(e) => update(i, { amount: Number(e.target.value) })}
            />
          )}
          {row.kind === 'realmAtLeast' && (
            <input
              placeholder="realm"
              value={String(row.realm ?? '')}
              onChange={(e) => update(i, { realm: e.target.value })}
            />
          )}
          <button type="button" className="danger" onClick={() => props.onChange(rows.filter((_, j) => j !== i))}>
            删
          </button>
        </div>
      ))}
    </div>
  );
}

export function OutcomeList(props: {
  title: string;
  rows: Row[];
  onChange: (rows: Row[]) => void;
}) {
  const rows = props.rows || [];
  const update = (i: number, patch: Row) => {
    const next = rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r));
    props.onChange(next);
  };
  return (
    <div className="block">
      <div className="block-head">
        <strong>{props.title}</strong>
        <button type="button" onClick={() => props.onChange([...rows, { kind: 'setFlag', id: '' }])}>
          + 结果
        </button>
      </div>
      {rows.map((row, i) => (
        <div className="row-edit" key={i}>
          <select value={String(row.kind ?? '')} onChange={(e) => update(i, { kind: e.target.value })}>
            {OUTCOME_KINDS.map((k) => (
              <option key={k} value={k}>{k}</option>
            ))}
          </select>
          <input
            placeholder="id"
            value={String(row.id ?? '')}
            onChange={(e) => update(i, { id: e.target.value })}
          />
          {(row.kind === 'addStock' || row.kind === 'grantProgress' || row.kind === 'relationDelta') && (
            <input
              type="number"
              placeholder="amount"
              value={Number(row.amount ?? 0)}
              onChange={(e) => update(i, { amount: Number(e.target.value) })}
            />
          )}
          {row.kind === 'relationDelta' && (
            <>
              <input
                placeholder="fromDefinitionId"
                value={String(row.fromDefinitionId ?? '')}
                onChange={(e) => update(i, { fromDefinitionId: e.target.value })}
              />
              <input
                placeholder="toDefinitionId"
                value={String(row.toDefinitionId ?? '')}
                onChange={(e) => update(i, { toDefinitionId: e.target.value })}
              />
            </>
          )}
          <button type="button" className="danger" onClick={() => props.onChange(rows.filter((_, j) => j !== i))}>
            删
          </button>
        </div>
      ))}
    </div>
  );
}
