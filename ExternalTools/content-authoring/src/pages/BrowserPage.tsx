import type { PackageState, ValidationIssue } from '../lib/types';
import { fileLabel, validatePackage } from '../lib/packageIo';
import { useMemo, useState } from 'react';

const TYPES = [
  'quest', 'contentEvent', 'worldRegion', 'chapter', 'openingScenario',
  'character', 'cultivation', 'opportunitySite', 'workArea', 'job', 'resource'
];

export function BrowserPage(props: {
  state: PackageState;
  onOpen: (page: 'region' | 'quest' | 'event', id?: string) => void;
}) {
  const [typeFilter, setTypeFilter] = useState('quest');
  const [issues, setIssues] = useState<ValidationIssue[] | null>(null);

  const list = useMemo(
    () => props.state.defs.filter((d) => d.type === typeFilter),
    [props.state, typeFilter]
  );

  return (
    <div className="page split">
      <aside className="side">
        <h3>类型</h3>
        {TYPES.map((t) => (
          <button
            key={t}
            type="button"
            className={typeFilter === t ? 'nav active' : 'nav'}
            onClick={() => setTypeFilter(t)}
          >
            {t} ({props.state.defs.filter((d) => d.type === t).length})
          </button>
        ))}
        <button
          type="button"
          className="primary"
          style={{ marginTop: 12 }}
          onClick={() => setIssues(validatePackage(props.state))}
        >
          运行校验
        </button>
      </aside>
      <section className="main">
        <h2>包总览 — {typeFilter}</h2>
        <table>
          <thead>
            <tr>
              <th>id</th>
              <th>name</th>
              <th>文件</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {list.map((d) => (
              <tr key={d.id}>
                <td><code>{d.id}</code></td>
                <td>{d.name}</td>
                <td>{fileLabel(d.filePath)}</td>
                <td>
                  {d.type === 'quest' && (
                    <button type="button" onClick={() => props.onOpen('quest', d.id)}>打开任务</button>
                  )}
                  {d.type === 'contentEvent' && (
                    <button type="button" onClick={() => props.onOpen('event', d.id)}>打开事件</button>
                  )}
                  {d.type === 'worldRegion' && (
                    <button type="button" onClick={() => props.onOpen('region', d.id)}>打开地图</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {issues && (
          <div className="issues">
            <h3>校验结果（{issues.length}）</h3>
            {issues.length === 0 && <p className="ok">通过：未发现错误／警告。</p>}
            <ul>
              {issues.map((iss, i) => (
                <li key={i} className={iss.level}>
                  [{iss.level}] {iss.message}
                  {iss.definitionId ? ` — ${iss.definitionId}` : ''}
                </li>
              ))}
            </ul>
          </div>
        )}
      </section>
    </div>
  );
}
