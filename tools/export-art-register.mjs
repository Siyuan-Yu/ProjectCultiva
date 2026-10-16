#!/usr/bin/env node
// One-way TSV -> XLSX view. The TSV remains the only editable authority.
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sourceRelative = 'docs/40-process/273-current-art-asset-register.tsv';
const source = path.join(repo, sourceRelative);
const destination = source.replace(/\.tsv$/i, '.xlsx');
const mainHeaders = ['AssetID', '名称', '分类', '优先级', '制作进度', '品质阶段', '接入/验收状态', '简短备注'];
const required = ['AssetID', '名称', '分类', '优先级', '制作进度', '品质阶段', '接入位置与方式', '验收状态', '备注与替换要求'];
const dataRow = 9;

function parseTsv(text) {
  const records = [];
  let row = [], field = '', quoted = false, atStart = true;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"' && text[i + 1] === '"') { field += '"'; i++; }
      else if (c === '"') quoted = false;
      else field += c;
    } else if (c === '"' && atStart) {
      quoted = true;
    } else if (c === '\t') {
      row.push(field); field = ''; atStart = true;
    } else if (c === '\n' || c === '\r') {
      if (c === '\r' && text[i + 1] === '\n') i++;
      row.push(field); records.push(row); row = []; field = ''; atStart = true;
    } else {
      field += c; atStart = false;
    }
  }
  if (quoted) throw new Error('TSV contains an unclosed quoted field');
  if (row.length || field !== '') { row.push(field); records.push(row); }
  return records;
}

function readSource(bytes) {
  if (!bytes.subarray(0, 3).equals(Buffer.from([0xef, 0xbb, 0xbf])))
    throw new Error('TSV must remain UTF-8 with BOM (EF BB BF)');
  const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes.subarray(3));
  const matrix = parseTsv(text);
  if (matrix.length < 2) throw new Error('TSV needs a header and at least one asset');
  const headers = matrix[0];
  if (new Set(headers).size !== headers.length || required.some(h => !headers.includes(h)))
    throw new Error('TSV has duplicate or missing required headers');
  const rows = matrix.slice(1);
  if (rows.some(r => r.length !== headers.length)) throw new Error('TSV row width differs from header width');
  const idIndex = headers.indexOf('AssetID');
  const ids = rows.map(r => r[idIndex]);
  if (ids.some(id => !id) || new Set(ids).size !== ids.length) throw new Error('AssetID is empty or duplicated');
  const objects = rows.map(r => Object.fromEntries(headers.map((h, i) => [h, r[i]])));
  const counts = Object.fromEntries(['P0', 'P1', 'P2'].map(p => [p, objects.filter(r => r['优先级'] === p).length]));
  return { headers, rows, objects, counts, hash: createHash('sha256').update(bytes).digest('hex') };
}

// All source fields are text. Prefix a formula-like value for Excel display only;
// the verification below also rejects any exported formula cell.
function excelText(value) { return /^[=+\-@]/.test(value) ? "'" + value : value; }
function readbackText(value, original) {
  if (typeof value !== 'string') return String(value ?? '');
  return /^[=+\-@]/.test(original) && value === "'" + original ? original : value;
}

function setTextBlock(sheet, address, values) {
  const range = sheet.getRange(address);
  range.setNumberFormat('@');
  range.values = values.map(row => row.map(excelText));
}

function styleSheet(sheet, columns, rowCount, isMain) {
  const last = String.fromCharCode(64 + columns);
  sheet.showGridLines = false;
  sheet.getRange(`A1:${last}${dataRow + rowCount - 1}`).format.font = { name: 'Aptos', size: 10, color: '#243044' };
  sheet.getRange('A1').format.font = { name: 'Aptos', size: 15, bold: true, color: '#23344D' };
  sheet.getRange('A2').format.font = { name: 'Aptos', size: 10, italic: true, color: '#8A511D' };
  sheet.getRange('A6:H6').format.fill = '#EEF3F7';
  sheet.getRange('A6:H6').format.rowHeight = 25;
  const header = sheet.getRange(`A8:${last}8`);
  header.format.fill = '#25435F';
  header.format.font = { name: 'Aptos', size: 10, bold: true, color: '#FFFFFF' };
  header.format.rowHeight = 31;
  header.format.verticalAlignment = 'center';
  const body = sheet.getRange(`A${dataRow}:${last}${dataRow + rowCount - 1}`);
  body.format.verticalAlignment = 'center';
  body.format.wrapText = true;
  body.format.rowHeight = isMain ? 36 : 44;
  sheet.getRange('A:A').format.columnWidth = 20;
  if (isMain) {
    [25, 19, 11, 16, 16, 25, 54].forEach((width, i) =>
      sheet.getRange(`${String.fromCharCode(66 + i)}:${String.fromCharCode(66 + i)}`).format.columnWidth = width);
  } else {
    for (let i = 1; i < columns; i++) {
      const col = String.fromCharCode(65 + i);
      sheet.getRange(`${col}:${col}`).format.columnWidth = [23, 19, 11, 17, 38, 50, 50, 16, 16, 38, 50, 54, 15, 52, 20][i - 1] ?? 24;
    }
  }
  sheet.freezePanes.freezeRows(8);
  sheet.freezePanes.freezeColumns(1);
  const table = sheet.tables.add(`A8:${last}${dataRow + rowCount - 1}`, true, isMain ? 'ArtOverview' : 'ArtEngineering');
  table.style = 'TableStyleMedium2';
  table.showFilterButton = true;
  const priorityRange = sheet.getRange(`D${dataRow}:D${dataRow + rowCount - 1}`);
  priorityRange.conditionalFormats.add('containsText', { text: 'P0', format: { fill: '#FCE7DE', font: { color: '#9A3412', bold: true } } });
  priorityRange.conditionalFormats.add('containsText', { text: 'P1', format: { fill: '#FEF3C7', font: { color: '#92400E' } } });
  priorityRange.conditionalFormats.add('containsText', { text: 'P2', format: { fill: '#E7EDF3', font: { color: '#475569' } } });
  const progressCol = isMain ? 'E' : 'I';
  const progressRange = sheet.getRange(`${progressCol}${dataRow}:${progressCol}${dataRow + rowCount - 1}`);
  progressRange.conditionalFormats.add('containsText', { text: '待确认', format: { fill: '#FEF3C7' } });
  progressRange.conditionalFormats.add('containsText', { text: '已接入待验', format: { fill: '#DDEBFF' } });
  progressRange.conditionalFormats.add('containsText', { text: '已验收', format: { fill: '#DCFCE7' } });
}

function populate(sheet, headers, rows, sourceInfo, isMain) {
  const n = rows.length;
  setTextBlock(sheet, 'A1', [[isMain ? '美术资产清单' : '美术资产工程明细']]);
  setTextBlock(sheet, 'A2', [['TSV导出查看版，不在此文件单独维护进度']]);
  setTextBlock(sheet, 'A3:B5', [['源文件', sourceRelative], ['导出时间 UTC', sourceInfo.generatedAt], ['源 SHA-256', sourceInfo.hash]]);
  sheet.getRange('A6:H6').values = [['资产数', n, 'P0', sourceInfo.counts.P0, 'P1', sourceInfo.counts.P1, 'P2', sourceInfo.counts.P2]];
  setTextBlock(sheet, `A8:${String.fromCharCode(64 + headers.length)}${dataRow + n - 1}`, [headers, ...rows]);
  styleSheet(sheet, headers.length, n, isMain);
}

async function verifyXlsx(filePath, SpreadsheetFile, FileBlob, sourceInfo) {
  const imported = await SpreadsheetFile.importXlsx(await FileBlob.load(filePath));
  const names = imported.worksheets.items.map(s => s.name);
  if (names.join('|') !== '美术清单|工程明细') throw new Error(`Wrong sheets: ${names.join(', ')}`);
  const overview = imported.worksheets.getItem('美术清单');
  const detail = imported.worksheets.getItem('工程明细');
  const n = sourceInfo.rows.length;
  const detailMatrix = detail.getRange(`A8:${String.fromCharCode(64 + sourceInfo.headers.length)}${dataRow + n - 1}`).values;
  const sourceMatrix = [sourceInfo.headers, ...sourceInfo.rows];
  if (detailMatrix.length !== sourceMatrix.length) throw new Error('Engineering row count changed');
  for (let r = 0; r < sourceMatrix.length; r++) {
    for (let c = 0; c < sourceMatrix[r].length; c++) {
      if (readbackText(detailMatrix[r][c], sourceMatrix[r][c]) !== sourceMatrix[r][c])
        throw new Error(`Engineering content mismatch at row ${r + 8}, column ${c + 1}`);
    }
  }
  const expectedOverview = sourceInfo.objects.map(o => [o.AssetID, o['名称'], o['分类'], o['优先级'], o['制作进度'], o['品质阶段'],
    `${o['制作进度']} / ${o['验收状态']}`, o['备注与替换要求']]);
  const viewMatrix = overview.getRange(`A${dataRow}:H${dataRow + n - 1}`).values;
  for (let r = 0; r < n; r++) for (let c = 0; c < mainHeaders.length; c++)
    if (readbackText(viewMatrix[r][c], expectedOverview[r][c]) !== expectedOverview[r][c])
      throw new Error(`Overview content mismatch at row ${r + dataRow}, column ${c + 1}`);
  const ids = detailMatrix.slice(1).map(r => r[0]);
  if (new Set(ids).size !== n || ids.join('\0') !== sourceInfo.rows.map(r => r[0]).join('\0'))
    throw new Error('AssetID set or order mismatch');
  for (const sheet of [overview, detail]) {
    const summary = sheet.getRange('A6:H6').values[0];
    if (summary[1] !== n || summary[3] !== sourceInfo.counts.P0 || summary[5] !== sourceInfo.counts.P1 || summary[7] !== sourceInfo.counts.P2)
      throw new Error(`Summary mismatch on ${sheet.name}`);
    const sourcePath = sheet.getRange('B3').values[0][0];
    const generatedAt = sheet.getRange('B4').values[0][0];
    if (sourcePath !== sourceRelative || generatedAt !== sourceInfo.generatedAt)
      throw new Error(`Source metadata mismatch on ${sheet.name}: ${JSON.stringify([sourcePath, generatedAt])} versus ${JSON.stringify([sourceRelative, sourceInfo.generatedAt])}`);
    if (sheet.getRange('B5').values[0][0] !== sourceInfo.hash) throw new Error(`Source hash mismatch on ${sheet.name}`);
    if (!sheet.tables.items.length) throw new Error(`Missing filter table on ${sheet.name}`);
  }
  return { count: n, counts: sourceInfo.counts, hash: sourceInfo.hash };
}

async function main() {
  const sourceInfo = readSource(await fs.readFile(source));
  sourceInfo.generatedAt = `UTC ${new Date().toISOString()}`;
  const moduleRoot = process.env.CODEX_WORKSPACE_NODE_MODULES ||
    path.join(os.homedir(), '.cache', 'codex-runtimes', 'codex-primary-runtime', 'dependencies', 'node', 'node_modules');
  const tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'xianxia-art-export-'));
  const tempOutput = path.join(path.dirname(destination), `.${path.basename(destination)}.${process.pid}.tmp`);
  try {
    await fs.symlink(moduleRoot, path.join(tempDir, 'node_modules'), process.platform === 'win32' ? 'junction' : 'dir');
    const require = createRequire(path.join(tempDir, 'entry.cjs'));
    const { Workbook, SpreadsheetFile, FileBlob } = require('@oai/artifact-tool');
    const wb = Workbook.create();
    const overview = wb.worksheets.add('美术清单');
    const detail = wb.worksheets.add('工程明细');
    const viewRows = sourceInfo.objects.map(o => [o.AssetID, o['名称'], o['分类'], o['优先级'], o['制作进度'], o['品质阶段'],
      `${o['制作进度']} / ${o['验收状态']}`, o['备注与替换要求']]);
    populate(overview, mainHeaders, viewRows, sourceInfo, true);
    populate(detail, sourceInfo.headers, sourceInfo.rows, sourceInfo, false);
    wb.recalculate();
    if (process.env.ART_REGISTER_PREVIEW_DIR) {
      await fs.mkdir(process.env.ART_REGISTER_PREVIEW_DIR, { recursive: true });
      for (const [sheetName, fileName, range] of [
        ['美术清单', 'art-overview-preview.png', 'A1:H16'],
        ['工程明细', 'art-detail-preview.png', 'A1:H13'],
      ]) {
        const image = await wb.render({ sheetName, range, scale: 1, format: 'png' });
        await fs.writeFile(path.join(process.env.ART_REGISTER_PREVIEW_DIR, fileName),
          new Uint8Array(await image.arrayBuffer()));
      }
    }
    const output = await SpreadsheetFile.exportXlsx(wb);
    await output.save(tempOutput);
    const result = await verifyXlsx(tempOutput, SpreadsheetFile, FileBlob, sourceInfo);
    try { await fs.rename(tempOutput, destination); }
    catch (error) {
      if (['EACCES', 'EPERM', 'EBUSY'].includes(error.code))
        throw new Error(`Cannot replace ${destination}. Close the workbook in Excel and retry; the previous file is unchanged.`, { cause: error });
      throw error;
    }
    console.log(`Exported ${path.relative(repo, destination)}: ${result.count} assets, P0=${result.counts.P0}, P1=${result.counts.P1}, P2=${result.counts.P2}; full-field readback OK; SHA-256=${result.hash}`);
  } finally {
    await fs.rm(tempOutput, { force: true }).catch(() => {});
    await fs.rm(`${tempOutput}.inspect.ndjson`, { force: true }).catch(() => {});
    await fs.rm(tempDir, { recursive: true, force: true }).catch(() => {});
  }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
