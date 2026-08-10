import { useCallback, useEffect, useMemo, useState } from 'react';
import type { PackageState } from './lib/types';
import { loadPackage, persistPackage, touchedPaths } from './lib/packageIo';
import { BrowserPage } from './pages/BrowserPage';
import { RegionPage } from './pages/RegionPage';
import { QuestPage } from './pages/QuestPage';
import { EventPage } from './pages/EventPage';

type PageId = 'browser' | 'region' | 'quest' | 'event';

const NAV: { id: PageId; label: string }[] = [
  { id: 'browser', label: '包总览与校验' },
  { id: 'region', label: '区域／地点' },
  { id: 'quest', label: '任务' },
  { id: 'event', label: '事件' }
];

export function App() {
  const [root, setRoot] = useState('');
  const [state, setState] = useState<PackageState | null>(null);
  const [page, setPage] = useState<PageId>('browser');
  const [selectedId, setSelectedId] = useState<string | undefined>();
  const [dirtyIds, setDirtyIds] = useState<Set<string>>(() => new Set());
  const [status, setStatus] = useState('');
  const [error, setError] = useState('');

  const dirty = dirtyIds.size > 0;

  const reload = useCallback(async (packageRoot: string) => {
    setError('');
    setStatus('正在加载…');
    try {
      const next = await loadPackage(packageRoot);
      setState(next);
      setRoot(packageRoot);
      setDirtyIds(new Set());
      setStatus(`已加载 ${next.defs.length} 条定义 · ${next.files.length} 个 JSON`);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setStatus('');
    }
  }, []);

  useEffect(() => {
    (async () => {
      if (!window.studioApi) {
        setError('请用 Electron 启动：在 ExternalTools/content-authoring 执行 npm run dev');
        return;
      }
      const def = await window.studioApi.defaultPackageRoot();
      if (def) await reload(def);
      else setStatus('未找到默认 Content/BaseGame，请点「打开包…」');
    })();
  }, [reload]);

  const onChange = useCallback((next: PackageState, dirtyId: string) => {
    setState(next);
    setDirtyIds((prev) => {
      const n = new Set(prev);
      n.add(dirtyId);
      return n;
    });
  }, []);

  const onSave = useCallback(async () => {
    if (!state || dirtyIds.size === 0) return;
    setStatus('保存中…');
    try {
      const paths = touchedPaths(state, dirtyIds);
      await persistPackage(state, paths);
      const fresh = await loadPackage(state.root);
      setState(fresh);
      setDirtyIds(new Set());
      setStatus(`已写入 ${paths.length} 个文件`);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }, [state, dirtyIds]);

  const openEditor = (p: PageId, id?: string) => {
    setPage(p);
    setSelectedId(id);
  };

  const title = useMemo(() => root || '（未打开包）', [root]);

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <strong>XianXia Content Studio</strong>
          <span className="path" title={title}>{title}</span>
          {dirty && <span className="dirty">未保存</span>}
        </div>
        <div className="actions">
          <button
            type="button"
            onClick={async () => {
              const picked = await window.studioApi?.openPackageDialog();
              if (picked) await reload(picked);
            }}
          >
            打开包…
          </button>
          <button type="button" className="primary" disabled={!dirty} onClick={onSave}>
            保存全部改动
          </button>
        </div>
      </header>

      <div className="shell">
        <nav className="sidebar">
          {NAV.map((n) => (
            <button
              key={n.id}
              type="button"
              className={page === n.id ? 'nav active' : 'nav'}
              onClick={() => {
                setPage(n.id);
                setSelectedId(undefined);
              }}
            >
              {n.label}
            </button>
          ))}
        </nav>

        <main className="workspace">
          {error && <div className="banner err">{error}</div>}
          {status && <div className="banner info">{status}</div>}
          {!state ? (
            <p className="empty">打开 Content/BaseGame 包目录后开始编辑。</p>
          ) : page === 'browser' ? (
            <BrowserPage state={state} onOpen={openEditor} />
          ) : page === 'region' ? (
            <RegionPage
              key={selectedId || 'region'}
              state={state}
              selectedId={selectedId}
              onChange={onChange}
              dirty={dirty}
              onSave={onSave}
            />
          ) : page === 'quest' ? (
            <QuestPage
              key={selectedId || 'quest'}
              state={state}
              selectedId={selectedId}
              onChange={onChange}
              dirty={dirty}
              onSave={onSave}
            />
          ) : (
            <EventPage
              key={selectedId || 'event'}
              state={state}
              selectedId={selectedId}
              onChange={onChange}
              dirty={dirty}
              onSave={onSave}
            />
          )}
        </main>
      </div>
    </div>
  );
}
