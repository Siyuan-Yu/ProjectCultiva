#!/usr/bin/env python3
"""为 feishu-map 中缺少应用写入权限的文档，批量添加 ChatCCC 机器人为可编辑作者。"""
import json
import subprocess
import sys
import time
import urllib.request
import websocket

CDP = "http://127.0.0.1:15166"
BOT = "ysy的ChatCCC改名版"
DOC_BASE = "https://my.feishu.cn/docx"


def load_pages():
    with urllib.request.urlopen(f"{CDP}/json/list", timeout=5) as response:
        return [t for t in json.load(response) if t.get("type") == "page"]


def pick_page(pages, url_part):
    matches = [p for p in pages if url_part in (p.get("url") or "")]
    if len(matches) != 1:
        names = [{"id": p.get("id"), "url": p.get("url"), "title": p.get("title")} for p in matches]
        raise RuntimeError(f"expected one page for {url_part}, got {len(matches)}: {names}")
    return matches[0]


def cdp_call(ws, msg_id, method, params=None):
    ws.send(json.dumps({"id": msg_id, "method": method, "params": params or {}}))
    while True:
        message = json.loads(ws.recv())
        if message.get("id") == msg_id:
            if "error" in message:
                raise RuntimeError(message["error"])
            return message.get("result", {})


def eval_js(ws, msg_id, expression):
    result = cdp_call(ws, msg_id, "Runtime.evaluate", {
        "expression": expression,
        "awaitPromise": True,
        "returnByValue": True,
    })
    value = result.get("result", {})
    if value.get("subtype") == "error":
        raise RuntimeError(value.get("description"))
    return value.get("value")


def navigate(page_id, url):
    with urllib.request.urlopen(f"{CDP}/json/list", timeout=5) as response:
        target = next(t for t in json.load(response) if t.get("id") == page_id)
    ws = websocket.create_connection(target["webSocketDebuggerUrl"], timeout=60, suppress_origin=True)
    try:
        cdp_call(ws, 1, "Page.navigate", {"url": url})
        time.sleep(4)
    finally:
        ws.close()


def grant_on_current_page(page_id):
    with urllib.request.urlopen(f"{CDP}/json/list", timeout=5) as response:
        target = next(t for t in json.load(response) if t.get("id") == page_id)
    ws = websocket.create_connection(target["webSocketDebuggerUrl"], timeout=60, suppress_origin=True)
    try:
        rect = eval_js(
            ws,
            1,
            """
(() => {
  const btn = [...document.querySelectorAll('button')]
    .find(b => (b.innerText || '').trim() === '分享' && b.offsetParent);
  if (!btn) return null;
  const r = btn.getBoundingClientRect();
  return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
})()
""",
        )
        if not rect:
            return {"ok": False, "reason": "no-share-button"}

        x, y = rect["x"], rect["y"]
        for i, typ in enumerate(["mouseMoved", "mousePressed", "mouseReleased"], start=2):
            cdp_call(ws, i, "Input.dispatchMouseEvent", {
                "type": typ,
                "x": x,
                "y": y,
                "button": "left",
                "clickCount": 0 if typ == "mouseMoved" else 1,
            })
        time.sleep(2)

        ready = eval_js(
            ws,
            10,
            """
(() => {
  const input = [...document.querySelectorAll('input')]
    .find(i => i.offsetParent && (i.placeholder || '').includes('添加作者'));
  if (!input) return { ok: false, reason: 'no-author-input' };
  input.focus();
  input.click();
  input.value = '';
  input.dispatchEvent(new Event('input', { bubbles: true }));
  return { ok: true };
})()
""",
        )
        if not ready.get("ok"):
            return {"ok": False, "reason": ready.get("reason", "author-input")}

        cdp_call(ws, 11, "Input.insertText", {"text": BOT})
        time.sleep(1.2)

        picked = eval_js(
            ws,
            12,
            f"""
(() => {{
  const needle = {json.dumps(BOT, ensure_ascii=False)};
  const hit = [...document.querySelectorAll('[role=\"option\"], li, div, span')]
    .find(el => (el.innerText || '').trim() === needle && el.offsetParent);
  if (!hit) return {{ ok: false }};
  hit.click();
  return {{ ok: true }};
}})()
""",
        )
        if not picked.get("ok"):
            return {"ok": False, "reason": "pick-bot"}

        time.sleep(0.8)
        eval_js(
            ws,
            13,
            """
(() => {
  const edit = [...document.querySelectorAll('button,span,div,[role=\"menuitem\"],li')]
    .find(el => (el.innerText || '').trim() === '可编辑');
  if (edit) edit.click();
  const confirm = [...document.querySelectorAll('button')]
    .find(b => ['确定', '完成', '邀请', '添加', '确认'].includes((b.innerText || '').trim()));
  if (confirm) confirm.click();
  return true;
})()
""",
        )
        time.sleep(1)
        return {"ok": True}
    finally:
        ws.close()


FAILED = [
    {"key": "recent-updates-2026-08-14", "docId": "HqczdHh2Zo5A7UxZdvVc4Ggunse"},
    {"key": "npc-dialogue-host-ux-2026-08-14", "docId": "M0q4dQsBdojfxixN0DTcXwlTnCh"},
    {"key": "housing-control-core-2026-08-15", "docId": "NjepdWBA2o8O6kxzLxycQjgTnUf"},
    {"key": "cultivation-breakthrough-host-2026-08-15", "docId": "ZNYIdDIFDoEmSgxhOwFcLm04nQb"},
    {"key": "quest-manual-api-2026-08-15", "docId": "XPE5dGfcYoI5iSxuL8zco64QnBc"},
    {"key": "jiang-lao-cave-manual-2026-08-15", "docId": "M4hBdXHm0oDcuoxRm3ccJ45HnXd"},
    {"key": "combat-arts-physique-2026-08-16", "docId": "W7iAdwYigo0PxlxJbaPcOKnjnXb"},
    {"key": "control-core-chase-spawn-zone-2026-08-16", "docId": "UWOvdUOlao8e3ExK2ywc84fUnnh"},
    {"key": "defeat-teleport-cave-spawn-gui-2026-08-16", "docId": "K2qod9vD2oqtCVxqCQecAvxxnzh"},
    {"key": "world-graph-editor-usage", "docId": "RpyMdwTJmo1sW6xtaMTc574mn9b"},
    {"key": "world-graph-host-travel-isolation-2026-08-16", "docId": "KbFRdzob3o4ndMxsmlbcRV9inng"},
    {"key": "local-place-editor-usage", "docId": "CANPds3DJoPnsixBGDRctgkvnTh"},
    {"key": "skill-mastery-study-ritual-2026-08-16", "docId": "Ra6odyPuao50ZsxxoyCc8W5EnTt"},
    {"key": "skill-mastery-config-absolute-tiers-2026-08-16", "docId": "FOJ3dtLeMo7jgUxUlm2c3tz2n9b"},
    {"key": "manual-art-editor-and-cleanup-2026-08-16", "docId": "NGmwd2xJNoZOiRxfmpwcGE6Unzs"},
    {"key": "spirit-veil-ranged-normal-attack-2026-08-16", "docId": "ISh4dF7JToMZhMxQVv8c0dWZnxf"},
    {"key": "world-object-inspect-and-tree-chop-2026-08-16", "docId": "RGZ7dJ4egoTRGTxMcLLcMZMBnFf"},
    {"key": "farm-field-zone-labor-2026-08-16", "docId": "KLvxdPDDTodhQPxXij8c680Ynif"},
    {"key": "skill-mastery-farm-veil-chop-rollup-2026-08-17", "docId": "WaaNdNf2poof5qx2deVcFfKrnwb"},
]


def main():
    failed = FAILED
    if not failed:
        print("没有待处理文档。")
        return 0

    pages = load_pages()
    doc_pages = [p for p in pages if "feishu.cn/docx" in (p.get("url") or "")]
    if not doc_pages:
        doc_pages = [p for p in pages if "feishu.cn" in (p.get("url") or "")]
    page = doc_pages[0]
    page_id = page["id"]
    print(f"使用标签页 {page_id} ({page.get('url')})")

    ok = 0
    errors = []
    for item in failed:
        url = f"{DOC_BASE}/{item['docId']}"
        print(f"处理 {item['key']} ... ", end="", flush=True)
        try:
            navigate(page_id, url)
            result = grant_on_current_page(page_id)
            if not result.get("ok"):
                errors.append((item["key"], result.get("reason")))
                print(f"FAIL ({result.get('reason')})")
                continue
            ok += 1
            print("OK")
        except Exception as exc:
            errors.append((item["key"], str(exc)))
            print(f"ERR ({exc})")
        time.sleep(0.5)

    print(f"\n完成：{ok}/{len(failed)} 篇已添加机器人协作者。")
    if errors:
        print("失败：")
        for key, reason in errors:
            print(f"  {key}: {reason}")
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
