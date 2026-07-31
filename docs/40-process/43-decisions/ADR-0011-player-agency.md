# ADR-0011：PlayerAgency = FocusCharacter + 动态势力领导权

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（架构冻结增量）

## 背景

玩家既要直接控人，又要在有领导权时管宗门；不能做成无实体的上帝视角。

## 选项

**A. 玩家=全局上帝** — 与“从凡人成长”冲突。  
**B. 玩家永久绑死一个不可换焦点** — 过死。  
**C. PlayerAgency：始终有 FocusCharacter；势力权限随职位动态获得／失去**  

## 决策

选 **C**。模式：`Character`／`FactionLeadership`。失去领导权后保留人物控制，旧势力 AI 继续运转。

## 影响

见 `33`／`34`。禁止 `IsPlayerCharacter` 包办一切。
