# ADR-0058：开局无主旧营与玩家势力身份分离

日期：2026-10-25。状态：**Producer Design Confirmed / 实施与人工验收状态见 375**。

制作人确认：取消主角团对开局「行路旧营」的初始所有权，但保留玩家势力身份与日后通过占领取得城镇的能力。`base:faction_player` 仍作为角色、队伍、外交和占领后的势力身份存在；**开局拥有该身份不等于开局已有领地**。

新开局保留 `site:runtime:base:flag_player_origin` 的稳定 Site ID、城镇实体、唯一固定议政厅、等级和位置，不因旧 ID 中含 `flag` 而重建或删除。该 Site 的初始 `ownerFactionId` 为空，因此不产生主角团的初始实际管辖；它是可依既有固定核心接管流程取得的无主地点。不得赋予玩家初始 Claim、仓库权或免费升级。旧存档不静默迁移；正式内容版本变化须走既有不兼容版本提示与新开局路径。

本决定不改变 PlayerIdentity、ActiveControlledCharacter、FactionMembership 或 Continuous Surface 权威，也不把普通玩家势力系统删除。内容作者源与正式运行 Surface 的初始 Owner 必须一致。地图概念图上的该 Site 以中性色显示，不把它涂成已占领的玩家势力范围。
