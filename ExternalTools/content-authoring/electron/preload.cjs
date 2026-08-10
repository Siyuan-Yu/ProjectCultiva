const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('studioApi', {
  openPackageDialog: () => ipcRenderer.invoke('dialog:openPackage'),
  defaultPackageRoot: () => ipcRenderer.invoke('fs:defaultPackageRoot'),
  readText: (filePath) => ipcRenderer.invoke('fs:readText', filePath),
  writeText: (filePath, text) => ipcRenderer.invoke('fs:writeText', filePath, text),
  listJsonFiles: (packageRoot) => ipcRenderer.invoke('fs:listJsonFiles', packageRoot),
  showItemInFolder: (filePath) => ipcRenderer.invoke('shell:showItem', filePath)
});
