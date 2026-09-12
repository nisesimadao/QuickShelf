(() => {
  const send = data => window.chrome?.webview?.postMessage(data);
  const CIRC = 163.36;
  const isJapanese = (navigator.language || '').toLowerCase().startsWith('ja');
  const STRINGS = {
    ja: {
      processName: 'プロセス名', port: 'ポート', processService: 'プロセス / サービス', shelfTitle: 'クイックシェルフ',
      add: '追加', clear: 'クリア', loadingProcesses: 'プロセスを取得中...', loadingPorts: 'ポートを取得中...',
      noPorts: 'LISTENポートなし', addFiles: 'ファイルを追加', remove: '削除', openLocal: 'localhost:{port} を開く'
    },
    en: {
      processName: 'Process', port: 'Port', processService: 'Process / Service', shelfTitle: 'QUICK SHELF',
      add: 'Add', clear: 'Clear', loadingProcesses: 'Loading processes...', loadingPorts: 'Loading ports...',
      noPorts: 'No listening ports', addFiles: 'Add files', remove: 'Remove', openLocal: 'Open localhost:{port}'
    }
  };
  const tr = (key, vars = {}) => {
    let value = STRINGS[isJapanese ? 'ja' : 'en'][key] || key;
    for (const [name, replacement] of Object.entries(vars)) value = value.replace(`{${name}}`, String(replacement));
    return value;
  };
  const esc = value => String(value ?? '').replace(/[&<>"']/g, ch => ({
    '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'
  }[ch]));
  const clamp = n => Math.max(0, Math.min(100, Number(n) || 0));

  document.documentElement.lang = isJapanese ? 'ja' : 'en';

  function applyStaticLanguage() {
    const set = (id, key) => { const el = document.getElementById(id); if (el) el.textContent = tr(key); };
    set('process-name-header', 'processName');
    set('port-header', 'port');
    set('port-process-header', 'processService');
    set('shelf-title', 'shelfTitle');
    set('shelf-add', 'add');
    set('shelf-clear', 'clear');
  }

  function setGauge(arcId, valueId, percent) {
    const p = clamp(percent);
    const arc = document.getElementById(arcId);
    const value = document.getElementById(valueId);
    if (arc) arc.setAttribute('stroke-dashoffset', String(CIRC * (1 - p / 100)));
    if (value) value.textContent = `${Math.round(p)}%`;
  }

  function renderProcesses(snapshot) {
    const list = document.getElementById('process-list');
    if (!list) return;

    const previousScrollTop = list.scrollTop;
    const totalMemory = Math.max(1, Number(snapshot.memoryTotalBytes || 0));
    const processes = Array.isArray(snapshot.processes) ? snapshot.processes : [];

    list.innerHTML = processes.map(p => {
      const cpu = clamp(p.cpuPercent).toFixed(0);
      const ram = clamp((Number(p.memoryBytes || 0) / totalMemory) * 100).toFixed(0);
      const cpuClass = Number(cpu) >= 50 ? 'text-[#ff0033] font-semibold' : 'text-zinc-400 group-hover:text-zinc-200';
      return `<div class="group relative flex items-center justify-between px-3 py-1 rounded-full hover:bg-white/[0.04] transition-colors duration-200 cursor-pointer overflow-hidden" title="PID ${Number(p.pid)}">
        <div class="row-main-content flex items-center flex-1 min-w-0">
          <div class="flex-1 min-w-0 pr-1 fade-text-right text-zinc-200 group-hover:text-white tracking-tight font-medium"><span>${esc(p.name)}</span></div>
          <div class="w-11 text-right ${cpuClass} font-mono text-[10.5px] flex-shrink-0 pr-1">${cpu}%</div>
          <div class="w-11 text-right text-zinc-400 group-hover:text-zinc-200 font-mono text-[10.5px] flex-shrink-0 pr-1">${ram}%</div>
        </div>
        <div class="row-slider-btn">
          <button class="taskkill-process w-full bg-[#ff2233] hover:bg-[#e0002d] active:scale-95 text-white font-medium text-[9.5px] py-1 rounded-full shadow-sm flex items-center justify-center gap-1 leading-none shadow-red-950/40 whitespace-nowrap" data-pid="${Number(p.pid)}">
            <span>Taskkill</span><span class="material-symbols-outlined text-[10px] block">close</span>
          </button>
        </div>
      </div>`;
    }).join('');

    if (!processes.length) {
      list.innerHTML = `<div class="px-3 py-2 text-[10px] text-zinc-600 font-mono">${tr('loadingProcesses')}</div>`;
    }

    requestAnimationFrame(() => {
      const max = Math.max(0, list.scrollHeight - list.clientHeight);
      list.scrollTop = Math.min(previousScrollTop, max);
    });
  }

  function renderPorts(snapshot) {
    const list = document.getElementById('port-list');
    if (!list) return;
    const ports = Array.isArray(snapshot.ports) ? snapshot.ports.slice(0, 3) : [];

    list.innerHTML = ports.map(p => {
      const title = tr('openLocal', { port: Number(p.port) });
      return `<div class="group relative flex items-center justify-between px-3 py-1 rounded-full hover:bg-white/[0.04] transition-colors duration-200 cursor-pointer overflow-hidden">
        <div class="row-main-content flex items-center flex-1 min-w-0 gap-2">
          <div class="w-14 flex items-center flex-shrink-0"><span class="open-port font-mono text-zinc-200 group-hover:text-white text-[10px] font-semibold bg-white/[0.06] border border-white/[0.08] px-2 py-0.5 rounded-full tracking-tight" data-port="${Number(p.port)}" title="${esc(title)}">:${Number(p.port)}</span></div>
          <a class="open-port flex-1 text-zinc-300 group-hover:text-white font-sans text-[11px] truncate tracking-tight flex items-center gap-1 hover:underline min-w-0" href="#" data-port="${Number(p.port)}" title="${esc(title)}"><span class="truncate">${esc(p.label || p.processName)}</span><span class="material-symbols-outlined text-[10px] text-zinc-500 opacity-0 group-hover:opacity-100 transition-opacity block flex-shrink-0">open_in_new</span></a>
        </div>
        <div class="row-slider-btn"><button class="taskkill-process w-full bg-[#ff2233] hover:bg-[#e0002d] text-white transition-all text-[9.5px] py-1 rounded-full font-medium active:scale-95 flex items-center justify-center gap-1 shadow-sm leading-none shadow-red-950/30 whitespace-nowrap" data-pid="${Number(p.pid)}"><span>Taskkill</span><span class="material-symbols-outlined text-[10px] block">close</span></button></div>
      </div>`;
    }).join('');

    if (!ports.length) {
      list.innerHTML = `<div class="px-3 py-1 text-[10px] text-zinc-600 font-mono">${tr('noPorts')}</div>`;
    }
  }

  function formatBytes(bytes) {
    const n = Math.max(0, Number(bytes) || 0);
    if (n >= 1024 * 1024 * 1024) return `${(n / 1024 / 1024 / 1024).toFixed(1)} GB`;
    if (n >= 1024 * 1024) return `${(n / 1024 / 1024).toFixed(1)} MB`;
    if (n >= 1024) return `${Math.round(n / 1024)} KB`;
    return `${n} B`;
  }

  function iconForExtension(extension) {
    const ext = String(extension || '').toLowerCase();
    if (['.png','.jpg','.jpeg','.gif','.webp','.bmp','.svg'].includes(ext)) return ['image','#ff8c00'];
    if (['.mp4','.mov','.mkv','.webm','.avi'].includes(ext)) return ['movie','#ff0033'];
    if (['.mp3','.wav','.flac','.aac','.m4a'].includes(ext)) return ['audio_file','#00b7ff'];
    if (['.js','.ts','.tsx','.jsx','.cs','.java','.py','.cpp','.c','.h','.html','.css','.json','.xml','.yml','.yaml'].includes(ext)) return ['code','#8b7cff'];
    if (['.zip','.7z','.rar','.tar','.gz'].includes(ext)) return ['folder_zip','#ffd166'];
    if (['.pdf','.doc','.docx','.txt','.md'].includes(ext)) return ['description','#c8c8c8'];
    return ['draft','#9ca3af'];
  }

  function renderShelf(files) {
    const list = document.getElementById('staged-files');
    const count = document.getElementById('shelf-count');
    if (!list) return;
    const entries = Array.isArray(files) ? files : [];
    if (count) count.textContent = String(entries.length);

    if (!entries.length) {
      list.innerHTML = `<button id="shelf-empty-add" class="w-full rounded-full border border-dashed border-white/[0.10] bg-white/[0.02] hover:bg-white/[0.04] hover:border-white/[0.18] transition-all px-3 py-2 text-[10px] font-mono text-zinc-500 hover:text-zinc-300 flex items-center justify-center gap-1.5">
        <span class="material-symbols-outlined text-[13px]">add</span><span>${tr('addFiles')}</span>
      </button>`;
      return;
    }

    list.innerHTML = entries.map(file => {
      const [icon, color] = iconForExtension(file.extension);
      return `<div class="group relative rounded-full border border-white/[0.06] bg-white/[0.03] hover:bg-white/[0.06] hover:border-white/[0.14] transition-all px-3 py-1.5 flex items-center justify-between cursor-grab active:cursor-grabbing" data-path="${esc(file.path)}">
        <button class="shelf-open flex items-center gap-2 min-w-0 pr-2 text-left flex-1" data-path="${esc(file.path)}" title="${esc(file.path)}">
          <div class="w-6 h-6 rounded-full border flex items-center justify-center flex-shrink-0" style="color:${color};background:${color}18;border-color:${color}35"><span class="material-symbols-outlined text-[13px]">${icon}</span></div>
          <div class="flex flex-col truncate leading-tight"><span class="text-[11px] font-medium text-zinc-200 group-hover:text-white tracking-tight truncate">${esc(file.name)}</span><span class="text-[9px] font-mono text-zinc-400">${formatBytes(file.sizeBytes)} · ${esc((file.extension || 'file').replace('.', '').toUpperCase())}</span></div>
        </button>
        <div class="flex items-center gap-1 flex-shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
          <button class="shelf-remove p-1 text-zinc-400 hover:text-[#ff0033] rounded-full hover:bg-white/[0.08] transition-colors" data-path="${esc(file.path)}" title="${tr('remove')}"><span class="material-symbols-outlined text-[13px] block">close</span></button>
        </div>
      </div>`;
    }).join('');
  }

  function render(snapshot) {
    if (!snapshot) return;
    setGauge('cpuArc', 'cpuValue', snapshot.cpuPercent);
    setGauge('ramArc', 'ramValue', snapshot.memoryPercent);
    setGauge('gpuArc', 'gpuValue', snapshot.gpu?.usagePercent ?? 0);
    renderProcesses(snapshot);
    renderPorts(snapshot);
  }

  let shelfPointer = null;

  document.addEventListener('pointerdown', event => {
    const openFile = event.target.closest('.shelf-open');
    if (!openFile || event.button !== 0) return;
    shelfPointer = { id: event.pointerId, path: openFile.dataset.path || '', x: event.clientX, y: event.clientY };
  });

  document.addEventListener('pointermove', event => {
    if (!shelfPointer || shelfPointer.id !== event.pointerId) return;
    if ((event.buttons & 1) === 0) { shelfPointer = null; return; }
    const dx = event.clientX - shelfPointer.x;
    const dy = event.clientY - shelfPointer.y;
    if ((dx * dx + dy * dy) < 36) return;
    const path = shelfPointer.path;
    shelfPointer = null;
    if (path) send({type:'beginShelfFileDrag', path});
  });

  document.addEventListener('pointerup', event => {
    if (!shelfPointer || shelfPointer.id !== event.pointerId) return;
    const path = shelfPointer.path;
    shelfPointer = null;
    if (path) send({type:'openShelfFile', path});
  });

  document.addEventListener('pointercancel', () => { shelfPointer = null; });

  document.addEventListener('click', event => {
    const kill = event.target.closest('.taskkill-process');
    if (kill) {
      event.preventDefault(); event.stopPropagation();
      const pid = Number(kill.dataset.pid);
      if (Number.isInteger(pid) && pid > 4) send({type:'killProcess', pid});
      return;
    }

    const openPort = event.target.closest('.open-port');
    if (openPort) {
      event.preventDefault();
      const port = Number(openPort.dataset.port);
      if (Number.isInteger(port) && port > 0) send({type:'openPort', port});
      return;
    }

    if (event.target.closest('#shelf-add') || event.target.closest('#shelf-empty-add')) { send({type:'pickFiles'}); return; }
    if (event.target.closest('#shelf-clear')) { send({type:'clearShelfFiles'}); return; }

    const remove = event.target.closest('.shelf-remove');
    if (remove) { event.stopPropagation(); send({type:'removeShelfFile', path: remove.dataset.path || ''}); return; }

    if (event.target.closest('.shelf-open')) event.preventDefault();
  });

  window.chrome?.webview?.addEventListener('message', event => {
    if (event.data?.type === 'snapshot') render(event.data.value);
    if (event.data?.type === 'shelfFiles') renderShelf(event.data.value);
  });

  applyStaticLanguage();
  const processList = document.getElementById('process-list');
  if (processList) processList.innerHTML = `<div class="px-3 py-2 text-[10px] text-zinc-600 font-mono">${tr('loadingProcesses')}</div>`;
  const portList = document.getElementById('port-list');
  if (portList) portList.innerHTML = `<div class="px-3 py-1 text-[10px] text-zinc-600 font-mono">${tr('loadingPorts')}</div>`;
  renderShelf([]);
})();

