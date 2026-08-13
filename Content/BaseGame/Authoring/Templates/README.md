# 第一章内容模板（Authoring Templates）

复制本目录 JSON 到 `Content/BaseGame/Data/<类型子目录>/`（见 `Data/README.md`），替换 `REPLACE_*` 占位符后再纳入加载。  
**本目录不会被 ContentPackageLoader 扫描**（仅 `Data/**/*.json`）。

| 模板 | 用途 |
|---|---|
| `ch01_chapter.template.json` | 章节＋Day Beat＋Quest／Event 链＋Story Flag |
| `ch01_quest_chain.template.json` | 任务链两环样例 |
| `ch01_event_chain.template.json` | 事件链样例（选项写 Flag） |
| `ch01_story_flags.template.json` | Flag 清单（文档用，非 runtime type） |

命名规范见 `docs/40-process/84-chapter-content-naming-standards.md`。  
可运行壳：`base:chapter_ch01_shell`＋`base:scenario_chapter1_harness`。
