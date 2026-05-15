const { app, BrowserWindow, dialog, shell } = require('electron');
const path = require('path');
const { startServer, stopServer } = require('../server');

let mainWindow = null;
let serverState = null;
let isQuitting = false;

function stageNotesUrl(port) {
    return `http://127.0.0.1:${port}`;
}

function createMainWindow(port) {
    const window = new BrowserWindow({
        width: 1440,
        height: 960,
        minWidth: 1100,
        minHeight: 720,
        title: 'StageNotes',
        autoHideMenuBar: true,
        backgroundColor: '#101725',
        webPreferences: {
            contextIsolation: true,
            nodeIntegration: false,
            sandbox: true,
        },
    });

    const appUrl = stageNotesUrl(port);
    window.loadURL(appUrl);

    window.webContents.setWindowOpenHandler(({ url }) => {
        shell.openExternal(url);
        return { action: 'deny' };
    });

    window.webContents.on('will-navigate', (event, url) => {
        if (!url.startsWith(appUrl)) {
            event.preventDefault();
            shell.openExternal(url);
        }
    });

    window.on('closed', () => {
        if (mainWindow === window) {
            mainWindow = null;
        }
    });

    return window;
}

async function bootDesktopApp() {
    const dbFile = path.join(app.getPath('userData'), 'data', 'db.json');
    serverState = await startServer({
        host: '127.0.0.1',
        port: 0,
        dbFile,
    });

    mainWindow = createMainWindow(serverState.port);
}

function formatError(error) {
    if (!error) {
        return 'Unknown error';
    }

    return error.stack || error.message || String(error);
}

app.whenReady()
    .then(bootDesktopApp)
    .catch(async (error) => {
        const detail = formatError(error);
        console.error(`Failed to launch StageNotes desktop app:\n${detail}`);

        await dialog.showMessageBox({
            type: 'error',
            title: 'StageNotes Could Not Start',
            message: 'StageNotes could not start its local companion server.',
            detail,
        });

        app.quit();
    });

app.on('activate', () => {
    if (!mainWindow && serverState) {
        mainWindow = createMainWindow(serverState.port);
    }
});

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('before-quit', (event) => {
    if (isQuitting) {
        return;
    }

    event.preventDefault();
    isQuitting = true;

    stopServer()
        .catch((error) => {
            console.error(`Failed to stop StageNotes server cleanly:\n${formatError(error)}`);
        })
        .finally(() => {
            app.quit();
        });
});
