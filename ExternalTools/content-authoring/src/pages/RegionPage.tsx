import { useMemo, useState } from 'react';
import type { JsonDict, PackageState } from '../lib/types';
import { asArray, asStringArray, upsertDefinition } from '../lib/packageIo';

const LOCATION_KINDS = ['Settlement', 'Village', 'Wild', 'Opportunity', 'Road'];

export function RegionPage(props: {
  state: PackageState;
  selectedId?: string;
  onChange: (next: PackageState, dirtyId: string) => void;
  dirty: boolean;
  onSave: () => void;
}) {
  const regions = useMemo(
    () => props.state.defs.filter((d) => d.type === 'worldRegion'),
    [props.state]
  );
  const [rid, setRid] = useState(props.selectedId || regions[0]?.id || '');
  const region = regions.find((r) => r.id === rid) || regions[0];
  const locations = asArray(region?.raw.locations);

  const commit = (raw: JsonDict) => {
    if (!region) return;
    props.onChange(
      upsertDefinition(props.state, {
        ...region,
        raw,
        name: String(raw.name ?? region.name)
      }),
      region.id
    );
  };

  const patchRegion = (nextLocs: JsonDict[]) => {
    if (!region) return;
    commit({ ...region.raw, locations: nextLocs });
  };

  const updateLoc = (i: number, patch: JsonDict) => {
    patchRegion(locations.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  };

  return (
    <div className="page">
      <header className="toolbar">
        <label>
          区域
          <select value={region?.id || ''} onChange={(e) => setRid(e.target.value)}>
            {regions.map((r) => (
              <option key={r.id} value={r.id}>{r.id}</option>
            ))}
          </select>
        </label>
        <button type="button" className="primary" disabled={!props.dirty} onClick={props.onSave}>
          保存到磁盘
        </button>
      </header>

      {!region ? (
        <p>包中没有 worldRegion。</p>
      ) : (
        <>
          <div className="meta">
            <label>
              区域 id
              <input value={String(region.raw.id ?? '')} readOnly />
            </label>
            <label>
              名称
              <input
                value={String(region.raw.name ?? '')}
                onChange={(e) => commit({ ...region.raw, name: e.target.value })}
              />
            </label>
            <label>
              startLocationId
              <select
                value={String(region.raw.startLocationId ?? '')}
                onChange={(e) => commit({ ...region.raw, startLocationId: e.target.value })}
              >
                <option value="">（未设）</option>
                {locations.map((l) => (
                  <option key={String(l.id)} value={String(l.id)}>{String(l.id)}</option>
                ))}
              </select>
            </label>
          </div>

          <div className="block-head">
            <h3>地点（逻辑地图）</h3>
            <button
              type="button"
              onClick={() =>
                patchRegion([
                  ...locations,
                  {
                    id: `${String(region.id).replace(/region_/, 'loc_')}_new`,
                    name: '新地点',
                    kind: 'Wild',
                    tags: [],
                    allowedActivities: [],
                    adjacentIds: [],
                    presentationX: 0,
                    presentationZ: 0
                  }
                ])
              }
            >
              + 地点
            </button>
          </div>

          <p className="graph-hint">
            邻接字段是 <code>adjacentIds</code>（不是 linkedLocationIds）。表现坐标 presentationX／Z 供 Host 摆点，不是地砖图。
          </p>

          <table className="edit-table">
            <thead>
              <tr>
                <th>id</th>
                <th>name</th>
                <th>kind</th>
                <th>tags</th>
                <th>allowedActivities</th>
                <th>adjacentIds</th>
                <th>X</th>
                <th>Z</th>
                <th>探索资源</th>
                <th>NPC／机缘／任务</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {locations.map((loc, i) => (
                <tr key={i}>
                  <td>
                    <input value={String(loc.id ?? '')} onChange={(e) => updateLoc(i, { id: e.target.value })} />
                  </td>
                  <td>
                    <input value={String(loc.name ?? '')} onChange={(e) => updateLoc(i, { name: e.target.value })} />
                  </td>
                  <td>
                    <select value={String(loc.kind ?? 'Wild')} onChange={(e) => updateLoc(i, { kind: e.target.value })}>
                      {LOCATION_KINDS.map((k) => (
                        <option key={k} value={k}>{k}</option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <input
                      value={asStringArray(loc.tags).join(', ')}
                      onChange={(e) =>
                        updateLoc(i, {
                          tags: e.target.value.split(/[,，\s]+/).map((s) => s.trim()).filter(Boolean)
                        })
                      }
                    />
                  </td>
                  <td>
                    <input
                      value={asStringArray(loc.allowedActivities).join(', ')}
                      onChange={(e) =>
                        updateLoc(i, {
                          allowedActivities: e.target.value.split(/[,，\s]+/).map((s) => s.trim()).filter(Boolean)
                        })
                      }
                    />
                  </td>
                  <td>
                    <input
                      value={asStringArray(loc.adjacentIds).join(', ')}
                      onChange={(e) =>
                        updateLoc(i, {
                          adjacentIds: e.target.value.split(/[,，\s]+/).map((s) => s.trim()).filter(Boolean)
                        })
                      }
                    />
                  </td>
                  <td>
                    <input
                      type="number"
                      value={Number(loc.presentationX ?? 0)}
                      onChange={(e) => updateLoc(i, { presentationX: Number(e.target.value) })}
                    />
                  </td>
                  <td>
                    <input
                      type="number"
                      value={Number(loc.presentationZ ?? 0)}
                      onChange={(e) => updateLoc(i, { presentationZ: Number(e.target.value) })}
                    />
                  </td>
                  <td>
                    <input
                      placeholder="resourceOnExploreId"
                      value={String(loc.resourceOnExploreId ?? '')}
                      onChange={(e) => updateLoc(i, { resourceOnExploreId: e.target.value || undefined })}
                    />
                    <input
                      type="number"
                      placeholder="amount"
                      value={Number(loc.resourceOnExploreAmount ?? 0)}
                      onChange={(e) => updateLoc(i, { resourceOnExploreAmount: Number(e.target.value) })}
                    />
                  </td>
                  <td>
                    <input
                      placeholder="residentNpcDefinitionId"
                      value={String(loc.residentNpcDefinitionId ?? '')}
                      onChange={(e) => updateLoc(i, { residentNpcDefinitionId: e.target.value || undefined })}
                    />
                    <input
                      placeholder="opportunitySiteId"
                      value={String(loc.opportunitySiteId ?? '')}
                      onChange={(e) => updateLoc(i, { opportunitySiteId: e.target.value || undefined })}
                    />
                    <input
                      placeholder="questOfferIds"
                      value={asStringArray(loc.questOfferIds).join(', ')}
                      onChange={(e) =>
                        updateLoc(i, {
                          questOfferIds: e.target.value.split(/[,，\s]+/).map((s) => s.trim()).filter(Boolean)
                        })
                      }
                    />
                  </td>
                  <td>
                    <button
                      type="button"
                      className="danger"
                      onClick={() => patchRegion(locations.filter((_, j) => j !== i))}
                    >
                      删
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <h3>连通预览</h3>
          <ul className="links">
            {locations.map((loc) => (
              <li key={String(loc.id)}>
                <code>{String(loc.id)}</code>
                {' → '}
                {asStringArray(loc.adjacentIds).join(', ') || '（无出口）'}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
