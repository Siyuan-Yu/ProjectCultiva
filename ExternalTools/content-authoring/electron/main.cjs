const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const path = require('path');
const fs = require('fs');

const isDev = !app.isPackaged;

function createWindow() {
  const win = new BrowserWindow({
    width: 1400,
    height: 900,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  if (isDev) {
    win.loadURL('http://127.0.0.1:5173');
    win.webContents.openDevTools({ mode: 'detach' });
  } else {
    win.loadFile(path.join(__dirname, '..', 'dist', 'index.html'));
  }
}

function defaultPackageRoot() {
  // ExternalTools/content-authoring → repo/Content/BaseGame
  const fromDev = path.resolve(__dirname, '..', '..', '..', 'Content', 'BaseGame');
  if (fs.existsSync(path.join(fromDev, 'manifest.json'))) return fromDev;
  return '';
}

ipcMain.handle('dialog:openPackage', async () => {
  const result = await dialog.showOpenDialog({
    title: '选择 Content/BaseGame 包目录',
    properties: ['openDirectory']
  });
  if (result.canceled || !result.filePaths[0]) return null;
  return result.filePaths[0];
});

ipcMain.handle('fs:defaultPackageRoot', () => defaultPackageRoot());

ipcMain.handle('fs:readText', async (_e, filePath) => {
  return fs.promises.readFile(filePath, 'utf8');
});

ipcMain.handle('fs:writeText', async (_e, filePath, text) => {
  await fs.promises.mkdir(path.dirname(filePath), { recursive: true });
  await fs.promises.writeFile(filePath, text, 'utf8');
  return true;
});

ipcMain.handle('fs:listJsonFiles', async (_e, packageRoot) => {
  const dataDir = path.join(packageRoot, 'Data');
  const out = [];
  async function walk(dir) {
    const entries = await fs.promises.readdir(dir, { withFileTypes: true });
    for (const ent of entries) {
      const full = path.join(dir, ent.name);
      if (ent.isDirectory()) await walk(full);
      else if (ent.isFile() && ent.name.toLowerCase().endsWith('.json')) out.push(full);
    }
  }
  if (!fs.existsSync(dataDir)) return [];
  await walk(dataDir);
  return out;
});

ipcMain.handle('shell:showItem', async (_e, filePath) => {
  const { shell } = require('electron');
  shell.showItemInFolder(filePath);
});

app.whenReady().then(createWindow);
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow();
});
