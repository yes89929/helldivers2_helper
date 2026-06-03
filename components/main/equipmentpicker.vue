<template>
  <div class="equip-modal" @click.self="$emit('close')">
    <div class="panel">
      <div class="head">
        <h2 class="title">{{ slotLabel }} 선택</h2>
        <button class="close" @click="$emit('close')" title="닫기 (Esc)">✕</button>
      </div>
      <input
        ref="searchEl"
        class="search"
        v-model="q"
        :placeholder="searchPlaceholder"
      >
      <div class="body">
        <div class="group" v-for="g in filteredGroups" :key="g.category">
          <div class="group-title">{{ g.category }}</div>
          <div class="items">
            <div class="card" v-for="it in g.items" :key="it.name"
              :class="{ active: selected && selected.name === it.name }"
              @click="$emit('select', it)"
            >
              <img class="thumb" :src="imgPath(it.name)" :alt="it.name" loading="lazy">
              <div class="info">
                <div class="name">{{ it.name }}</div>
                <div class="desc">{{ it.desc }}</div>
                <div class="passive" v-if="it.passive">
                  <img class="passive-icon" :src="passiveIcon(it.passive)" :alt="it.passive" loading="lazy">
                  <div class="passive-text">
                    <div class="passive-name">{{ it.passive }}</div>
                    <div class="passive-desc">{{ passiveDesc(it.passive) }}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div v-if="!filteredGroups.length" class="empty">검색 결과가 없습니다.</div>
      </div>
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  slotKey: { type: String, required: true },
  selected: { type: Object, default: null }
})
const emit = defineEmits(['select', 'close'])

const q = ref('')
const searchEl = ref(null)

const slotMeta = computed(() => EQUIPMENT_SLOTS.find(s => s.key === props.slotKey))
const slotLabel = computed(() => slotMeta.value?.label || '')
const folder = computed(() => slotMeta.value?.folder || 'weapons')
const groups = computed(() => EQUIPMENT[props.slotKey] || [])
const isArmor = computed(() => props.slotKey === 'armor')
const searchPlaceholder = computed(() => isArmor.value ? '이름·패시브로 검색' : '이름·특성으로 검색')

const imgPath = (name) => `/equipment/${folder.value}/${name}.png`
const passiveIcon = (name) => `/equipment/passives/${name}.png`
const passiveDesc = (name) => PASSIVES[name]?.desc || ''

const f_matches = (it, term) => {
  if (it.name.toLowerCase().includes(term)) return true
  if (it.desc && it.desc.toLowerCase().includes(term)) return true
  if (it.passive) {
    if (it.passive.toLowerCase().includes(term)) return true
    const p = PASSIVES[it.passive]
    if (p) {
      if ((p.desc || '').toLowerCase().includes(term)) return true
      if ((p.aliases || []).some(a => a.toLowerCase().includes(term))) return true
    }
  }
  return false
}

const filteredGroups = computed(() => {
  const term = q.value.trim().toLowerCase()
  if (!term) return groups.value
  return groups.value
    .map(g => ({ category: g.category, items: g.items.filter(it => f_matches(it, term)) }))
    .filter(g => g.items.length)
})

const f_onkey = (e) => { if (e.key === 'Escape') emit('close') }
onMounted(() => {
  window.addEventListener('keydown', f_onkey)
  nextTick(() => searchEl.value?.focus())
})
onBeforeUnmount(() => window.removeEventListener('keydown', f_onkey))
</script>

<style lang="scss" scoped>
.equip-modal {
  position: fixed;
  inset: 0;
  z-index: 100000;
  background: rgba(0, 0, 0, .7);
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  .panel {
    width: 760px;
    max-width: calc(100vw - 60px);
    max-height: 82vh;
    box-sizing: border-box;
    background: rgb(20, 21, 24);
    border: 2px solid rgba(255, 232, 0, .5);
    display: flex;
    flex-direction: column;
    padding: 16px;
  }
  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 12px;
    .title {
      margin: 0;
      font-size: 22px;
    }
    .close {
      background: transparent;
      border: 2px solid rgba(255, 255, 255, .3);
      color: white;
      width: 32px;
      height: 32px;
      cursor: pointer;
      font-size: 14px;
      &:hover {
        border-color: rgb(255, 232, 0);
      }
    }
  }
  .search {
    box-sizing: border-box;
    width: 100%;
    height: 36px;
    padding: 0 12px;
    margin-bottom: 12px;
    background: rgba(0, 0, 0, .4);
    border: 2px solid rgba(255, 255, 255, .3);
    color: white;
    font-size: 14px;
    outline: none;
    &:focus {
      border-color: rgb(255, 232, 0);
    }
  }
  .body {
    overflow-y: auto;
    overflow-x: hidden;
    padding-right: 6px;
    &::-webkit-scrollbar {
      width: 6px;
    }
    &::-webkit-scrollbar-thumb {
      background: rgba(255, 255, 255, .25);
    }
  }
  .group {
    margin-bottom: 16px;
    .group-title {
      font-size: 15px;
      font-weight: 600;
      color: rgb(255, 232, 0);
      border-left: 3px solid rgb(255, 232, 0);
      padding-left: 8px;
      margin-bottom: 8px;
    }
    .items {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(330px, 1fr));
      gap: 8px;
    }
  }
  .card {
    display: flex;
    gap: 10px;
    padding: 8px;
    box-sizing: border-box;
    border: 2px solid rgba(255, 255, 255, .2);
    background: rgba(0, 0, 0, .3);
    cursor: pointer;
    &:hover {
      border-color: rgba(255, 232, 0, .6);
      background: rgba(0, 0, 0, .5);
    }
    &.active {
      border-color: rgb(255, 232, 0);
      background: rgba(255, 232, 0, .12);
    }
    .thumb {
      width: 60px;
      height: 60px;
      object-fit: contain;
      flex: none;
    }
    .info {
      min-width: 0;
      .name {
        font-size: 14px;
        font-weight: 600;
        margin-bottom: 2px;
      }
      .desc {
        font-size: 11px;
        font-weight: 300;
        opacity: .8;
        white-space: pre-line;
        line-height: 1.4;
      }
      .passive {
        display: flex;
        gap: 6px;
        margin-top: 6px;
        padding-top: 6px;
        border-top: 1px solid rgba(255, 255, 255, .15);
        .passive-icon {
          width: 28px;
          height: 28px;
          object-fit: contain;
          flex: none;
        }
        .passive-text {
          min-width: 0;
          .passive-name {
            font-size: 12px;
            font-weight: 600;
            color: rgb(255, 232, 0);
          }
          .passive-desc {
            font-size: 10px;
            font-weight: 300;
            opacity: .7;
            white-space: pre-line;
            line-height: 1.35;
          }
        }
      }
    }
  }
  .empty {
    opacity: .5;
    padding: 20px;
    text-align: center;
  }
}
</style>
