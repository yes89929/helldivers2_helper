// 장비 자동 장착용 OCR 브리지.
// Windows.Media.Ocr 기반 C# 헬퍼(ocr/hd2-ocr-helper.exe)를 persistent 프로세스로 띄우고,
// stdin 한 줄(탭 구분 후보 이름) → stdout 한 줄(매칭 이름)로 통신한다.
// 헬퍼는 자체적으로 게임 화면을 캡처/전처리/OCR/매칭한다(레퍼런스 MatchItemFromScreen 포팅).

import { spawn } from 'node:child_process'

let proc = null
let readyPromise = null
let readyResolve = null
let buffer = ''
let engine = null // 'ONEOCR' | 'WINMEDIA' | null
const queue = [] // [{ resolve, timer }]

const CALL_TIMEOUT_MS = 6000

// 헬퍼를 (필요 시) 시작하고, READY 신호를 기다리는 Promise를 돌려준다.
export function startOcrHelper(exePath, oneocrDir) {
  if (proc) return readyPromise
  readyPromise = new Promise((resolve) => { readyResolve = resolve })
  try {
    proc = spawn(exePath, [oneocrDir || ''], { windowsHide: true })
  } catch (e) {
    proc = null
    readyResolve?.(false)
    return readyPromise
  }
  proc.stdout.setEncoding('utf8')
  proc.stdout.on('data', (chunk) => {
    buffer += chunk
    let idx
    while ((idx = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, idx).replace(/\r$/, '')
      buffer = buffer.slice(idx + 1)
      if (readyResolve) {
        // 첫 줄: "READY ONEOCR" / "READY WINMEDIA" / "NO_OCR"
        const parts = line.split(' ')
        if (parts[0] === 'READY') { engine = parts[1] || 'WINMEDIA'; readyResolve(true) }
        else { engine = null; readyResolve(false) }
        readyResolve = null
        continue
      }
      const job = queue.shift()
      if (job) {
        clearTimeout(job.timer)
        job.resolve(line.length ? line : null)
      }
    }
  })
  const cleanup = () => {
    proc = null
    engine = null
    if (readyResolve) { readyResolve(false); readyResolve = null }
    while (queue.length) { const j = queue.shift(); clearTimeout(j.timer); j.resolve(null) }
    buffer = ''
  }
  proc.on('exit', cleanup)
  proc.on('error', cleanup)
  proc.stdin.on('error', () => {})
  return readyPromise
}

export function stopOcrHelper() {
  if (proc) {
    try { proc.stdin.end() } catch {}
    try { proc.kill() } catch {}
    proc = null
  }
}

// 현재 장착된 항목 이름을 OCR로 인식. 실패/타임아웃 시 null.
export function ocrCurrentItem(candidates) {
  return new Promise((resolve) => {
    if (!proc || !proc.stdin || !proc.stdin.writable) { resolve(null); return }
    const timer = setTimeout(() => {
      const i = queue.findIndex((j) => j.timer === timer)
      if (i !== -1) queue.splice(i, 1)
      resolve(null)
    }, CALL_TIMEOUT_MS)
    queue.push({ resolve, timer })
    try {
      proc.stdin.write((candidates || []).join('\t') + '\n')
    } catch (e) {
      clearTimeout(timer)
      const i = queue.findIndex((j) => j.timer === timer)
      if (i !== -1) queue.splice(i, 1)
      resolve(null)
    }
  })
}

export function isOcrRunning() {
  return !!proc
}

// 현재 헬퍼가 초기화한 엔진: 'ONEOCR' | 'WINMEDIA' | null(미기동/실패)
export function getEngine() {
  return engine
}
