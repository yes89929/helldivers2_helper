// 인게임 장비(방어구/주무기/보조무기/투척무기) 자동 장착 네비게이션 엔진.
// 레퍼런스 ChubbyMaru/HD2-Helper Program.cs 의 RunAutoSelection / ExecuteAutoSelection /
// MoveToTarget 를 1:1 포팅. 입력 합성(tap)·대기(sleep)·현재항목 OCR(ocr)은 주입받는다.
//
// tap(action): action ∈ up/down/left/right/tabPrev/tabNext/select/open/back → 키 입력
// ocr(candidates: string[]): 현재 장착 항목 이름 → Promise<string|null>  (Windows OCR 헬퍼)
// 위치 계산은 레퍼런스와 동일: (group, row=i/cols, col=i%cols), 미보유 압축, 방어구 더미 보정.

import { EQUIPMENT_SLOTS, EQUIPMENT_GRID, GEAR_LAYOUT } from './loadout-equipment-grid.js'

// 슬롯의 OCR 후보 = 해당 타입 전체 이름(미보유 포함). 레퍼런스는 _parsedData.Where(Type==type) 전체와 매칭.
function ocrCandidates(slot) {
  const grid = EQUIPMENT_GRID[slot]
  const names = []
  for (const tab of grid.tabs) for (const n of (grid.items[tab] || [])) names.push(n)
  return names
}

// 레퍼런스 ExecuteAutoSelection 의 itemMap 빌드 (Program.cs:1434-1463).
// disabled(미보유) 필터 → 방어구 'B-01 전술' 뒤 더미 3칸 → (group, i/cols, i%cols).
function buildEquipmentItemMap(slot, disabled) {
  const grid = EQUIPMENT_GRID[slot]
  const colCount = grid.cols
  const itemMap = new Map()
  const groupItemCounts = []
  let group = 0
  for (const tab of grid.tabs) {
    let items = (grid.items[tab] || []).filter((name) => !disabled.has(name))
    if (items.length === 0) continue // 빈 탭은 탭으로 세지 않음 (레퍼런스 GroupBy 와 동일)
    const ti = items.indexOf('B-01 전술')
    if (ti !== -1) {
      items = items.slice()
      items.splice(ti + 1, 0, '__dummy1__', '__dummy2__', '__dummy3__')
    }
    for (let i = 0; i < items.length; i++) {
      itemMap.set(items[i], { group, row: Math.floor(i / colCount), col: i % colCount })
    }
    groupItemCounts.push(items.length)
    group++
  }
  return { itemMap, groupItemCounts, totalTabs: group, colCount }
}

// 레퍼런스 MoveToTarget (Program.cs:1385-1423) 1:1 포팅.
async function moveToTarget(cur, target, ctx, tap, sleep, settleMs) {
  const { totalTabs, colCount, groupItemCounts } = ctx
  const half = Math.trunc(totalTabs / 2) // C# int 나눗셈

  while (cur.group !== target.group) {
    const diff = target.group - cur.group
    if (diff > half || (diff < 0 && diff >= -half)) {
      await tap('tabPrev')
      cur.group = (cur.group - 1 + totalTabs) % totalTabs
    } else {
      await tap('tabNext')
      cur.group = (cur.group + 1) % totalTabs
    }
    cur.row = 0
  }

  await sleep(settleMs)

  while (cur.row !== target.row) {
    const nextR = cur.row < target.row ? cur.row + 1 : cur.row - 1
    if ((nextR * colCount) + cur.col >= groupItemCounts[cur.group]) cur.col = 0
    await tap(cur.row < target.row ? 'down' : 'up')
    cur.row = nextR
  }

  await sleep(settleMs)

  while (cur.col < target.col) { await tap('right'); cur.col++ }
  while (cur.col > target.col) { await tap('left'); cur.col-- }

  await tap('select')
}

// 한 슬롯 처리 (레퍼런스 ExecuteAutoSelection PATH A, index 0-3, Program.cs:1467-1489).
async function executeEquipmentSlot(slot, targetName, disabled, tap, ocr, sleep, settleMs) {
  const ctx = buildEquipmentItemMap(slot, disabled)
  const target = ctx.itemMap.get(targetName)
  if (!target) return // 대상이 미보유/미존재 → 스킵

  // OCR로 현재 장착 항목 감지 (3회 재시도, 150ms)
  const candidates = ocrCandidates(slot)
  let currentName = null
  for (let retry = 0; retry < 3; retry++) {
    currentName = await ocr(candidates)
    if (currentName) break
    await sleep(150)
  }

  // 현재 항목을 알면 그 위치에서 대상까지 이동. 못 알면 이동하지 않음(레퍼런스와 동일).
  if (currentName) {
    const cur = ctx.itemMap.get(currentName)
    if (cur) {
      await moveToTarget({ group: cur.group, row: cur.row, col: cur.col }, target, ctx, tap, sleep, settleMs)
    }
  }
}

// 장비 자동 장착 엔트리 (레퍼런스 RunAutoSelection 의 장비 파트, Program.cs:1334-1372).
// equipment = { armor, primary, secondary, throwable } : 각 슬롯에 장착할 이름 또는 null.
export async function runEquipmentAutoSelect({ equipment, disabled, tap, ocr, sleep, settleMs = 250 }) {
  equipment = equipment || {}
  if (!EQUIPMENT_SLOTS.some((s) => equipment[s])) return

  await tap('open') // R: 로드아웃(군장) 메뉴 열기
  await sleep(settleMs)

  let gearR = 0, gearC = 0
  for (const slot of EQUIPMENT_SLOTS) {
    if (!equipment[slot]) continue
    const [tr, tc] = GEAR_LAYOUT[slot]

    while (gearR < tr) { await tap('down'); gearR++ }
    while (gearR > tr) { await tap('up'); gearR-- }
    while (gearC < tc) { await tap('right'); gearC++ }
    while (gearC > tc) { await tap('left'); gearC-- }

    await tap('select') // Space: 해당 슬롯 선택 서브패널 열기
    await executeEquipmentSlot(slot, equipment[slot], disabled, tap, ocr, sleep, settleMs)
    await tap('back') // Escape: 서브패널 닫기
    await sleep(settleMs)
  }

  await tap('open') // R: 로드아웃 메뉴 닫기 (레퍼런스는 끝에서 R 한번 더)
  await sleep(settleMs)
}

export { buildEquipmentItemMap, ocrCandidates }
