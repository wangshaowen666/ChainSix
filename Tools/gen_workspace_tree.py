#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成工作区目录视图（单文件 HTML，数据内嵌）。

用法：python3 Tools/gen_workspace_tree.py
输出：文档/工作区目录.html
"""
import json
import os
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "文档", "工作区目录.html")

SKIP_DIRS = {
    "Library", "Temp", "obj", "bin", ".git", "Logs", "HybridCLRData",
    ".idea", ".codely-cli", "node_modules", "__pycache__", ".vs", "py_venv",
}
SKIP_FILES = {".DS_Store"}
SKIP_EXT = {".meta"}

stat = {"files": 0, "dirs": 0, "size": 0}


def walk(path, name):
    node = {"n": name, "t": "d", "c": []}
    try:
        entries = sorted(os.scandir(path), key=lambda e: (not e.is_dir(), e.name.lower()))
    except OSError:
        return node
    for e in entries:
        if e.is_dir(follow_symlinks=False):
            if e.name in SKIP_DIRS:
                continue
            child = walk(e.path, e.name)
            if child["c"]:
                stat["dirs"] += 1
                node["c"].append(child)
        else:
            if e.name in SKIP_FILES:
                continue
            ext = os.path.splitext(e.name)[1].lower()
            if ext in SKIP_EXT:
                continue
            try:
                size = e.stat(follow_symlinks=False).st_size
            except OSError:
                size = 0
            stat["files"] += 1
            stat["size"] += size
            node["c"].append({"n": e.name, "t": "f", "s": size})
    return node


def main():
    tree = walk(ROOT, "ChainSix")
    stamp = time.strftime("%Y-%m-%d %H:%M")
    payload = json.dumps(tree, ensure_ascii=False, separators=(",", ":"))

    html = TEMPLATE.replace("__DATA__", payload)
    html = html.replace("__STAMP__", stamp)
    html = html.replace("__FILES__", f"{stat['files']:,}")
    html = html.replace("__DIRS__", f"{stat['dirs']:,}")
    html = html.replace("__SIZE__", f"{stat['size'] / 1024 / 1024:.1f} MB")
    html = html.replace("__ROOT__", ROOT)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"written: {OUT}  ({len(html) / 1024:.1f} KB)")


TEMPLATE = r"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>工作区目录 · ChainSix</title>
<style>
  :root{
    --bg:#0d1014; --panel:#12161c; --line:#20262f;
    --fg:#d7dce4; --fg-dim:#8a93a0; --fg-faint:#5d6673;
    --accent:#5aa9ff; --folder:#e0b25c;
  }
  *{box-sizing:border-box}
  html,body{height:100%}
  body{
    margin:0; background:var(--bg); color:var(--fg);
    font:13px/1.5 -apple-system,"SF Pro Text","PingFang SC","Microsoft YaHei",sans-serif;
    -webkit-font-smoothing:antialiased;
  }
  .wrap{display:flex; flex-direction:column; height:100vh; min-width:280px}
  header{
    padding:10px 12px 9px; border-bottom:1px solid var(--line);
    background:linear-gradient(180deg,#141a21,#10141a); flex:none;
  }
  .title{display:flex; align-items:baseline; gap:8px}
  .title b{font-size:13.5px; font-weight:600; letter-spacing:.2px}
  .title span{font-size:11px; color:var(--fg-faint); font-variant-numeric:tabular-nums}
  .path{
    margin-top:2px; font-size:11px; color:var(--fg-faint);
    white-space:nowrap; overflow:hidden; text-overflow:ellipsis;
    font-family:ui-monospace,SFMono-Regular,Menlo,monospace;
  }
  .search{
    margin-top:9px; display:flex; align-items:center; gap:6px;
    background:#0b0e12; border:1px solid var(--line); border-radius:6px;
    padding:0 8px; height:28px; transition:border-color .15s;
  }
  .search:focus-within{border-color:#2f5b86}
  .search input{
    flex:1; min-width:0; border:0; outline:0; background:transparent;
    color:var(--fg); font-size:12px; font-family:inherit;
  }
  .search input::placeholder{color:var(--fg-faint)}
  .search .k{font-size:10px; color:var(--fg-faint); border:1px solid var(--line);
    border-radius:3px; padding:0 4px; line-height:15px}
  #tree{flex:1; overflow:auto; padding:6px 6px 24px}
  #tree::-webkit-scrollbar{width:9px}
  #tree::-webkit-scrollbar-thumb{background:#242b35; border-radius:5px}
  #tree::-webkit-scrollbar-thumb:hover{background:#313a47}
  .row{
    display:flex; align-items:center; gap:5px; height:22px; padding-right:8px;
    border-radius:4px; cursor:default; white-space:nowrap; position:relative;
  }
  .row:hover{background:#1a2028}
  .row.file{cursor:pointer}
  .row.file:hover .name{color:#fff}
  .chev{
    width:12px; flex:none; text-align:center; color:var(--fg-faint);
    font-size:9px; transition:transform .12s ease; user-select:none;
  }
  .chev.open{transform:rotate(90deg)}
  .chev.hide{visibility:hidden}
  .ic{width:14px; flex:none; text-align:center; font-size:10px; color:var(--folder);
    font-family:ui-monospace,Menlo,monospace; opacity:.95}
  .name{overflow:hidden; text-overflow:ellipsis; font-size:12.5px}
  .dir>.name{color:#c9d2de; font-weight:500}
  .file .name{color:#aeb8c5}
  .cnt{margin-left:auto; font-size:10.5px; color:var(--fg-faint); padding-left:8px;
    font-variant-numeric:tabular-nums; opacity:0; transition:opacity .1s}
  .row:hover .cnt{opacity:1}
  .kids{margin-left:11px; border-left:1px solid #1b212a; padding-left:4px}
  .kids.hide{display:none}
  .empty{padding:18px 12px; color:var(--fg-faint); font-size:12px}
  footer{
    flex:none; border-top:1px solid var(--line); padding:6px 12px;
    font-size:10.5px; color:var(--fg-faint); display:flex; gap:10px;
    justify-content:space-between; background:#10141a;
  }
  #toast{
    position:fixed; left:50%; bottom:34px; transform:translate(-50%,8px);
    background:#1d2530; color:#dfe6ef; border:1px solid #2c3644;
    padding:6px 12px; border-radius:6px; font-size:12px; opacity:0;
    pointer-events:none; transition:.18s; max-width:90%; overflow:hidden;
    text-overflow:ellipsis; white-space:nowrap;
  }
  #toast.on{opacity:1; transform:translate(-50%,0)}
</style>
</head>
<body>
<div class="wrap">
  <header>
    <div class="title"><b>工作区目录</b><span>__FILES__ 文件 · __DIRS__ 目录 · __SIZE__</span></div>
    <div class="path">__ROOT__</div>
    <div class="search">
      <input id="q" placeholder="过滤文件或目录…" autocomplete="off" spellcheck="false">
      <span class="k">/</span>
    </div>
  </header>
  <div id="tree"></div>
  <footer><span>生成于 __STAMP__</span><span>点击文件复制路径</span></footer>
</div>
<div id="toast"></div>

<script>
const DATA = __DATA__;

const ICON = {
  cs:'C#', js:'JS', ts:'TS', py:'PY', json:'{}', xlsx:'XL', xls:'XL', csv:'CS',
  md:'MD', txt:'TX', sh:'SH', bat:'BA', conf:'CF', proto:'PR', asset:'AS',
  unity:'SC', prefab:'PF', png:'IM', jpg:'IM', psd:'PS', tga:'IM', mat:'MT',
  dll:'DL', so:'SO', sln:'SL', csproj:'PJ', yml:'YM', yaml:'YM', xml:'XM'
};
const COLOR = {
  cs:'#8ec07c', js:'#e6c07b', ts:'#6fa8dc', py:'#7fb3d5', json:'#c8b06a',
  xlsx:'#79b877', csv:'#79b877', md:'#b9a6d8', sh:'#9aa7b8', proto:'#d99b6a',
  png:'#c58fd0', jpg:'#c58fd0', prefab:'#6fb1c9', unity:'#6fb1c9', dll:'#8a93a0'
};

const treeEl = document.getElementById('tree');
const qEl = document.getElementById('q');
const openSet = new Set(['ChainSix']);
DATA.c.forEach(c => { if (c.t === 'd') openSet.add('ChainSix/' + c.n); });

function ext(n){ const i = n.lastIndexOf('.'); return i > 0 ? n.slice(i+1).toLowerCase() : ''; }
function size(b){
  if (b < 1024) return b + ' B';
  if (b < 1024*1024) return (b/1024).toFixed(b < 10240 ? 1 : 0) + ' KB';
  return (b/1048576).toFixed(1) + ' MB';
}
function countFiles(node){
  if (node.t === 'f') return 1;
  let s = 0;
  for (const c of node.c) s += countFiles(c);
  return s;
}
function hit(node, q){
  if (!q) return true;
  if (node.n.toLowerCase().includes(q)) return true;
  if (node.t === 'd') return node.c.some(c => hit(c, q));
  return false;
}

function render(node, path, depth, q){
  const frag = document.createDocumentFragment();
  for (const child of node.c) {
    if (!hit(child, q)) continue;
    const p = path + '/' + child.n;
    const isDir = child.t === 'd';
    const row = document.createElement('div');
    row.className = 'row ' + (isDir ? 'dir' : 'file');
    row.style.paddingLeft = (depth * 11 + 2) + 'px';

    const chev = document.createElement('span');
    chev.className = 'chev' + (isDir ? (openSet.has(p) || q ? ' open' : '') : ' hide');
    chev.textContent = '\u25B6';
    row.appendChild(chev);

    const ic = document.createElement('span');
    ic.className = 'ic';
    if (isDir) { ic.textContent = '\u25C6'; ic.style.color = 'var(--folder)'; }
    else {
      const e = ext(child.n);
      ic.textContent = ICON[e] || '\u00B7';
      ic.style.color = COLOR[e] || '#6d7683';
    }
    row.appendChild(ic);

    const nm = document.createElement('span');
    nm.className = 'name';
    nm.textContent = child.n;
    row.appendChild(nm);

    const cnt = document.createElement('span');
    cnt.className = 'cnt';
    cnt.textContent = isDir ? countFiles(child) : size(child.s || 0);
    row.appendChild(cnt);

    frag.appendChild(row);

    if (isDir) {
      const kids = document.createElement('div');
      kids.className = 'kids' + (openSet.has(p) || q ? '' : ' hide');
      kids.appendChild(render(child, p, depth + 1, q));
      frag.appendChild(kids);
      row.addEventListener('click', () => {
        const on = openSet.has(p);
        if (on) openSet.delete(p); else openSet.add(p);
        chev.classList.toggle('open', !on);
        kids.classList.toggle('hide', on);
      });
    } else {
      row.addEventListener('click', () => {
        const full = p.replace(/^ChainSix/, '__ROOT__');
        navigator.clipboard.writeText(full).then(
          () => toast('已复制 ' + full),
          () => toast(full)
        );
      });
    }
  }
  return frag;
}

let toastTimer = null;
function toast(msg){
  const t = document.getElementById('toast');
  t.textContent = msg;
  t.classList.add('on');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove('on'), 1600);
}

function draw(){
  const q = qEl.value.trim().toLowerCase();
  treeEl.innerHTML = '';
  const out = render(DATA, 'ChainSix', 0, q);
  if (!out.childNodes.length) {
    const d = document.createElement('div');
    d.className = 'empty';
    d.textContent = '没有匹配的文件';
    treeEl.appendChild(d);
  } else {
    treeEl.appendChild(out);
  }
}

qEl.addEventListener('input', draw);
document.addEventListener('keydown', e => {
  if (e.key === '/' && document.activeElement !== qEl) { e.preventDefault(); qEl.focus(); }
  if (e.key === 'Escape') { qEl.value = ''; qEl.blur(); draw(); }
});
draw();
</script>
</body>
</html>
"""

if __name__ == "__main__":
    main()
