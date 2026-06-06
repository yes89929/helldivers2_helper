<template>
  <div class="_oaverlay">
    <div class="stratagems" :class="{ focusing: _focusindex != -1, cinematic: _cinematic_mode }">
      <div class="stratagem" v-for="(stratagem, index) in _stratagems" :key="stratagem.name"
        :class="{ focus: _focusindex == index }"
      >
        <img class="icon" :src="stratagem.icon" alt="" :class="{ canuse: !f_cooldown(stratagem) }">
        <img class="cooldown" :src="stratagem.icon" alt=""
          v-if="f_cooldown(stratagem) > 0"
          :style="{ 'clip-path': `polygon(0 ${f_cooldown(stratagem) * 100}%, 100% ${f_cooldown(stratagem) * 100}%, 100% 100%, 0% 100%)` }"
        >
        <div class="cdtimer" v-if="f_cooldown(stratagem) > 0" :class="{ estimate: !f_using_ocr(stratagem) }">{{ f_remain_seconds(stratagem) }}s</div>
        <div class="rotatekey" v-if="index == 0">{{ f_get_key_string(_rotatekey) }}</div>
        <div class="rotatekey" v-else-if="index == _stratagems.length - 1">{{ f_get_key_string(_rotatekey_reverse) }}</div>
        <div class="hotkey" v-if="stratagem.hotkey">{{ f_get_key_string(stratagem.hotkey) }}</div>
      </div>
      <div class="stratagem reinforce" v-if="_stratagems.length">

        <img class="icon canuse" :src="_reinforce.icon" alt="">
        <div class="hotkey" v-if="_reinforce.hotkey">{{ f_get_key_string(_reinforcekey) }}</div>
      </div>
    </div>
    <div class="mouse_stratagem" v-if="_mouse_stratagem_state">마우스 스트라타젬 입력중</div>
    <div class="record" v-if="_record_alert">{{ _record_alert }}</div>
  </div>
</template>

<script setup>
definePageMeta({
  layout: 'overlay'
})


const _stratagems = ref([])
const _reinforce = {
  name: 'Reinforce',
  keys: ['up', 'down', 'right', 'left', 'up'],
  icon: '/stratagems/General Stratagems/Reinforce.svg',
  cooldown: 0,
  takedown: 0,
  hotkey: '`~'
}

ipcRenderer.on('stratagemsets', array => {
  _stratagems.value = array
})

ipcRenderer.on('stratagemFire', stratagem => {
  const target = _stratagems.value.find(s => s.name == stratagem.name)
  if (target) target.lastFire = stratagem.lastFire
  if (target.name.includes('Eagle')) {
    _stratagems.value.forEach(e => {
      if (e.name != 'Eagle Rearm' && e.name.includes('Eagle')) {
        e.cooldown = stratagem.cooldown
        e.lastFire = stratagem.lastFire
      }
    })
  }
})

// OCR로 읽은 실제 잔여 쿨다운(권위값). rows: [{ name, remainMs }]
ipcRenderer.on('stratagemCooldowns', rows => {
  if (!Array.isArray(rows)) return
  const now = Date.now()
  rows.forEach(row => {
    if (!row || !row.name) return
    const target = _stratagems.value.find(s => s.name == row.name)
    if (target) { target.ocrRemainMs = row.remainMs; target.ocrAt = now }
  })
})

const _focusindex = ref(-1)
ipcRenderer.on('stratagemFocus', index => {
  _focusindex.value = index
})
const _process = ref(false)
onMounted(async () => {
  _process.value = await ipcRenderer.invoke('loaded', 'overlay')
  if (!_process.value.isDev) {
    onkeydown = e => {
      if(e.key === 'Tab') {
        e.preventDefault()
      }
      if(e.key === 'r' && e.ctrlKey) {
        e.preventDefault()
      }
      if (e.key === 'I' && e.ctrlKey) {
        e.preventDefault()
      }
    }
  }
})

const _now = ref(Date.now())
setInterval(() => {
  _now.value = Date.now()
}, 100)
// 잔여 ms: OCR 실측을 우선하되, 던짐(lastFire)이 OCR 샘플(ocrAt)보다 나중이면 정적 추정이 최신.
const f_remain_ms = (stratagem) => {
  const ocr = (stratagem.ocrRemainMs != null && stratagem.ocrAt)
    ? stratagem.ocrRemainMs - (_now.value - stratagem.ocrAt)
    : null
  const est = stratagem.lastFire
    ? (stratagem.cooldown + stratagem.takedown) - (_now.value - stratagem.lastFire)
    : null
  let useOcr = ocr != null
  if (useOcr && stratagem.lastFire && stratagem.ocrAt && stratagem.lastFire > stratagem.ocrAt) useOcr = false
  const remain = useOcr ? ocr : est
  return remain && remain > 0 ? remain : 0
}
// 현재 표시값이 OCR 실측 기반인지(정적 추정이면 false → '추정' 흐린 색)
const f_using_ocr = (stratagem) => {
  if (stratagem.ocrRemainMs == null || !stratagem.ocrAt) return false
  if (stratagem.lastFire && stratagem.lastFire > stratagem.ocrAt) return false
  return (stratagem.ocrRemainMs - (_now.value - stratagem.ocrAt)) > 0
}
// 아이콘 채움 비율(0~1). 숫자는 정확, 바는 정적 총량 기준 근사.
const f_cooldown = (stratagem) => {
  const remain = f_remain_ms(stratagem)
  if (remain <= 0) return 0
  const total = (stratagem.cooldown + stratagem.takedown) || remain
  return Math.min(1, remain / total)
}
const f_remain_seconds = (stratagem) => Math.ceil(f_remain_ms(stratagem) / 1000)

const _cinematic_mode = ref(false)
ipcRenderer.on('cinematic_mode', v => {
  _cinematic_mode.value = v
})

const _mouse_stratagem_state = ref(false)
ipcRenderer.on('mouse_stratagem_state', v => {
  _mouse_stratagem_state.value = v
})

const _record_alert = ref('')
let _record_alert_timer = null
ipcRenderer.on('record_started', v => {
  _record_alert.value = '다시보기를 저장하고 있습니다.'
  if (_record_alert_timer) clearTimeout(_record_alert_timer)
  _record_alert_timer = setTimeout(() => {
    _record_alert.value = ''
  }, 3000)
})
ipcRenderer.on('record_saved', ({ path, length }) => {
  _record_alert.value = `${length}초 다시보기가 저장되었습니다.`
  if (_record_alert_timer) clearTimeout(_record_alert_timer)
  _record_alert_timer = setTimeout(() => {
    _record_alert.value = ''
  }, 3000)
})


const f_get_key_string = (key) => {
  const key_string_map = {
    'RBUTTON': 'RM',
    'HANGUL': '한/영',
    'XBUTTON1': 'MB1',
    'XBUTTON2': 'MB2',
    'XBUTTON3': 'MB3',
    'XBUTTON4': 'MB4',
    'XBUTTON5': 'MB',
    'XBUTTON6': 'MB6',
    'XBUTTON7': 'MB7',
    'XBUTTON8': 'MB8',
    'LMENU': 'LALT',
    'RMENU': 'RALT',
    'OEM_PERIOD': '.>',
    'OEM_COMMA': '<,',
    'OEM_MINUS': '-_',
    'OEM_PLUS': '+*',
    'OEM_1': ';:',
    'OEM_2': '/?',
    'OEM_3': '`~',
    'OEM_4': '[{',
    'OEM_5': '|\\',
    'OEM_6': '}]',
    'OEM_7': '\'"',
    'OEM_8': '``',
    'ADD': '+',
    'SUBTRACT': '-',
    'MULTIPLY': '*',
    'DIVIDE': '/'
    
  }
  return key_string_map[key] || key
}
const _rotatekey = ref('T')
const _rotatekey_reverse = ref('H')
const _reinforcekey = ref('OEM_3')
ipcRenderer.on('keybinds', v => {
  _rotatekey.value = v.rotatekey
  _rotatekey_reverse.value = v.rotatekey_reverse
  _reinforcekey.value = v.reinforce
})
</script>



<style lang="scss" scoped>
._oaverlay {
  display: flex;
  justify-content: center;
  align-items: center;
  .mouse_stratagem {
    position: fixed;
    top: 0;
    font-size: 7vh;
    background: white;
    padding: 2px 4px;
    color: black;
    opacity: .5;
  }
  .record {
    position: fixed;
    top: 0;
    font-size: 7vh;
    background: white;
    padding: 2px 4px;
    color: black;
    opacity: .5;
    margin-left: 150vh;
  }
  .stratagems {
    position: fixed;
    bottom: 42vh;
    display: flex;
    opacity: .5;
    .stratagem {
      position: relative;
      display: flex;
      justify-content: center;
      align-items: center;
      margin: 5vh;
      background: rgba(0, 0, 0, .05);
      border: 1.5vh solid transparent;
      transform: scale(.8);
      .icon {
        height: 30vh;
        opacity: .3;
        &.canuse {
          opacity: 1;
        }
      }
      &.focus {
        border-color: rgb(255, 232, 0);
        background: rgba(0, 0, 0, .3);
        // 쿨타임/로테이션 종료 시 아이콘이 커진 채로 가끔 복구되지 않는 문제 → 확대(scale) 효과 제거.
        // 포커스 표시는 테두리/배경만으로 충분하다.
      }
      .cooldown {
        opacity: 1;
        position: absolute;
      }
      .cdtimer {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        font-size: 9vh;
        font-weight: 700;
        color: #fff;
        text-shadow: 0 0 1.2vh #000, 0 0 0.6vh #000;
        pointer-events: none;
        &.estimate { color: #b8b8b8; }
      }
      .rotatekey {
        position: absolute;
        bottom: -7.5vh;
        font-size: 10vh;
        background: white;
        padding: 2px 4px;
        color: black;
      }
      .hotkey {
        position: absolute;
        top: -6vh;
        font-size: 10vh;
        background: white;
        padding: 2px 4px;
        color: black;
      }
      &.reinforce {
        margin-left: 15vh;
      }
    }
    &.focusing {
      opacity: 1;
    }
    &.cinematic {
      bottom: 5vh;
      .stratagem {
        .icon {
          height: 20vh;
        }
        .rotatekey {
          font-size: 5vh;
          bottom: -3.5vh;
        }
        .hotkey {
          font-size: 5vh;
          top: -3.5vh;
        }
        .cdtimer {
          font-size: 6vh;
        }
      }
    }
  }
}
</style>
