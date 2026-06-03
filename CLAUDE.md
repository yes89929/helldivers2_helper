# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Windows-only Electron + Nuxt 3 desktop helper for the game **Helldivers 2**. It augments the
running game with global hotkey features: Korean chat input overlay, stratagem auto-execution and
on-screen HUD, mouse-driven stratagem input, weapon macros ("mechanized assist"), cinematic-mode HUD
auto-toggle, automatic replay/deathcam recording, and loadout auto-equip. The codebase, comments, and
UI are primarily in **Korean** — match that language when editing existing files.

## Commands

| Command | What it does |
| --- | --- |
| `npm run dev` | Primary dev loop. `concurrently` runs `nuxt dev` (port 3000) + `electron .` with `NODE_ENV=development`. |
| `npm run nuxt` | Nuxt dev server only (renderer at http://localhost:3000). |
| `npm run electron` | Electron only, dev env. Expects the Nuxt server running or `dist/` already built. |
| `npm run generate` | `nuxt generate` → static SPA into `dist/`. Required before any packaging. |
| `npm run pack` | `nuxt generate && electron-builder --dir` → unpacked build. |
| `npm run dist` | `nuxt generate && electron-builder` → NSIS installer into `package/`. |

There are **no tests, no linter, and no formatter** configured — do not invent them. The `start`
script references `electron-forge`, which is not installed; ignore it. npm scripts use Windows `cmd`
`set VAR=` syntax.

Requires running as **Administrator** in production (`build.win.requestedExecutionLevel`), because it
synthesizes input into another process's window.

## Commit convention

Use **Conventional Commits** (`feat:`, `fix:`, `chore:`, `refactor:`, optional scope like `feat(ui):`),
and write the commit message **in Korean**.

## Architecture

Two processes that share **one Nuxt SPA** across four windows.

### Main process — `index.js` (the heart, ~2500 lines)
Runs as **raw ESM directly by Electron** (it is *not* bundled). Holds nearly all logic: settings
persistence, the per-feature input loops, window lifecycle, and the IPC surface. Key collaborators it
imports:

- **`src/user32.js`** — the native layer. `ffi-rs` bindings to `user32/gdi32/kernel32/imm32` DLLs for
  synthetic key/mouse input, window queries (`GetForegroundWindow`/`GetWindowText`/`GetWindowRect`),
  screen capture (BitBlt + `GetDIBits`), and IME control. Critically, it exports a global `keyboard`
  event bus: a `setInterval` polls `GetAsyncKeyState` for every virtual key at ~120 Hz and emits
  `keydown`/`keyup`-style events plus `EVERY`/`EVERY_ALL`. **This is how global hotkeys are detected
  while the game (not the app) has focus** — there is no OS keyboard hook.
- **`steam.js`** — reads the Steam registry (`winreg`) and the user's game config to auto-detect
  username, SteamID, install path, and in-game keybinds. Polled every ~2 s, so no manual key setup is
  needed. Also builds game-invite/join links.
- **`record.js`** — shells out to the bundled `ffmpeg/` binary to capture gameplay and deathcam clips
  into `Videos/HELLDIVERS 2`. Includes WebP conversion for deathcam previews.
- **`src/loadout-autoselect.js` + `src/loadout-grid.js`** — pure navigation logic that auto-equips a
  saved preset by injecting key taps to walk the in-game loadout grid (ported from
  ChubbyMaru/HD2-Helper). `loadout-grid.js` is the baked grid coordinate model.

### Renderer — Nuxt 3 SPA (`ssr: false`, static)
`app.vue` → `<NuxtLayout><NuxtPage/></NuxtLayout>`. The pages in `pages/` are not navigated by URL;
instead each window is told which route to mount via the `initRoute` IPC handshake (see below).
`utils/stratagems.js` is the renderer-side stratagem catalog (auto-imported via `nuxt.config.js`
`imports.dirs`).

### The four windows (all load the same SPA, routed to different pages)
Created in `createMainWindow()`. Each calls `loadURL('app://dist/index.html')` in prod /
`http://localhost:3000` in dev. On boot, `app.vue` sends `initRoute`; the main process replies with a
per-window route, mapping each `BrowserWindow` to its page:

- `windows.main` → `/main` — the control panel: settings, mod manager, stratagem/preset picker.
- `windows.overlay` → `/overlay` — in-game stratagem HUD; always-on-top, click-through, follows the
  game window's monitor/size.
- `windows.chat` → `/chat` — the Korean chat input box overlay.
- `windows.record` → `/record` — deathcam/recording display surface.

### IPC and persistence
`preload.js` exposes `window.ipcRenderer` (`send`/`invoke`/`on`/`open`/`listen`). The main process has
~80 `ipcMain.on`/`ipcMain.handle` handlers — most are a single setting toggle that updates an in-memory
value and writes it back to a JSON file under `app.getPath('userData')`:
`user.settings.json`, `user.settings.keybinds.json`, `user.settings.presets.json`,
`user.settings.disabled.json`. The custom `app://` protocol is registered in `app.whenReady()` and
serves files from the app path. Auto-update (`electron-updater`) is currently **disabled** (commented
out in `whenReady`).

Game-focus detection keys off the window title `HELLDIVERS™ 2` (with the ™ glyph); features only act
when that window is foreground.

## Critical build constraint: main-process modules go in `src/`, not `utils/`

`package.json` `build.files` **excludes `utils/**`** (along with `components/`, `pages/`, `app.vue`,
etc.) from the packaged app, because those are bundled into the Nuxt static build and don't need to ship
as raw files. But `index.js` runs as raw ESM, so **every module it imports must physically exist in the
package**. Therefore any helper the main process imports must live in `src/` (kept in the package).

When adding a new main-process module, create it under `src/` and import via `./src/...`. Put
renderer-only data/utilities in `utils/`. Precedent: `src/user32.js`, `src/loadout-autoselect.js`,
`src/loadout-grid.js`.

## `.reference/`

Gitignored and excluded from the build. Contains the upstream ChubbyMaru/HD2-Helper sources and the
generator scripts (`derive-grid.mjs`, `convert-disabled.mjs`) used to bake `src/loadout-grid.js` and
the disabled-item list. Regenerate from here rather than hand-editing the baked grid.
