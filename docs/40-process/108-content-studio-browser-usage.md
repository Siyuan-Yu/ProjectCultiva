# 108 · Content Studio · 包总览与校验台用法

> 状态：**可用（Studio v0.1）**｜日期：2026-08-10  
> 所属应用：`ExternalTools/content-authoring`（XianXia Content Studio）  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 这个编辑器干什么

浏览 `Content/BaseGame` 包里全部 `definitions`，按类型过滤，并对整包跑字段／引用校验。本身不发明新文件格式；改数据请进区域／任务／事件编辑器。

## 怎么打开

1. `cd ExternalTools/content-authoring` → `npm run dev`（或运行打包好的 exe）
2. 顶栏确认包路径为 `…/Content/BaseGame`；不对就点「打开包…」
3. 左侧点 **包总览与校验**

## 日常操作

1. 左侧点类型（`quest`／`contentEvent`／`worldRegion`…），中间表格列出该类型全部条目。
2. 行内「打开任务／事件／地图」会跳到对应编辑器并选中该 id。
3. 点 **运行校验**：
   - **error**：未知字段、重复 id、地点引用不存在等（保存前应清掉）
   - **warn**：例如库存资源 id 未在包中注册
4. 顶栏 **保存全部改动** 只在其它编辑器改过数据后才可用；本页只读。

## 注意

- 必须用 Electron 壳；只开浏览器没有 `studioApi`，读不了盘。
- 校验规则对齐 `Content/BaseGame/Data/SCHEMA.md` 与字段白名单；与 Unity `DefinitionSchema` 同契约。
- 改完 JSON 后回 Unity **重新 Play** 才会进游戏。
