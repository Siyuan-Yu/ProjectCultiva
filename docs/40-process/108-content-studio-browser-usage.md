# 108 · 包总览与校验台用法（PackageBrowser）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-10  
> 工程：`ExternalTools/ContentAuthoring/PackageBrowser/`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 干什么

浏览 `Content/BaseGame` 全部 `definitions`，按类型过滤，整包校验。不发明新格式。

## 怎么打开

**方式 A — Visual Studio**

1. 打开 `ExternalTools/ContentAuthoring/ContentAuthoring.sln`  
2. 启动项目设为 `PackageBrowser` → F5（或 Release 生成）

**方式 B — 已发布 exe**

```powershell
cd ExternalTools\ContentAuthoring
.\publish.ps1
# 然后双击
publish\PackageBrowser\PackageBrowser.exe
```

## 日常操作

1. 启动后若自动找到仓库内 `Content/BaseGame` 会直接加载；否则点 **打开包…**
2. 左侧选类型，中间看 id／name／文件名
3. 点 **运行校验**，右侧看 error／warn
4. 改数据请用另外三个独立编辑器；本工具以只读＋校验为主

## 注意

- 校验对齐 `SCHEMA.md` 字段白名单  
- 改完 JSON 后 Unity **重新 Play**
