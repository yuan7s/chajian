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

const PENDING_FEATURES = {
  'coding-cleanup': '「编码整理」窗口尚未迁移到网页版，敬请期待。',
  'rename-target':  '「重命名」窗口尚未迁移到网页版，敬请期待。',
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
  if (res.status === 409) throw new Error('SolidWorks 正忙，请稍后重试');
  if (!res.ok) throw new Error('通信失败（HTTP ' + res.status + '）');
  if (!body) throw new Error('通信失败：无效响应');
  if (body.ok) return body.data;
  throw new Error(formatError(body.errorCode, body.errorArgs));
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

function renderButtons(group) {
  currentGroup = group;
  const toolbar = el('toolbar');
  toolbar.innerHTML = '';
  const visible = BUTTONS.filter((b) => !group || b.groups.includes(group));
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
  if (PENDING_FEATURES[b.command]) { showToast(PENDING_FEATURES[b.command], 'info'); return; }
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
    if (b.command === 'sort-components') {
      await sendCommand('sort-components', buildSortArgs(), 600000);
      showToast('装配体排序完成', 'ok');
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
    setConnected(false);
    el('doc-name').textContent = '未连接';
    renderButtons('');
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
  el('cfg-sort-assembly-first').checked = localStorage.getItem('sortAssemblyFirst') === 'true';
  el('cfg-sort-suppressed-last').checked = localStorage.getItem('sortSuppressedLast') === 'true';
  el('cfg-sort-folders').checked = localStorage.getItem('sortFolders') === 'true';
  el('cfg-sort-recursive').checked = localStorage.getItem('sortRecursiveSubAssemblies') === 'true';
  el('cfg-sort-descending').checked = localStorage.getItem('sortDescending') === 'true';
  el('cfg-sort-name-source').value = localStorage.getItem('sortNameSource') || 'ComponentName';
}

function saveSettingsForm() {
  localStorage.setItem('standardPath', el('cfg-standard-path').value.trim());
  localStorage.setItem('sheetFormatPath', el('cfg-sheet-format-path').value.trim());
  localStorage.setItem('sortAssemblyFirst', String(el('cfg-sort-assembly-first').checked));
  localStorage.setItem('sortSuppressedLast', String(el('cfg-sort-suppressed-last').checked));
  localStorage.setItem('sortFolders', String(el('cfg-sort-folders').checked));
  localStorage.setItem('sortRecursiveSubAssemblies', String(el('cfg-sort-recursive').checked));
  localStorage.setItem('sortDescending', String(el('cfg-sort-descending').checked));
  localStorage.setItem('sortNameSource', el('cfg-sort-name-source').value);
  showToast('设置已保存', 'ok');
}

function initSettingsPanel() {
  el('settings-toggle').addEventListener('click', () => {
    const panel = el('settings-panel');
    panel.hidden = !panel.hidden;
    if (!panel.hidden) loadSettingsForm();
  });
  el('settings-save').addEventListener('click', saveSettingsForm);
}

// ─────────── 启动 ───────────
initSettingsPanel();
renderButtons('');
refreshStatus();
connectEvents();
setInterval(refreshStatus, 5000);
