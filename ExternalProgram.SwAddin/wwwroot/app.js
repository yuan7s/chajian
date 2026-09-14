'use strict';

// ─────────── 按钮定义（与后端命令对应） ───────────
const BUTTONS = [
  { id: 'OpenFolder',              text: '打开目录',   command: 'open-file-location',       groups: ['part', 'drawing', 'assembly'] },
  { id: 'PartCoding',              text: '图号编码',   command: 'sync-coding-props',        groups: ['part'] },
  { id: 'BlankSize',               text: '下料尺寸',   command: 'write-blank-size',         groups: ['part', 'assembly'] },
  { id: 'SaveDwg',                 text: '另存 DWG',   command: 'save-dwg',                 groups: ['drawing'] },
  { id: 'SavePdf',                 text: '另存 PDF',   command: 'save-pdf',                 groups: ['drawing'] },
  { id: 'RotateView',              text: '旋转视图',   command: 'rotate-drawing-view',      groups: ['drawing'] },
  { id: 'IsoView',                 text: 'ISO',        command: 'set-iso-standard',         groups: ['drawing'] },
  { id: 'ReplaceDrawingSettings',  text: '标准格式',   command: 'replace-drawing-settings', groups: ['drawing'] },
  { id: 'ReferencePlaneMate',      text: '基准面配合', command: 'mate-reference-planes',    groups: ['assembly'] },
  { id: 'AssemblyCleanup',         text: '编码整理',   command: 'coding-cleanup',           groups: ['assembly'] },
  { id: 'AssemblySort',            text: '排序',       command: 'sort-components',          groups: ['assembly'] },
  { id: 'TreeSettings',            text: '树设置',     command: 'hide-config-names',        groups: ['assembly'] },
  { id: 'Rename',                  text: '重命名',     command: 'rename-target',            groups: ['assembly'] },
  { id: 'RunSwpMacro',             text: '运行宏',     command: 'run-swp-macro',            groups: ['assembly'] },
  { id: 'DeleteErrorMates',        text: '删错配合',   command: 'delete-error-mates',       groups: ['assembly'] },
  { id: 'DeleteCustomProps',       text: '删自定义',   command: 'delete-custom-props',      groups: ['assembly'] },
  { id: 'DeleteConfigProps',       text: '删配置',     command: 'delete-config-props',      groups: ['assembly'] },
];

const CONFIRM_COMMANDS = {
  'delete-custom-props': '将删除当前文档及其所有子件的自定义属性，确定继续？',
  'delete-config-props': '将删除当前文档及其所有子件的配置属性，确定继续？',
  'delete-error-mates':  '将删除当前装配体中的错误配合，确定继续？',
};

const el = (id) => document.getElementById(id);

// ─────────── 错误码 → 中文提示（对应后端 SwAddinClient.FormatCommandError） ───────────
function formatError(code, args) {
  args = args || {};
  switch (code) {
    case 'active_document_required': return '没有活动文档';
    case 'unknown_command': return '未知命令: ' + (args.command || '');
    case 'argument_required': return args.name ? args.name + ' 不能为空' : '缺少必要参数';
    case 'file_not_found': return args.path ? '文件不存在: ' + args.path : '文件不存在';
    case 'unsupported_file_type': return '不支持的文件类型';
    case 'unsupported_solidworks_file_type': return '不支持的 SolidWorks 文件类型: ' + (args.path || '');
    case 'drawing_required': return '请在工程图环境下使用';
    case 'drawing_must_be_saved': return '当前工程图还没有保存，无法另存 ' + (args.format || '') + '。';
    case 'drawing_view_required': return '请选择一个视图';
    case 'drawing_doc_unavailable': return '无法获取当前工程图对象';
    case 'drawing_template_required': return '请选择工程图模板，或勾选使用 SW 默认模板。';
    case 'drawing_template_unavailable': return '无法获取 SolidWorks 默认工程图模板，请在工程图窗口中选择 .drwdot 模板。';
    case 'drawing_source_must_be_saved': return '当前模型尚未保存，无法生成关联工程图。';
    case 'drawing_create_failed': return args.path ? '创建工程图失败: ' + args.path : '创建工程图失败';
    case 'assembly_required': return '请在装配体环境下使用';
    case 'assembly_doc_unavailable': return '无法获取装配体对象';
    case 'assembly_component_selection_required': return '请在装配体中选中一个组件';
    case 'component_selection_required': return '请先选中一个或多个组件';
    case 'component_not_found': return '未找到组件: ' + (args.name || '');
    case 'component_select_failed': return '组件选择失败: ' + (args.name || '');
    case 'document_extension_unavailable': return '无法获取当前文档扩展对象';
    case 'assembly_extension_unavailable': return '无法获取装配体扩展对象';
    case 'solidworks_operation_failed': return args.operation ? 'SolidWorks 操作失败: ' + args.operation : 'SolidWorks 操作失败';
    case 'reference_plane_mate_failed': return args.error
      ? '基准面配合失败: ' + args.error
      : '未能添加基准面配合，请确认组件和装配体基准面名称匹配';
    case 'macro_file_required': return '请选择 .swp 宏文件';
    case 'unsupported_macro_file': return '只支持 .swp 宏文件';
    case 'macro_run_failed': return '宏执行失败，错误码: ' + (args.error || '');
    case 'feature_manager_unavailable': return '无法获取 FeatureManager';
    case 'property_manager_unavailable': return '无法获取属性管理器';
    case 'configuration_property_manager_unavailable': return '无法获取当前配置属性管理器';
    case 'lightweight_component_write_unsupported': return '选中子件为轻化状态时仅支持读取文件属性，写入前请在 SolidWorks 中还原该子件。';
    case 'component_file_property_path_invalid': return '无法读取选中子件文件属性，文件路径无效: ' + (args.path || '');
    case 'document_manager_open_failed': return 'Document Manager 打开文件失败: ' + (args.error || '');
    case 'document_manager_unavailable': return '无法初始化 SolidWorks Document Manager，请检查内置许可证。';
    case 'part_required_for_bounding_box': return '请在零件环境下获取包围盒';
    case 'bounding_box_unavailable': return '无法获取包围盒';
    case 'solidworks_rename_failed': return 'SolidWorks 重命名失败，错误码: ' + (args.error || '');
    case 'replace_component_reselect_failed': return '无法重新选中待替换组件';
    case 'replace_component_failed': return 'SolidWorks 未能替换组件引用';
    case 'rename_component_select_failed': return '无法选中待重命名组件';
    case 'selected_component_must_be_saved_for_rename': return '选中组件尚未保存，无法重命名';
    case 'document_must_be_saved_for_rename': return '当前文档尚未保存，无法重命名';
    case 'new_file_name_required': return '请输入新文件名';
    case 'invalid_file_name': return '文件名包含非法字符';
    case 'target_directory_unavailable': return '无法确定目标文件夹';
    case 'unsupported_document_type': return '不支持的文档类型';
    case 'batch_no_models': return '没有可处理的模型文件';
    case 'target_file_exists': return '目标文件已存在: ' + (args.path || '');
    case 'save_new_file_failed': return swCodeMessage('保存新文件失败', args.errors, args.warnings);
    case 'open_new_file_failed': return swCodeMessage('打开新文件失败', args.errors, args.warnings);
    case 'save_renamed_component_failed': return swCodeMessage('保存重命名后的组件失败', args.errors, args.warnings);
    case 'save_assembly_failed': return swCodeMessage('保存当前装配体失败', args.errors, args.warnings);
    case 'save_properties_failed': return swCodeMessage('保存属性失败', args.errors, args.warnings);
    case 'assembly_configuration_unavailable': return '无法获取当前装配体配置';
    case 'assembly_root_component_unavailable': return '无法获取装配体根组件';
    case 'sort_reorder_failed': return '排序失败，SolidWorks 重排组件时出错，请检查装配体是否包含轻化或只读组件。';
    case 'main_thread_timeout': return 'SolidWorks 主线程未及时响应，请等待当前操作完成后再试。';
    case 'internal_error': return '插件内部错误，请查看插件日志。';
    default: return code ? '插件返回失败: ' + code : '未知错误';
  }
}

function swCodeMessage(prefix, errors, warnings) {
  const msg = warnings
    ? prefix + '，错误码: ' + errors + '，警告码: ' + warnings
    : prefix + '，错误码: ' + errors;
  const hint = swSaveErrorHint(errors);
  return hint ? msg + '（' + hint + '）' : msg;
}

function swSaveErrorHint(errors) {
  const n = parseInt(errors, 10);
  if (isNaN(n)) return '';
  return (n & 8192) !== 0 ? '需要先保存引用文档' : '';
}

// ─────────── 命令调用 ───────────
async function sendCommand(command, args, timeoutMs) {
  const ctrl = new AbortController();
  const timer = timeoutMs ? setTimeout(() => ctrl.abort(), timeoutMs) : null;
  let res;
  try {
    res = await fetch('/command', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Command: command, Args: args || {} }),
      signal: ctrl.signal,
    });
  } catch (err) {
    if (err && err.name === 'AbortError') throw new Error('操作超时');
    throw new Error('通信失败: ' + (err && err.message ? err.message : err));
  } finally {
    if (timer) clearTimeout(timer);
  }

  let body = null;
  try { body = await res.json(); } catch (e) { /* ignore */ }

  if (res.status === 403) throw new Error('请求被拒绝（跨站拦截）');
  if (res.status === 409) { const e = new Error('SolidWorks 正忙，请稍后重试'); e.code = 'command_busy'; throw e; }
  if (!res.ok) throw new Error('通信失败（HTTP ' + res.status + '）');
  if (!body) throw new Error('通信失败：无效响应');
  if (body.ok) return body.data;
  const err = new Error(formatError(body.errorCode, body.errorArgs));
  err.code = body.errorCode;
  throw err;
}

// ─────────── 文档类型 → 分组 ───────────
function groupForType(type) {
  const t = String(type || '').toUpperCase();
  if (t === '1' || t === 'PART') return 'part';
  if (t === '3' || t === 'DRAWING') return 'drawing';
  if (t === '2' || t === 'ASSEMBLY') return 'assembly';
  return '';
}

// ─────────── 渲染 ───────────
let currentGroup = '';

function getDefaultLayout() {
  const layout = { part: [], drawing: [], assembly: [] };
  for (const b of BUTTONS) {
    for (const g of b.groups) {
      if (layout[g]) layout[g].push(b.id);
    }
  }
  return layout;
}

function getButtonLayout() {
  const raw = localStorage.getItem('btnLayout');
  if (raw) {
    try {
      const parsed = JSON.parse(raw);
      if (parsed && Array.isArray(parsed.part) && Array.isArray(parsed.drawing) && Array.isArray(parsed.assembly)) {
        return parsed;
      }
    } catch (e) { /* fall through to default */ }
  }
  return getDefaultLayout();
}

function renderButtons(group) {
  currentGroup = group;
  const toolbar = el('toolbar');
  toolbar.innerHTML = '';
  const layout = getButtonLayout();
  const visible = group ? BUTTONS.filter((b) => (layout[group] || []).includes(b.id)) : [];
  if (visible.length === 0) {
    toolbar.innerHTML = '<div class="empty">当前文档类型没有可用操作</div>';
    return;
  }
  for (const b of visible) {
    const btn = document.createElement('button');
    btn.className = 'tool-btn';
    btn.textContent = b.text;
    btn.addEventListener('click', () => handleButton(b));
    toolbar.appendChild(btn);
  }
}

// ─────────── 按钮点击 ───────────
async function handleButton(b) {
  if (b.command === 'coding-cleanup') { openCodingCleanupModal(); return; }
  if (b.command === 'rename-target') { openRenameModal(); return; }
  if (b.command === 'sort-components') { openSortModal(); return; }
  const confirmMsg = CONFIRM_COMMANDS[b.command];
  if (confirmMsg && !window.confirm(confirmMsg)) return;

  try {
    if (b.command === 'open-file-location') {
      const data = await sendCommand('open-file-location');
      if (data && data.opened) showToast('已打开文件所在目录', 'ok');
      else showToast('当前文件尚未保存，无法打开目录', 'warn');
      return;
    }
    if (b.command === 'replace-drawing-settings') {
      const standardPath = localStorage.getItem('standardPath') || '';
      const sheetFormatPath = localStorage.getItem('sheetFormatPath') || '';
      if (!standardPath || !sheetFormatPath) {
        showToast('请先点右上角「设置」填写绘图标准和图纸格式文件路径', 'warn');
        return;
      }
      await sendCommand('replace-drawing-settings', { standardPath, sheetFormatPath });
      showToast('绘图标准和图纸格式替换完成', 'ok');
      return;
    }
    if (b.command === 'run-swp-macro') {
      const pick = await sendCommand('pick-swp-macro');
      if (!pick || pick.cancelled) return;
      await sendCommand('run-swp-macro', { path: pick.path });
      showToast('宏执行完成', 'ok');
      return;
    }
    if (b.command === 'write-blank-size') {
      const data = await sendCommand('write-blank-size');
      const size = data && data.blankSize;
      showToast(size ? '下料尺寸已写入: ' + size : '下料尺寸已写入', 'ok');
      return;
    }
    await sendCommand(b.command);
    showToast('完成', 'ok');
  } catch (err) {
    showToast(err && err.message ? err.message : String(err), 'error');
  }
}

function buildSortArgs() {
  return {
    assemblyFirst: localStorage.getItem('sortAssemblyFirst') === 'true',
    suppressedLast: localStorage.getItem('sortSuppressedLast') === 'true',
    sortFolders: localStorage.getItem('sortFolders') === 'true',
    recursiveSubAssemblies: localStorage.getItem('sortRecursiveSubAssemblies') === 'true',
    nameSource: localStorage.getItem('sortNameSource') || 'ComponentName',
    descending: localStorage.getItem('sortDescending') === 'true',
  };
}

// ─────────── 状态与事件 ───────────
async function refreshStatus() {
  try {
    const data = await sendCommand('active-document');
    setConnected(true);
    const title = (data && data.title) || '';
    const path = (data && data.path) || '';
    const type = data && data.type;
    el('doc-name').textContent = path ? path.split(/[\\/]/).pop().replace(/\.[^.]+$/, '') : (title || '无文档');
    renderButtons(groupForType(type));
  } catch (err) {
    if (err && err.code === 'command_busy') {
      return; // SolidWorks 正忙（如文件选择框打开中），保持当前状态不变
    }
    if (err && err.code === 'active_document_required') {
      setConnected(true);
      el('doc-name').textContent = '无文档';
      renderButtons('');
    } else {
      setConnected(false);
      el('doc-name').textContent = '未连接';
      renderButtons('');
    }
  }
}

function setConnected(on) {
  const dot = el('status-dot');
  const text = el('status-text');
  dot.className = 'status-dot ' + (on ? 'on' : 'off');
  text.textContent = on ? '已连接' : '未连接';
}

function connectEvents() {
  try {
    const ws = new WebSocket('ws://' + location.host + '/events');
    ws.onmessage = (ev) => {
      let msg;
      try { msg = JSON.parse(ev.data); } catch (e) { return; }
      if (msg.type === 'doc-changed') { refreshStatus(); }
      else if (msg.type === 'selection-changed') {
        const type = msg.data && msg.data.type;
        renderButtons(groupForType(type));
      } else if (msg.type === 'sw-shutdown') { setConnected(false); }
    };
    ws.onclose = () => setTimeout(connectEvents, 3000);
    ws.onerror = () => { /* onclose 会触发重连 */ };
  } catch (e) {
    setTimeout(connectEvents, 3000);
  }
}

// ─────────── toast ───────────
let toastTimer = null;
function showToast(message, kind) {
  const t = el('toast');
  t.textContent = message;
  t.className = 'toast ' + (kind || '');
  t.hidden = false;
  if (toastTimer) clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { t.hidden = true; }, 2500);
}

// ─────────── 设置面板 ───────────
function loadSettingsForm() {
  el('cfg-standard-path').value = localStorage.getItem('standardPath') || '';
  el('cfg-sheet-format-path').value = localStorage.getItem('sheetFormatPath') || '';
}

function saveSettingsForm() {
  localStorage.setItem('standardPath', el('cfg-standard-path').value.trim());
  localStorage.setItem('sheetFormatPath', el('cfg-sheet-format-path').value.trim());
  saveButtonLayout();
  showToast('设置已保存', 'ok');
}

function buildButtonLayoutUI() {
  const container = el('layout-groups');
  container.innerHTML = '';
  const layout = getButtonLayout();
  const groups = [['part', '零件'], ['drawing', '工程图'], ['assembly', '装配体']];
  for (const [group, label] of groups) {
    const details = document.createElement('details');
    details.className = 'layout-group';
    const summary = document.createElement('summary');
    summary.textContent = label;
    details.appendChild(summary);
    const ids = layout[group] || [];
    for (const b of BUTTONS) {
      const lbl = document.createElement('label');
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.dataset.group = group;
      cb.dataset.id = b.id;
      cb.checked = ids.includes(b.id);
      lbl.appendChild(cb);
      lbl.appendChild(document.createTextNode(' ' + b.text));
      details.appendChild(lbl);
    }
    container.appendChild(details);
  }
}

function saveButtonLayout() {
  const layout = { part: [], drawing: [], assembly: [] };
  document.querySelectorAll('#layout-groups input[type="checkbox"]').forEach((cb) => {
    if (cb.checked) layout[cb.dataset.group].push(cb.dataset.id);
  });
  localStorage.setItem('btnLayout', JSON.stringify(layout));
  renderButtons(currentGroup);
}

function initSettingsPanel() {
  el('settings-toggle').addEventListener('click', () => {
    const panel = el('settings-panel');
    panel.hidden = !panel.hidden;
    if (!panel.hidden) { loadSettingsForm(); buildButtonLayoutUI(); }
  });
  el('settings-save').addEventListener('click', saveSettingsForm);
}

// ─────────── 通用辅助 ───────────
function lsBool(key, def) {
  const v = localStorage.getItem(key);
  return v === null ? def : v === 'true';
}

// ─────────── 模态框 ───────────
function openModal(id) { el(id).hidden = false; }
function closeModal(id) { el(id).hidden = true; }

function initModals() {
  document.querySelectorAll('[data-close]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const overlay = btn.closest('.modal-overlay');
      if (overlay) overlay.hidden = true;
    });
  });
  document.querySelectorAll('.modal-overlay').forEach((overlay) => {
    overlay.addEventListener('click', (e) => {
      if (e.target === overlay) overlay.hidden = true;
    });
  });

  el('cc-execute').addEventListener('click', executeCodingCleanup);
  el('sort-execute').addEventListener('click', executeSort);

  el('rn-new-name').addEventListener('input', scheduleRenameConflictCheck);
  el('rn-rename').addEventListener('click', () => doRename('rename-component'));
  el('rn-save-as').addEventListener('click', () => doRename('save-as-new'));
  el('rn-save-as-replace').addEventListener('click', () => doRename('save-as-replace'));
}

// ─────────── 编码整理 ───────────
function loadCcForm() {
  el('cc-name-filter').value = localStorage.getItem('ccNameFilter') || '';
  el('cc-process-asm').checked = lsBool('ccProcessAsm', true);
  el('cc-process-part').checked = lsBool('ccProcessPart', true);
  el('cc-exclude-virtual').checked = lsBool('ccExcludeVirtual', true);
  el('cc-exclude-standard').checked = lsBool('ccExcludeStandard', true);
  el('cc-exclude-purchased').checked = lsBool('ccExcludePurchased', true);
}

function saveCcForm() {
  localStorage.setItem('ccNameFilter', el('cc-name-filter').value);
  localStorage.setItem('ccProcessAsm', String(el('cc-process-asm').checked));
  localStorage.setItem('ccProcessPart', String(el('cc-process-part').checked));
  localStorage.setItem('ccExcludeVirtual', String(el('cc-exclude-virtual').checked));
  localStorage.setItem('ccExcludeStandard', String(el('cc-exclude-standard').checked));
  localStorage.setItem('ccExcludePurchased', String(el('cc-exclude-purchased').checked));
}

function openCodingCleanupModal() {
  loadCcForm();
  openModal('modal-coding-cleanup');
}

async function executeCodingCleanup() {
  saveCcForm();
  const args = {
    nameFilter: el('cc-name-filter').value,
    processAsm: el('cc-process-asm').checked,
    processPart: el('cc-process-part').checked,
    excludeVirtual: el('cc-exclude-virtual').checked,
    excludeStandard: el('cc-exclude-standard').checked,
    excludePurchased: el('cc-exclude-purchased').checked,
  };
  try {
    const data = await sendCommand('coding-cleanup', args, 600000);
    const n = data && data.processed != null ? data.processed : 0;
    showToast('编码整理完成，已更新 ' + n + ' 个组件。', 'ok');
    closeModal('modal-coding-cleanup');
  } catch (err) {
    showToast(err && err.message ? err.message : String(err), 'error');
  }
}

// ─────────── 装配体排序 ───────────
function loadSortForm() {
  el('sort-assembly-first').checked = lsBool('sortAssemblyFirst', false);
  el('sort-suppressed-last').checked = lsBool('sortSuppressedLast', false);
  el('sort-folders').checked = lsBool('sortFolders', false);
  el('sort-recursive').checked = lsBool('sortRecursiveSubAssemblies', false);
  el('sort-descending').checked = lsBool('sortDescending', false);
  el('sort-name-source').value = localStorage.getItem('sortNameSource') || 'ComponentName';
}

function saveSortForm() {
  localStorage.setItem('sortAssemblyFirst', String(el('sort-assembly-first').checked));
  localStorage.setItem('sortSuppressedLast', String(el('sort-suppressed-last').checked));
  localStorage.setItem('sortFolders', String(el('sort-folders').checked));
  localStorage.setItem('sortRecursiveSubAssemblies', String(el('sort-recursive').checked));
  localStorage.setItem('sortDescending', String(el('sort-descending').checked));
  localStorage.setItem('sortNameSource', el('sort-name-source').value);
}

function openSortModal() {
  loadSortForm();
  openModal('modal-sort');
}

async function executeSort() {
  saveSortForm();
  try {
    const data = await sendCommand('sort-components', buildSortArgs(), 600000);
    const n = data && data.foldersSorted != null ? data.foldersSorted : 0;
    showToast(n > 0 ? '装配体排序完成，已处理 ' + n + ' 个文件夹' : '装配体排序完成', 'ok');
    closeModal('modal-sort');
  } catch (err) {
    showToast(err && err.message ? err.message : String(err), 'error');
  }
}

// ─────────── 重命名 ───────────
let renameDebounce = null;
let renameConflictSeq = 0;

function loadRenameParams() {
  el('rn-property-target').value = localStorage.getItem('rnPropertyTarget') || 'configuration';
  el('rn-write-filename').checked = lsBool('rnWriteFileName', true);
  el('rn-write-material-code').checked = lsBool('rnWriteMaterialCode', true);
  el('rn-write-part-number').checked = lsBool('rnWritePartNumber', true);
  el('rn-write-design').checked = lsBool('rnWriteDesign', false);
  el('rn-write-version').checked = lsBool('rnWriteVersion', false);
  el('rn-design-text').value = localStorage.getItem('rnDesignText') || '';
  el('rn-version-text').value = localStorage.getItem('rnVersionText') || 'A';
}

function saveRenameParams() {
  localStorage.setItem('rnPropertyTarget', el('rn-property-target').value);
  localStorage.setItem('rnWriteFileName', String(el('rn-write-filename').checked));
  localStorage.setItem('rnWriteMaterialCode', String(el('rn-write-material-code').checked));
  localStorage.setItem('rnWritePartNumber', String(el('rn-write-part-number').checked));
  localStorage.setItem('rnWriteDesign', String(el('rn-write-design').checked));
  localStorage.setItem('rnWriteVersion', String(el('rn-write-version').checked));
  localStorage.setItem('rnDesignText', el('rn-design-text').value.trim());
  localStorage.setItem('rnVersionText', el('rn-version-text').value.trim());
}

async function openRenameModal() {
  loadRenameParams();
  el('rn-save-as-replace').style.display = 'none';
  openModal('modal-rename');
  el('rn-name-status').textContent = '';
  el('rn-name-status').className = 'rn-status';
  try {
    const info = await sendCommand('rename-target');
    const oldName = (info && info.baseName) || '';
    const ext = (info && info.extension) || '';
    const selected = !!(info && info.selectedComponent);
    el('rn-old-name').value = oldName;
    el('rn-ext').textContent = ext;
    el('rn-new-name').value = oldName;
    el('rn-save-as-replace').style.display = selected ? '' : 'none';
  } catch (err) {
    showToast(err && err.message ? err.message : String(err), 'error');
    closeModal('modal-rename');
  }
}

function scheduleRenameConflictCheck() {
  if (renameDebounce) clearTimeout(renameDebounce);
  renameDebounce = setTimeout(() => checkRenameConflict(), 500);
}

async function checkRenameConflict(newName) {
  const name = (typeof newName === 'string' ? newName : el('rn-new-name').value).trim();
  const status = el('rn-name-status');
  if (!name) {
    status.textContent = '';
    status.className = 'rn-status';
    return;
  }
  const seq = ++renameConflictSeq;
  try {
    const data = await sendCommand('check-name-conflict', { newName: name });
    if (seq !== renameConflictSeq) return;
    if (data && data.conflict) {
      const detail = data.existsOpen && data.existsFile
        ? '重名(打开+本地)'
        : data.existsOpen ? '重名(已打开)' : '重名(本地)';
      status.textContent = detail;
      status.className = 'rn-status conflict';
    } else {
      status.textContent = '可保存';
      status.className = 'rn-status ok';
    }
  } catch (err) {
    if (seq !== renameConflictSeq) return;
    status.textContent = '';
    status.className = 'rn-status';
  }
}

function buildRenameArgs(newName) {
  const target = el('rn-property-target').value;
  return {
    newName,
    propertyTarget: target,
    source: target,
    customProperties: target === 'custom',
    fileName: el('rn-write-filename').checked,
    materialCode: el('rn-write-material-code').checked,
    partNumber: el('rn-write-part-number').checked,
    design: el('rn-write-design').checked,
    version: el('rn-write-version').checked,
    designText: el('rn-design-text').value.trim(),
    versionText: el('rn-version-text').value.trim() || 'A',
    copyDrawing: true,
    renameProperties: [],
  };
}

async function doRename(action) {
  const newName = el('rn-new-name').value.trim();
  if (!newName) { showToast('请输入新文件名', 'warn'); return; }
  saveRenameParams();
  const labels = { 'rename-component': '重命名', 'save-as-new': '另存为', 'save-as-replace': '另存替换' };
  try {
    const data = await sendCommand(action, buildRenameArgs(newName), 600000);
    if (data && data.success) {
      showToast(labels[action] + '完成', 'ok');
      closeModal('modal-rename');
      refreshStatus();
    } else {
      showToast(labels[action] + '失败', 'error');
    }
  } catch (err) {
    showToast(labels[action] + '失败: ' + (err && err.message ? err.message : err), 'error');
  }
}

// ─────────── 属性面板 ───────────
const KEY_PROPERTIES = ['物料编码', '零件图号', '文件名称', '零件类型', '零件材质', '表面处理/热处理', '下料尺寸', '版本', '设计者', '出图者'];
let propPollTimer = null;

function initPropertyPanel() {
  el('property-toggle').addEventListener('click', () => setPropertyPanelVisible(el('property-panel').hidden));
  el('property-close').addEventListener('click', () => setPropertyPanelVisible(false));
  el('prop-source').addEventListener('change', savePropSettings);
  el('prop-scope').addEventListener('change', savePropSettings);
  loadPropSettings();
}

function setPropertyPanelVisible(show) {
  el('property-panel').hidden = !show;
  if (show) {
    refreshProperties();
    if (!propPollTimer) propPollTimer = setInterval(refreshProperties, 1200);
  } else if (propPollTimer) {
    clearInterval(propPollTimer);
    propPollTimer = null;
  }
}

function loadPropSettings() {
  el('prop-source').value = localStorage.getItem('propSource') || 'configuration';
  el('prop-scope').value = localStorage.getItem('propScope') || 'key';
}

function savePropSettings() {
  localStorage.setItem('propSource', el('prop-source').value);
  localStorage.setItem('propScope', el('prop-scope').value);
  refreshProperties();
}

async function refreshProperties() {
  try {
    const data = await sendCommand('read-properties', { source: el('prop-source').value });
    renderProperties(data);
  } catch (err) {
    if (err && err.code === 'command_busy') return;
    renderProperties(null);
  }
}

function renderProperties(data) {
  const list = el('prop-list');
  list.innerHTML = '';
  if (!data || !data.properties) {
    el('prop-title').textContent = '无文档';
    return;
  }
  el('prop-title').textContent = data.title || '-';
  const props = data.properties;
  const scope = el('prop-scope').value;
  let entries;
  if (scope === 'key') {
    entries = KEY_PROPERTIES.map((k) => [k, props[k] != null ? props[k] : '']);
  } else {
    entries = Object.entries(props);
  }
  for (const [name, value] of entries) {
    const row = document.createElement('div');
    row.className = 'prop-row';
    const nameEl = document.createElement('span');
    nameEl.className = 'prop-name';
    nameEl.textContent = name;
    const valEl = document.createElement('span');
    valEl.className = 'prop-value';
    valEl.textContent = value != null ? String(value) : '';
    row.appendChild(nameEl);
    row.appendChild(valEl);
    list.appendChild(row);
  }
}

// ─────────── 启动 ───────────
initSettingsPanel();
initPropertyPanel();
initModals();
renderButtons('');
refreshStatus();
connectEvents();
setInterval(refreshStatus, 5000);
