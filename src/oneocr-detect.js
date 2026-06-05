// OneOCR(캡처도구 oneocr.dll) 가용성 탐지 + userData 복사.
// 핵심 제약: C:\Program Files\WindowsApps 안의 oneocr.dll 은 패키지 외 프로세스가
// 직접 LoadLibrary 하면 DllNotFoundException 으로 차단된다(파일 ACL 무관, 로더 정책).
// 따라서 원본을 userData/oneocr 로 1회 복사해 그 사본에서 로드한다(검증됨).

import { existsSync, mkdirSync } from 'node:fs'
import { copyFile, rename } from 'node:fs/promises'
import { join } from 'node:path'
import { execFile } from 'node:child_process'

const FILES = ['oneocr.dll', 'oneocr.onemodel', 'onnxruntime.dll']

// 설치된 패키지의 InstallLocation 조회 (WindowsApps readdir 은 EPERM 이라 사용 불가).
function psInstallLocation(pkgName) {
  return new Promise((resolve) => {
    try {
      execFile(
        'powershell.exe',
        ['-NoProfile', '-NonInteractive', '-Command', `(Get-AppxPackage -Name ${pkgName}).InstallLocation`],
        { windowsHide: true, timeout: 8000 },
        (err, stdout) => resolve(err ? null : (String(stdout || '').trim() || null)),
      )
    } catch {
      resolve(null)
    }
  })
}

function hasAll(dir) {
  return !!dir && FILES.every((f) => existsSync(join(dir, f)))
}

// 캡처도구 → 사진 → Xbox 앱 순으로 OneOCR 원본 폴더를 해석.
async function resolveSource() {
  const candidates = [
    ['Microsoft.ScreenSketch', 'SnippingTool'],
    ['Microsoft.Windows.Photos', ''],
    ['Microsoft.GamingApp', ''],
  ]
  for (const [pkg, sub] of candidates) {
    const loc = await psInstallLocation(pkg)
    if (!loc) continue
    const dir = sub ? join(loc, sub) : loc
    if (hasAll(dir)) return { dir, source: pkg }
  }
  return null
}

// userData/oneocr 에 3개 파일을 보장하고 그 (로드 가능한) 경로를 돌려준다.
// 반환: { available, dir, source }
export async function detectOneOcr(userDataDir) {
  const destDir = join(userDataDir, 'oneocr')

  // 1) 캐시가 완비돼 있으면 그대로 사용(부팅 시 PowerShell 재조회 회피, 버전 변경에도 안정).
  if (hasAll(destDir)) return { available: true, dir: destDir, source: 'cache' }

  // 2) 원본 해석 후 복사(부분복사 방지: .part 로 받고 rename).
  const src = await resolveSource()
  if (!src) return { available: false, dir: null, source: null }
  try {
    mkdirSync(destDir, { recursive: true })
    for (const f of FILES) {
      const tmp = join(destDir, f + '.part')
      await copyFile(join(src.dir, f), tmp)
      await rename(tmp, join(destDir, f))
    }
  } catch {
    if (!hasAll(destDir)) return { available: false, dir: null, source: null }
  }
  return { available: true, dir: destDir, source: src.source }
}
