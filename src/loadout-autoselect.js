// 로드아웃(장비구성) 화면 스트라타젬 자동 선택 엔진.
//
// 참조 구현(ChubbyMaru/HD2-Helper, Program.cs의 ExecuteAutoSelection·MoveToTarget)을
// 이 프로젝트로 포팅한 것. 키 입력은 주입식 `tap(action)`으로 분리해 순수 로직으로 유지한다.
//
// 격자 모델은 utils/loadout-grid.js 에서 가져오며, 미보유(제외) 항목을 빼고 나면
// 실제 인게임 격자와 동일한 좌표가 된다. 커서는 패널을 열 때 (탭0, 0행, 0열)에서 시작한다고 가정.

import { STRATAGEM_GRID, STRATAGEM_GRID_TABS, STRATAGEM_GRID_COLS } from './loadout-grid.js'

// tap(action)에 전달되는 의미론적 동작들 (호출부에서 실제 키로 매핑)
export const ACTIONS = {
  UP: 'up',           // 격자 위로 (기본 W)
  DOWN: 'down',       // 격자 아래로 (기본 S)
  LEFT: 'left',       // 격자 왼쪽 (기본 A)
  RIGHT: 'right',     // 격자 오른쪽 (기본 D)
  TAB_PREV: 'tabPrev',// 이전 카테고리 탭 (기본 Z)
  TAB_NEXT: 'tabNext',// 다음 카테고리 탭 (기본 C)
  SELECT: 'select',   // 선택/패널 열기 (기본 SPACE)
}

// 제외 목록을 반영한 격자 맵을 만든다.
// 반환: { itemMap: Map<name,{group,row,col}>, groupItemCounts: number[], totalTabs }
export function buildItemMap(disabled = new Set()) {
  const colCount = STRATAGEM_GRID_COLS
  const itemMap = new Map()
  const groupItemCounts = []
  let group = 0
  for (const tab of STRATAGEM_GRID_TABS) {
    const items = (STRATAGEM_GRID[tab] || []).filter(name => !disabled.has(name))
    if (items.length === 0) continue // 비어 있는 탭은 인게임에도 없다고 가정 (참조 GroupBy 동작)
    for (let i = 0; i < items.length; i++) {
      itemMap.set(items[i], {
        group,
        row: Math.floor(i / colCount),
        col: i % colCount,
      })
    }
    groupItemCounts.push(items.length)
    group++
  }
  return { itemMap, groupItemCounts, totalTabs: group, colCount }
}

// 현재 커서(cur)에서 target 좌표로 이동 후 선택. cur는 호출부에서 갱신.
// ctx = { groupItemCounts, totalTabs, colCount }
async function moveToTarget(cur, target, ctx, tap, sleep, settleMs) {
  const { groupItemCounts, totalTabs, colCount } = ctx

  // 1) 탭 이동 (Z/C 중 링 최단경로)
  while (cur.g !== target.group) {
    const diff = target.group - cur.g
    if (diff > Math.floor(totalTabs / 2) || (diff < 0 && diff >= -Math.floor(totalTabs / 2))) {
      await tap(ACTIONS.TAB_PREV)
      cur.g = (cur.g - 1 + totalTabs) % totalTabs
    } else {
      await tap(ACTIONS.TAB_NEXT)
      cur.g = (cur.g + 1) % totalTabs
    }
    cur.r = 0
  }
  await sleep(settleMs)

  // 2) 행 이동 (S/W). 마지막 불완전 행에서는 게임이 열을 0으로 스냅하므로 추적.
  while (cur.r !== target.row) {
    const nextR = cur.r < target.row ? cur.r + 1 : cur.r - 1
    if (nextR * colCount + cur.c >= groupItemCounts[cur.g]) {
      cur.c = 0
    }
    await tap(cur.r < target.row ? ACTIONS.DOWN : ACTIONS.UP)
    cur.r = nextR
  }
  await sleep(settleMs)

  // 3) 열 이동 (D/A)
  while (cur.c < target.col) { await tap(ACTIONS.RIGHT); cur.c++ }
  while (cur.c > target.col) { await tap(ACTIONS.LEFT); cur.c-- }

  // 4) 선택
  await tap(ACTIONS.SELECT)
}

// 스트라타젬 자동 선택 실행.
// selectedNames: 사용자가 구성한 스트라타젬 이름 배열(순서대로, 임무 타입은 호출부에서 제외 권장)
// 반환: 실제로 선택 시도한 이름 배열
export async function runStratagemAutoSelect({
  selectedNames = [],
  disabled = new Set(),
  tap,
  sleep,
  settleMs = 250,
}) {
  const { itemMap, groupItemCounts, totalTabs, colCount } = buildItemMap(disabled)
  const ctx = { groupItemCounts, totalTabs, colCount }

  // 격자에 존재(=보유, 비-임무)하는 항목만, 최대 4개
  const targets = []
  for (const name of selectedNames) {
    if (itemMap.has(name)) targets.push(name)
    if (targets.length >= 4) break
  }
  if (targets.length === 0) return []

  // 스트라타젬 선택 패널 열기 (커서는 탭0/0행/0열에서 시작)
  await tap(ACTIONS.SELECT)
  await sleep(settleMs)

  const cur = { g: 0, r: 0, c: 0 }
  for (const name of targets) {
    const target = itemMap.get(name)
    await moveToTarget(cur, target, ctx, tap, sleep, settleMs)
    cur.g = target.group
    cur.r = target.row
    cur.c = target.col
  }
  return targets
}
