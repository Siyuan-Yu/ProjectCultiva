# ADR-0013：Mod 为正式长期目标，当前只做 Mod Ready

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（架构冻结增量）

## 背景

此前倾向“暂不承诺 Mod”。长期内容扩展与社区需要 Mod；过早做 Workshop／脚本 API 会拖垮垂直切片。

## 选项

**A. 永不做 Mod** — 省事；长期不利。  
**B. 现在就做完整 Mod 平台** — 过早。  
**C. 正式长期目标 + 分阶段；当前只冻结 Mod Ready 架构**  

## 决策

选 **C**。见 `36-content-package-and-mod-architecture.md` 阶段 A～E。

## 影响

官方内容也必须 ContentPackage 化，避免日后拆硬编码。
