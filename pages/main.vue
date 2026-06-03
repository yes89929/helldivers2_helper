<template>
  <div class="_main" :class="{ navless: c_pagetype != 'modmanager' }">
    <MainModmanager v-show="c_pagetype == 'modmanager'"/>
    <div class="page" v-show="c_pagetype != 'modmanager'">
      <div class="loadout-area">
        <div class="console">
          <h2 class="col-title">스트라타젬</h2>
          <div class="categories">
            <div class="category-col" v-for="(col, ci) in _category_layout" :key="ci">
              <div class="category" v-for="category in col" :key="category">
                <h2 class="title">{{ _categories[category][_i18n] }}</h2>
                <div class="stratagems" :class="category">
                  <div class="stratagem" v-for="stratagem in c_stratagems[category]" :key="stratagem.name"
                    @click="f_isSelected(stratagem) ? f_removestratagem(stratagem) : f_addstratagem(stratagem)"
                    @contextmenu.prevent="f_toggle_disabled(stratagem)"
                    :class="{ selected: f_isSelected(stratagem), disabled: f_is_disabled(stratagem) }"
                    :title="f_is_disabled(stratagem) ? '미보유(자동 장착 제외) — 우클릭으로 해제' : '우클릭: 미보유로 표시(자동 장착에서 제외)'"
                  >
                    <img :src="stratagem.icon" alt="">
                    <!-- <span v-if="stratagem.code">{{ stratagem.code }} </span>{{ stratagem.name }} {{ stratagem.index }} -->
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="stratagemsets">
            <!-- 1행: 공격/보급/방어에서 선택한 스트라타젬 (6 슬롯) -->
            <div class="set-row main">
              <div class="stratagem" v-for="i in 6" :key="i"
                @click="f_removestratagem(_stratagemsets[i])"
                :class="{ selected: _stratagemsets[i] }"
              >
                <img :src="_stratagemsets[i]?.icon" alt="">
              </div>
            </div>
            <!-- 2행: 임무(mission) / 기타(default) — 윗 행과 동일한 6열 그리드 -->
            <div class="set-row sub">
              <!-- 임무 슬롯: 미선택이어도 빈칸 표시(윗 행 6칸과 위치/너비/간격 일치) -->
              <div class="mission">
                <div class="stratagem" v-for="i in c_mission_slot_count" :key="i"
                  :class="{ selected: _mission_stratagems[i - 1] }"
                  @click="_mission_stratagems[i - 1] && f_removestratagem(_mission_stratagems[i - 1])"
                >
                  <img :src="_mission_stratagems[i - 1]?.icon" alt="">
                </div>
              </div>
              <!-- 기타(기본 보급): 가장 오른쪽 2칸 고정 -->
              <div class="default">
                <div class="stratagem" v-for="(stratagem, index) in _default_stratagems" :key="stratagem.name"
                  @click="_default_stratagems_hidden[index] = _default_stratagems_hidden[index] ? false : true"
                  :class="{ hidden: _default_stratagems_hidden[index] }"
                >
                  <img :src="stratagem.icon" alt="">
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="equipment">
          <h2 class="col-title">장비</h2>
          <div class="loadout">
            <!-- 방어구: 상단 전체 폭. 클릭 → 선택 모달 -->
            <div class="equip-slot armor" @click="f_open_equipment('armor')">
              <h3 class="slot-title" :title="_equipment.armor ? `방어구: ${_equipment.armor.name}` : '방어구'">
                방어구<span v-if="_equipment.armor" class="picked">: {{ _equipment.armor.name }}</span>
              </h3>
              <div class="slot-body">
                <img v-if="_equipment.armor" :src="f_equip_image('armor', _equipment.armor.name)" :alt="_equipment.armor.name">
                <span v-else class="plus">＋</span>
              </div>
            </div>
            <!-- 무기 3종: 1열로 나열. 각 슬롯 클릭 → 선택 모달 -->
            <div class="weapons">
              <div class="equip-slot" v-for="w in _equipment_weapons" :key="w.key" @click="f_open_equipment(w.key)">
                <h3 class="slot-title" :title="_equipment[w.key] ? `${w.kor}: ${_equipment[w.key].name}` : w.kor">
                  {{ w.kor }}<span v-if="_equipment[w.key]" class="picked">: {{ _equipment[w.key].name }}</span>
                </h3>
                <div class="slot-body">
                  <img v-if="_equipment[w.key]" :src="f_equip_image(w.key, _equipment[w.key].name)" :alt="_equipment[w.key].name">
                  <span v-else class="plus">＋</span>
                </div>
              </div>
            </div>
          </div>
        </div>
        <!-- 프리셋: 스트라타젬+장비 두 열을 가로질러 배치(장비도 저장 예정) -->
        <div class="presets">
          <div class="preset-head">
            <h2 class="title">프리셋</h2>
            <span class="hint">더블클릭: 이름변경 · 우클릭: 삭제 · 드래그: 순서변경</span>
          </div>
          <div class="preset-tabs">
            <div class="preset-tab" v-for="preset in _presets" :key="preset.id"
              :class="{ active: _selected_preset_id === preset.id }"
              draggable="true"
              @click="f_apply_preset(preset)"
              @dblclick="f_preset_start_rename(preset)"
              @contextmenu.prevent="f_delete_preset(preset)"
              @dragstart="f_preset_dragstart(preset)"
              @dragover.prevent="f_preset_dragover(preset)"
              @dragend="f_preset_dragend"
            >
              <input v-if="_editing_preset_id === preset.id"
                class="preset-rename"
                v-model="_editing_preset_name"
                :ref="el => el && el.focus()"
                @click.stop
                @keydown.enter="f_preset_confirm_rename(preset)"
                @keydown.esc="f_preset_cancel_rename"
                @blur="f_preset_confirm_rename(preset)"
              >
              <span v-else>{{ preset.name }}</span>
            </div>
            <div class="preset-tab add" @click="f_add_preset" title="현재 스트라타젬 구성으로 새 프리셋 만들기">＋</div>
          </div>
          <!-- 항상 표시(높이 고정). 선택된 프리셋이 없으면 비활성만. -->
          <button class="preset-save" :disabled="!_selected_preset_id" @click="f_save_selected_preset">현재 구성을 프리셋에 저장</button>
        </div>
      </div>
      <div class="settings">
        <h2 class="col-title">설정</h2>
        <div class="error" v-if="_steaminfo?.error">
          스팀의 게임 또는 계정 정보를 불러오는데 실패했습니다. 스팀 로그인을 확인하세요.
        </div>
        <div class="error" v-else-if="_steaminfo?.username &&!_steaminfo?.configInfo" @click="f_open_config_path"
          :style="{ cursor: 'pointer' }"
        >
          {{ _steaminfo?.username }} 계정의 {{ _steaminfo?.configPath }} 경로에 설정 파일이 없거나 문제가 있습니다.
        </div>
        <!-- <div class="username" v-else-if="_steaminfo?.username">{{ _steaminfo.username }}</div> -->
        <div class="options">
          <div class="section">
            <h3 class="title">게임 초대</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">게임 참여 링크 복사</div>
              </div>
              <button class="copy-button" @click="f_copy_game_invite" :disabled="_is_copying">
                {{ _is_copying ? '복사 중...' : '복사하기' }}
              </button>
            </div>
            <div v-if="_copy_result" class="copy-result" :class="{ error: _copy_result.error }">
              {{ _copy_result.error || '클립보드에 복사되었습니다.' }}
            </div>
          </div>
          <div class="section">
            <h3 class="title">단축키 설정</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">증원 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('reinforce', '증원 단축키')">{{ f_get_key_string(_bindkeys.reinforce) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">→ 방향 로테이션 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('rotatekey', '→ 방향 로테이션 단축키')">{{ f_get_key_string(_bindkeys.rotatekey) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">← 방향 로테이션 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('rotatekey_reverse', '← 방향 로테이션 단축키')">{{ f_get_key_string(_bindkeys.rotatekey_reverse) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">로테이션 취소 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('rotate_cancel', '로테이션 취소 단축키')">{{ f_get_key_string(_bindkeys.rotate_cancel) }}</div>
            </div>
          </div>
          <div class="section">
            <h3 class="title">스트라타젬 자동 장착</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">자동 장착 사용</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_autoselect_enabled"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">자동 장착 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('autoselect', '스트라타젬 자동 장착 단축키')">{{ f_get_key_string(_bindkeys.autoselect) || '미설정' }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">입력 딜레이 (ms, 30~100)</div>
              </div>
              <input class="input" type="number" min="30" max="100" v-model="_autoselect_input_delay"/>
            </div>
            <div class="option">
              <div class="description" :style="{ width: '100%' }">
                로드아웃(장비구성) 화면에서 스트라타젬 슬롯에 커서를 둔 뒤 단축키를 누르면 구성한 스트라타젬(앞 4개)을 자동 선택합니다.
                <b>미보유 스트라타젬은 좌측 목록에서 우클릭해 제외</b>해야 정확히 동작하며, 게임 텍스트 언어는 한국어 기준입니다.
              </div>
            </div>
          </div>
          <div class="section">
            <h3 class="title">성능 설정</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">스트라타젬 즉시 투척</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_stratagem_instant_fire"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">스트라타젬 투척 딜레이</div>
              </div>
              <input class="input" type="number" v-model="_stratagem_instant_fire_delay"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">스트라타젬 로테이션 선택 딜레이</div>
              </div>
              <input class="input" type="number" v-model="_rotate_delay"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">모든 동작 기본 딜레이</div>
              </div>
              <input class="input" type="number" v-model="_default_delay"/>
            </div>
          </div>
          <div class="section">
            <h3 class="title">한글 채팅창 설정</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">한글 채팅 입력 딜레이</div>
              </div>
              <input class="input" type="number" v-model="_chatinputdelay"/>
            </div>
            <!-- <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">한글 채팅 확장 창 좌측상단으로</div>
              </div>
              <div class="button" @click="f_chat_lefttop">초기화</div>
            </div> -->
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">채팅 입력 시 자동 한글 확장 켜기</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_instant_chat"/>
            </div>
            <div class="option" v-if="!_instant_chat">
              <div class="meta">
                <div class="deco"/>
                <div class="name">한글 채팅 확장 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('HANGUL', '한글 채팅 확장 단축키')">{{ f_get_key_string(_bindkeys.HANGUL) }}</div>
            </div>
          </div>
          <div class="section">
            <h3 class="title">시네마틱 모드 설정</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">시네마틱 모드 활성화</div>
              </div>
              <input type="checkbox" v-if="_keyBinds.cinematic_mode" class="checkbox" v-model="_cinematic_mode"/>
              <div class="description" v-else>HUD 켜기/끄기<br/>키설정 필요</div>
            </div>
          </div>
          <div class="section">
            <h3 class="title">스트라타젬 마우스 입력 설정</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">스트라타젬 마우스 입력 활성화</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_mousestratagem_enabled"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">입력 상태 활성화 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('mousestratagem', '스트라타젬 마우스 입력 상태 활성화 단축키')">{{ f_get_key_string(_bindkeys.mousestratagem) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">스트라타젬 콘솔 조작 시 자동 활성화</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_mousestratagem_with_console"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">스트라타젬 마우스 입력 임계 감도</div>
              </div>
              <input class="input" type="number" v-model="_mousestratagem_threshold"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">스트라타젬 마우스 입력간 딜레이</div>
              </div>
              <input class="input" type="number" v-model="_mousestratagem_delay"/>
            </div>
          </div>
          <div class="section">
            <h3 class="title">기계화 설정</h3>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">재장전 속도 증가 갑옷 착용 여부</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_autokey_with_goodarmor"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">기계화 전투 1번 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('autokey', '기계화 전투 단축키')">{{ f_get_key_string(_bindkeys.autokey) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">기계화 전투 1번</div>
              </div>
              <select class="select" v-model="_autokey_type">
                <option class="option" v-for="item in _autokey_type_map" :value="item.value">{{ item.name }}</option>
              </select>
            </div>
            <div class="option" v-if="(_autokey_type == 'eruptor') && !_keyBinds.weapon_swap">
              <div class="meta" :style="{ width: '100%', 'text-align': 'right' }">
                <div class="deco"/>
                <div class="description" :style="{ width: '100%', 'text-align': 'right' }">장비 교체(짧은무기) 키설정 필요</div>
              </div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">기계화 전투 2번 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('autokey_sub', '기계화 전투 단축키')">{{ f_get_key_string(_bindkeys.autokey_sub) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">기계화 전투 2번</div>
              </div>
              <select class="select" v-model="_autokey_type_sub">
                <option class="option" v-for="item in _autokey_type_map" :value="item.value">{{ item.name }}</option>
              </select>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">기계화 전투 3번 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('autokey_sub2', '기계화 전투 단축키')">{{ f_get_key_string(_bindkeys.autokey_sub2) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">기계화 전투 3번</div>
              </div>
              <select class="select" v-model="_autokey_type_sub2">
                <option class="option" v-for="item in _autokey_type_map" :value="item.value">{{ item.name }}</option>
              </select>
            </div>
            <div class="option" v-if="(_autokey_type_sub == 'eruptor') && !_keyBinds.weapon_swap">
              <div class="meta" :style="{ width: '100%', 'text-align': 'right' }">
                <div class="deco"/>
                <div class="description" :style="{ width: '100%', 'text-align': 'right' }">장비 교체(짧은무기) 키설정 필요</div>
              </div>
            </div>
            <div class="option" v-if="_autokey_type == 'heavy' || _autokey_type_sub == 'heavy' || _autokey_type_sub2 == 'heavy'">
              <div class="meta">
                <div class="deco"/>
                <div class="name">중기관총 반동 제어 감도</div>
              </div>
              <input class="input" type="number" v-model="_heavy_start_rate"/>
            </div>
            <div class="option" v-if="_autokey_type == 'heavy' || _autokey_type_sub == 'heavy' || _autokey_type_sub2 == 'heavy'">
              <div class="meta">
                <div class="deco"/>
                <div class="name">중기관총 사용 RPM</div>
              </div>
              <input class="input" type="number" v-model="_heavy_rpm"/>
            </div>
            <div class="option" v-if="_autokey_type == 'purifier' || _autokey_type_sub == 'purifier' || _autokey_type_sub2 == 'purifier'">
              <div class="meta">
                <div class="deco"/>
                <div class="name">퓨리파이어 반동 제어 감도</div>
              </div>
              <input class="input" type="number" v-model="_purifier_move_rate"/>
            </div>
            <div class="option" v-if="_autokey_type == 'apw' || _autokey_type_sub == 'apw' || _autokey_type_sub2 == 'apw'">
              <div class="meta">
                <div class="deco"/>
                <div class="name">대물소총 반동 제어 감도</div>
              </div>
              <input class="input" type="number" v-model="_apw_start_rate"/>
            </div>
          </div>
          <div class="section">
            <h3 class="title">플레이 자동 녹화</h3>
            <!-- <div class="option" v-if="_game_display.rotate">
              <div class="meta" :style="{ width: '100%', 'text-align': 'right' }">
                <div class="deco"/>
                <div class="description" :style="{ width: '100%', 'text-align': 'right' }">모니터가 회전 상태여서 녹화할 수 없습니다</div>
              </div>
            </div> -->
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">플레이 자동 녹화 활성화</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_autorecord"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">동영상 저장 위치 열기</div>
              </div>
              <div class="button" @click="f_open_video_folder">폴더 열기</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">저장된 동영상 전부 삭제</div>
              </div>
              <div class="button" :class="{ disabled: !_video_path_size }" @click="f_clear_video_folder">{{ _video_path_size ? f_format_size(_video_path_size) : '동영상 없음' }}</div>
            </div>
            <div class="option" v-if="_displaylength > 1">
              <div class="meta">
                <div class="deco"/>
                <div class="name">녹화 대상 모니터 번호</div>
              </div>
              <select class="select" v-model="_output_idx">
                <option class="option" v-for="item in _displaylength" :value="item - 1">{{ item - 1 }}</option>
              </select>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">자동 녹화 단축키</div>
              </div>
              <div class="shortcut" @click="f_set_key('record', '자동 녹화 단축키')">{{ f_get_key_string(_bindkeys.record) }}</div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">최대 자동 녹화 시간</div>
              </div>
              <div><input class="input" type="number" v-model="_record_duration" :min="_deathcam_seconds + _deathcam_delay + 1"/><span class="unit">초</span></div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">자동 녹화 초당 프레임</div>
              </div>
              <input class="input" type="number" v-model="_record_framerate"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">자동 녹화 품질</div>
              </div>
              <select class="select" v-model="_record_quality">
                <option class="option" v-for="item in _record_quality_map" :value="item.value">{{ item.name }}</option>
              </select>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">자동 데스캠 저장 활성화</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_deathcam_enabled"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">데스캠 녹화 시간</div>
              </div>
              <div><input class="input" type="number" v-model="_deathcam_seconds"/><span class="unit">초</span></div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">데스캠 녹화 딜레이</div>
              </div>
              <div><input class="input" type="number" v-model="_deathcam_delay"/><span class="unit">초</span></div>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">데스캠 미리보기 활성화</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_deathcam_preview"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">데스캠 미리보기 크기 배율</div>
              </div>
              <input type="number" class="input" v-model="_deathcam_size" :min="20" :max="200"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">데스캠 최대 저장 개수</div>
              </div>
              <input type="number" class="input" v-model="_deathcam_max_counts" :min="1"/>
            </div>
            <div class="option">
              <div class="meta">
                <div class="deco"/>
                <div class="name">데스캠 webp 변환 활성화</div>
              </div>
              <input type="checkbox" class="checkbox" v-model="_deathcam_webp"/>
            </div>
            <div class="option">
              <div class="meta" :style="{ width: '100%', 'text-align': 'right' }">
                <div class="deco"/>
                <div class="description" :style="{ width: '100%', 'text-align': 'right' }">webp 변환 시 20MB 이하의 용량이 됩니다</div>
              </div>
            </div>
          </div>
        </div>
        <div class="textbox">
          <div class="textboxtitle">
            <div class="textboxdeco"/>
            <div class="textboxname">선택적 복지 콘솔</div>
          </div>
          <div>당신은 장애를 갖고 있거나, 겁쟁이입니다. 하지만 이 새로운 기술을 사용하면 반드시 장애를 극복하고 두려움없는 헬다이버로 거듭날 수 있습니다.</div>
        </div>
      </div>
    </div>
    <div v-if="_key_modal" class="key_modal" @click.self="f_cancel_key">
      <div class="inner">
        <div class="title"><div class="deco"/>단축키 변경</div>
        <div class="name">{{ _key_modal }}</div>
        <div class="key">{{ f_get_key_string(_key_watching_key) }}</div>
        <div class="buttons">
          <div class="button cancel" @click="f_cancel_key">취소</div>
          <div class="button save" @click="f_save_key">저장</div>
        </div>
      </div>
    </div>

    <div class="update" v-if="c_newversion" @click="f_update_install">최신 업데이트가 다운로드 되었습니다! 클릭하여 업데이트 하세요</div>
    <div class="update" v-else-if="_progress">신규 업데이트를 다운로드 받고 있습니다. {{ _progress?.percent?.toFixed(0) || 0 }}%</div>

    <!-- 장비 선택 모달: 슬롯 클릭 시 해당 카테고리 카탈로그에서 고른다 -->
    <MainEquipmentpicker
      v-if="_equipment_modal"
      :slot-key="_equipment_modal"
      :selected="_equipment[_equipment_modal]"
      @select="f_select_equipment"
      @close="_equipment_modal = null"
    />
  </div>
</template>

<script setup>
const c_pagetype = computed(() => useRoute().query.type)

definePageMeta({
  layout: 'main'
})

const _newversion = ref(false)
const _updating = ref(false)
const _progress = ref()
ipcRenderer.on('checking-for-update', v => {
  console.log(v)
})
ipcRenderer.on('update-not-available', v => {
  console.log(v)
})
ipcRenderer.on('update-error', v => {
  console.log(v)
})
ipcRenderer.on('update-available', v => {
  console.log('update-available')
  _updating.value = true
})
ipcRenderer.on('download-progress', v => {
  _progress.value = v
})
ipcRenderer.on('update-downloaded', v => {
  _updating.value = false
  _newversion.value = true
})
const c_newversion = computed(() => {
  return !_updating.value && _newversion.value
})
const f_update_install = () => {
  ipcRenderer.send('update_install')
}
setInterval(async () => {
  try {
    await ipcRenderer.invoke('check_update')
  } catch (e) {}
}, 1000 * 60 * 1)

const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms))
const _process = ref(false)
onMounted(async () => {
  await ipcRenderer.invoke('check_update')
  await sleep(1000)
  _process.value = await ipcRenderer.invoke('loaded', 'main')
  if (!_process.value?.isDev) {
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
  // 마우스 버튼 클릭 시 기본 동작 차단
  window.onmousedown = e => {
    if (e.button === 3 || e.button === 4) { // 3: 뒤로가기 버튼, 4: 앞으로가기 버튼
      e.preventDefault()
    }
  }
  window.onkeydown = e => {
    if (e.key === ' ') { // 스페이스 키 막기
      e.preventDefault()
    }
    if (e.key?.startsWith('F') && e.key.length <= 3) { // F1 ~ F12 키 막기
      e.preventDefault()
    }
  }
})

const _keyBinds = ref({})
ipcRenderer.on('keyBinds', keyBinds => {
  _keyBinds.value = keyBinds
})


const _default_stratagems_hidden = ref([])
const _mission_stratagems = ref([
  {
    name: 'Hellbomb',
    keys: ['down', 'up', 'left', 'down', 'up', 'right', 'down', 'up'],
    icon: '/stratagems/General Stratagems/Hellbomb.svg',
    type: 'general',
    index: 2
  },
  {
    name: 'SEAF Artillery',
    keys: ['right', 'up', 'up', 'down'],
    icon: '/stratagems/General Stratagems/SEAF Artillery.svg',
    type: 'general',
    cooldown: 1000 * 5,
    takedown: 1000 * 10,
    index: 0
  }
])
const _default_stratagems = ref([
  {
    name: 'Eagle Rearm',
    keys: ['up', 'up', 'left', 'up', 'right'],
    icon: '/stratagems/Hangar/Eagle Rearm.svg',
    cooldown: 1000 * 120,
    takedown: 0,
    instant: true
  },
  {
    name: 'Resupply',
    keys: ['down', 'down', 'up', 'right'],
    icon: '/stratagems/General Stratagems/Resupply.svg',
    cooldown: 1000 * 160,
    takedown: 1000 * 15
  }
])
// 선택 표시 2행을 윗 행(6칸)과 동일하게: 임무 슬롯 수 = 6 - 기타(고정 보급) 개수.
// 임무가 비어 있어도 이 개수만큼 빈칸을 그려 위치/너비/간격을 윗 행과 맞춘다.
const c_mission_slot_count = computed(() => Math.max(0, 6 - _default_stratagems.value.length))

const _stratagems = STRATAGEMS
const c_stratagems = computed(() => {
  const stratagems = []
  for (const key in _stratagems) {
    stratagems.push(..._stratagems[key])
  }
  const res = {
    attack: [],
    supply: [],
    defense: [],
    general: []
  }
  for (const stratagem of stratagems) {
    const type = stratagem.type || 'undefined'
    if (!res[type]) continue
    res[type].push(stratagem)
  }
  for (const key in res) {
    res[key] = res[key].sort((a, b) => a.index - b.index)
  }
  return res
})

const _stratagemsets = ref({})
watch([_stratagemsets, _default_stratagems_hidden, _mission_stratagems], () => {
  const res = []
  for (const stratagem of Object.values(_stratagemsets.value)) {
    if (!stratagem) continue
    res.push({
      code: stratagem.code,
      name: stratagem.name,
      keys: [...stratagem.keys],
      icon: stratagem.icon,
      cooldown: stratagem.cooldown || 0,
      takedown: stratagem.takedown || 0
    })
  }
  for (const stratagem of _mission_stratagems.value) {
    res.push({
      ...stratagem,
      keys: [...stratagem.keys]
    })
  }
  for (const index in _default_stratagems.value) {
    if (_default_stratagems_hidden.value[index]) continue
    if (_default_stratagems.value[index].name == 'Eagle Rearm' && !res.find(e => e.name.includes('Eagle'))) continue
    res.push({
      ..._default_stratagems.value[index],
      keys: [..._default_stratagems.value[index].keys]
    })
  }
  if (res.length > 0) {
    res[0].rotatekey = 'T'
  }
  if (res.length > 1) {
    res[res.length - 1].rotatekey = 'H'
  }
  ipcRenderer.send('stratagemsets', res)
}, { deep: true })

const f_addstratagem = (stratagem) => {
  if (stratagem.type == 'general') {
    _mission_stratagems.value.push(stratagem)
    return
  }
  if (f_isSelected(stratagem)) return
  for (let i = 1; i <= 6; i++) {
    if (!_stratagemsets.value[i]) {
      _stratagemsets.value[i] = stratagem
      return
    }
  }
}
const f_removestratagem = (stratagem) => {
  const index = f_isSelected(stratagem)
  if (!index) return
  stratagem.type == 'general' ? _mission_stratagems.value.splice(index - 1, 1) : _stratagemsets.value[index] = null
}

const f_isSelected = stratagem => {
  if (!stratagem) return false
  if (stratagem.type == 'general') {
    const index = _mission_stratagems.value.findIndex(e => e.name == stratagem.name)
    if (index == -1) return false
    return index + 1
  }
  if (_stratagemsets.value[1]?.name == stratagem.name) return 1
  if (_stratagemsets.value[2]?.name == stratagem.name) return 2
  if (_stratagemsets.value[3]?.name == stratagem.name) return 3
  if (_stratagemsets.value[4]?.name == stratagem.name) return 4
  if (_stratagemsets.value[5]?.name == stratagem.name) return 5
  if (_stratagemsets.value[6]?.name == stratagem.name) return 6
  return false
}


/* ===== 미보유(자동 장착 제외) 스트라타젬 ===== */
// 인게임 격자에서 미보유 항목을 빼야 좌표가 일치하므로, 우클릭으로 제외 목록을 관리한다.
const _disabled_items = ref([])
const f_is_disabled = (stratagem) => !!stratagem && _disabled_items.value.includes(stratagem.name)
const f_toggle_disabled = (stratagem) => {
  if (!stratagem) return
  const i = _disabled_items.value.indexOf(stratagem.name)
  if (i === -1) _disabled_items.value.push(stratagem.name)
  else _disabled_items.value.splice(i, 1)
  ipcRenderer.send('disabled_items', JSON.parse(JSON.stringify(_disabled_items.value)))
}

/* ===== 스트라타젬 프리셋 ===== */
// 이름 -> 스트라타젬 객체 카탈로그 (프리셋은 이름만 저장하고 적용 시 여기서 재구성)
const _stratagem_catalog = {}
for (const key in _stratagems) {
  for (const stratagem of _stratagems[key]) {
    _stratagem_catalog[stratagem.name] = stratagem
  }
}

const _presets = ref([])
const _selected_preset_id = ref(null)
const _editing_preset_id = ref(null)
const _editing_preset_name = ref('')
const _dragged_preset_id = ref(null)

const f_save_presets = () => {
  ipcRenderer.send('presets', JSON.parse(JSON.stringify(_presets.value)))
}

// 현재 슬롯/미션 구성을 이름 목록으로 캡처
const f_capture_loadout = () => {
  const slots = {}
  for (let i = 1; i <= 6; i++) {
    slots[i] = _stratagemsets.value[i]?.name || null
  }
  const missions = _mission_stratagems.value.map(s => s.name)
  const equipment = {}
  for (const k of _equipment_keys) equipment[k] = _equipment.value[k]?.name || null
  return { slots, missions, equipment }
}

// 프리셋 적용: 이름을 카탈로그에서 재구성하여 현재 구성에 반영 (기존 watch가 오버레이로 전파)
const f_apply_preset = (preset) => {
  const newSlots = {}
  for (let i = 1; i <= 6; i++) {
    const name = preset.slots?.[i]
    newSlots[i] = name && _stratagem_catalog[name] ? _stratagem_catalog[name] : null
  }
  _stratagemsets.value = newSlots
  _mission_stratagems.value = (preset.missions || [])
    .map(name => _stratagem_catalog[name])
    .filter(Boolean)
  const eq = preset.equipment || {}
  const newEquip = {}
  for (const k of _equipment_keys) {
    const name = eq[k]
    newEquip[k] = name && _equipment_catalog[k]?.[name] ? _equipment_catalog[k][name] : null
  }
  _equipment.value = newEquip
  _selected_preset_id.value = preset.id
}

const f_get_next_preset_name = () => {
  const names = new Set(_presets.value.map(p => p.name))
  let n = 1
  while (names.has(`프리셋 ${n}`)) n++
  return `프리셋 ${n}`
}

const f_add_preset = () => {
  // 빈 프리셋으로 생성: 슬롯/미션/장비 모두 비어 있음. 생성 후 적용해 현재 화면도 비운다.
  const preset = {
    id: 'preset-' + Date.now(),
    name: f_get_next_preset_name(),
    slots: { 1: null, 2: null, 3: null, 4: null, 5: null, 6: null },
    missions: [],
    equipment: { armor: null, primary: null, secondary: null, throwable: null }
  }
  _presets.value.push(preset)
  f_apply_preset(preset)
  f_save_presets()
}

const f_save_selected_preset = () => {
  const preset = _presets.value.find(p => p.id === _selected_preset_id.value)
  if (!preset) return
  const { slots, missions, equipment } = f_capture_loadout()
  preset.slots = slots
  preset.missions = missions
  preset.equipment = equipment
  f_save_presets()
}

const f_delete_preset = (preset) => {
  if (!confirm(`'${preset.name}' 프리셋을 삭제할까요?`)) return
  _presets.value = _presets.value.filter(p => p.id !== preset.id)
  if (_selected_preset_id.value === preset.id) _selected_preset_id.value = null
  f_save_presets()
}

const f_preset_start_rename = (preset) => {
  _editing_preset_id.value = preset.id
  _editing_preset_name.value = preset.name
}
const f_preset_confirm_rename = (preset) => {
  if (_editing_preset_id.value !== preset.id) return
  const name = _editing_preset_name.value.trim()
  if (name) {
    preset.name = name
    f_save_presets()
  }
  _editing_preset_id.value = null
}
const f_preset_cancel_rename = () => {
  _editing_preset_id.value = null
}

const f_preset_dragstart = (preset) => {
  _dragged_preset_id.value = preset.id
}
const f_preset_dragover = (preset) => {
  const draggedId = _dragged_preset_id.value
  if (!draggedId || draggedId === preset.id) return
  const from = _presets.value.findIndex(p => p.id === draggedId)
  const to = _presets.value.findIndex(p => p.id === preset.id)
  if (from === -1 || to === -1) return
  const [moved] = _presets.value.splice(from, 1)
  _presets.value.splice(to, 0, moved)
}
const f_preset_dragend = () => {
  if (_dragged_preset_id.value) f_save_presets()
  _dragged_preset_id.value = null
}


const _i18n = ref('kor')
const _categories = {
  attack: {
    kor: '공격'
  },
  supply: {
    kor: '보급'
  },
  defense: {
    kor: '방어'
  },
  general: {
    kor: '임무'
  }
}

// 카테고리 가로 배치: 같은 배열(열)에 든 카테고리는 세로로 쌓인다.
// 장비 열을 추가하면서 스트라타젬을 2열로 압축: 1열=공격/방어/임무(세로로 쌓음), 2열=보급.
const _category_layout = [
  ['attack', 'defense', 'general'],
  ['supply']
]

// 장비(로드아웃) 무기 슬롯. 방어구는 상단 전체 폭, 무기 3종은 그 아래 1열.
const _equipment_weapons = [
  { key: 'primary', kor: '주 무기' },
  { key: 'secondary', kor: '보조 무기' },
  { key: 'throwable', kor: '투척 무기' }
]
// 선택된 장비(로드아웃). 슬롯 클릭 → 모달(MainEquipmentpicker)로 선택.
// 카탈로그/이미지는 utils/equipment.js + public/equipment/ (database.json 기반 생성).
// 프리셋에 이름으로 저장/복원된다(f_capture_loadout / f_apply_preset).
const _equipment = ref({ armor: null, primary: null, secondary: null, throwable: null })
const _equipment_keys = ['armor', 'primary', 'secondary', 'throwable']
// 이름 → 장비 아이템 역참조(프리셋 복원용). EQUIPMENT[slot] = [{ category, items: [{ name, ... }] }]
const _equipment_catalog = {}
for (const [slot, groups] of Object.entries(EQUIPMENT)) {
  const map = {}
  for (const g of groups) for (const it of g.items) map[it.name] = it
  _equipment_catalog[slot] = map
}
const _equipment_modal = ref(null) // 열린 슬롯 key(armor/primary/secondary/throwable) 또는 null
const f_open_equipment = (key) => { _equipment_modal.value = key }
const f_select_equipment = (item) => {
  if (_equipment_modal.value) _equipment.value[_equipment_modal.value] = item
  _equipment_modal.value = null
}
const f_equip_image = (key, name) => `/equipment/${key === 'armor' ? 'armors' : 'weapons'}/${name}.png`


const _stratagem_instant_fire = ref(true)
watch(_stratagem_instant_fire, () => {
  // localStorage.setItem('instantfire', _stratagem_instant_fire.value)
  ipcRenderer.send('instantfire', _stratagem_instant_fire.value)
})
ipcRenderer.on('instantfire', v => {
  _stratagem_instant_fire.value = v
})
const _stratagem_instant_fire_delay = ref(1000)
watch(_stratagem_instant_fire_delay, () => {
  // localStorage.setItem('instantfire_delay', _stratagem_instant_fire_delay.value)
  ipcRenderer.send('instantfire_delay', parseInt(_stratagem_instant_fire_delay.value || 0) || 0)
})
ipcRenderer.on('instantfire_delay', v => {
  _stratagem_instant_fire_delay.value = v
})
const _default_delay = ref(30)
watch(_default_delay, () => {
  // localStorage.setItem('inputDelay', _default_delay.value)
  ipcRenderer.send('inputDelay', parseInt(_default_delay.value || 0) || 0)
})
ipcRenderer.on('inputDelay', v => {
  _default_delay.value = v
})
const _chatinputdelay = ref(5)
watch(_chatinputdelay, () => {
  // localStorage.setItem('chatinputdelay', _chatinputdelay.value)
  ipcRenderer.send('chatinputdelay', parseInt(_chatinputdelay.value || 0) || 0)
})
ipcRenderer.on('chatinputdelay', v => {
  _chatinputdelay.value = v
})
const _rotate_delay = ref(300)
watch(_rotate_delay, () => {
  // localStorage.setItem('rotate_delay', _rotate_delay.value)
  ipcRenderer.send('rotate_delay', parseInt(_rotate_delay.value || 0) || 0)
})
ipcRenderer.on('rotate_delay', v => {
  _rotate_delay.value = v
})
const _instant_chat = ref(true)
watch(_instant_chat, () => {
  // localStorage.setItem('instant_chat', _instant_chat.value)
  ipcRenderer.send('instant_chat', _instant_chat.value)
})
ipcRenderer.on('instant_chat', v => {
  _instant_chat.value = v
})
const _cinematic_mode = ref(false)
watch(_cinematic_mode, () => {
  // localStorage.setItem('cinematic_mode', _cinematic_mode.value)
  ipcRenderer.send('cinematic_mode', _cinematic_mode.value)
})
ipcRenderer.on('cinematic_mode', v => {
  _cinematic_mode.value = v
})
const _autoselect_enabled = ref(true)
watch(_autoselect_enabled, () => {
  ipcRenderer.send('autoselect_enabled', _autoselect_enabled.value)
})
ipcRenderer.on('autoselect_enabled', v => {
  _autoselect_enabled.value = v
})
const _autoselect_input_delay = ref(30)
watch(_autoselect_input_delay, () => {
  ipcRenderer.send('autoselect_input_delay', parseInt(_autoselect_input_delay.value || 30) || 30)
})
ipcRenderer.on('autoselect_input_delay', v => {
  _autoselect_input_delay.value = v
})

const _autokey_enabled = ref(false)
watch(_autokey_enabled, () => {
  ipcRenderer.send('autokey_enabled', _autokey_enabled.value)
})
ipcRenderer.on('autokey_enabled', v => {
  _autokey_enabled.value = v
})
const _autokey_with_goodarmor = ref(false)
watch(_autokey_with_goodarmor, () => {
  ipcRenderer.send('autokey_with_goodarmor', _autokey_with_goodarmor.value)
})
ipcRenderer.on('autokey_with_goodarmor', v => {
  _autokey_with_goodarmor.value = v
})
const _autokey_type_map = [
  { name: '기계화 전투 미사용', value: '' },
  // { name: '이럽터 연사 보조', value: 'eruptor' },
  { name: '폭발 석궁 연사 보조', value: 'crossbow' },
  { name: '퍼니셔 플라스마 연사 보조', value: 'crossbow2' },
  { name: '유탄 발사기 연사 보조', value: 'crossbow3' },
  { name: '퓨리파이어 연사 제어 보조', value: 'purifier' },
  { name: '퓨리파이어 충전사격 보조', value: 'purifier_charge' },
  { name: '레일건 자동 조작 보조', value: 'railgun' },
  { name: '에포크 자동 조작 보조', value: 'epoch' },
  { name: '아크 발사기 자동 조작 보조', value: 'arc' },
  { name: '중기관총 반동 제어 보조', value: 'heavy' },
  { name: '대물소총 연발 사격', value: 'apw' }
]
const _autokey_type = ref('')
watch(_autokey_type, () => {
  ipcRenderer.send('autokey_type', _autokey_type.value)
})
ipcRenderer.on('autokey_type', v => {
  _autokey_type.value = v
})
const _autokey_type_sub = ref('')
watch(_autokey_type_sub, () => {
  ipcRenderer.send('autokey_type_sub', _autokey_type_sub.value)
})
ipcRenderer.on('autokey_type_sub', v => {
  _autokey_type_sub.value = v
})
const _autokey_type_sub2 = ref('')
watch(_autokey_type_sub2, () => {
  ipcRenderer.send('autokey_type_sub2', _autokey_type_sub2.value)
})
ipcRenderer.on('autokey_type_sub2', v => {
  _autokey_type_sub2.value = v
})

const _auto_arc_delay = ref(1000)
watch(_auto_arc_delay, () => {
  ipcRenderer.send('auto_arc_delay', _auto_arc_delay.value)
})
ipcRenderer.on('auto_arc_delay', v => {
  _auto_arc_delay.value = v
})

const _auto_epoch_delay = ref(2900)
watch(_auto_epoch_delay, () => {
  ipcRenderer.send('auto_epoch_delay', _auto_epoch_delay.value)
})
ipcRenderer.on('auto_railgun_delay', v => {
  _auto_railgun_delay.value = v
})
const _auto_epoch_reload_delay = ref(4200)
watch(_auto_epoch_reload_delay, () => {
  ipcRenderer.send('auto_epoch_reload_delay', _auto_epoch_reload_delay.value)
})
ipcRenderer.on('auto_epoch_reload_delay', v => {
  _auto_epoch_reload_delay.value = v
})

const _auto_railgun_delay = ref(2900)
watch(_auto_railgun_delay, () => {
  ipcRenderer.send('auto_railgun_delay', _auto_railgun_delay.value)
})
ipcRenderer.on('auto_railgun_delay', v => {
  _auto_railgun_delay.value = v
})
const _auto_railgun_reload_delay = ref(1000)
watch(_auto_railgun_reload_delay, () => {
  ipcRenderer.send('auto_railgun_reload_delay', _auto_railgun_reload_delay.value)
})
ipcRenderer.on('auto_railgun_reload_delay', v => {
  _auto_railgun_reload_delay.value = v
})
const _auto_eruptor_delay = ref(400)
watch(_auto_eruptor_delay, () => {
  ipcRenderer.send('auto_eruptor_delay', _auto_eruptor_delay.value)
})
ipcRenderer.on('auto_eruptor_delay', v => {
  _auto_eruptor_delay.value = v
})
const _auto_eruptor_reload_delay = ref(2800)
watch(_auto_eruptor_reload_delay, () => {
  ipcRenderer.send('auto_eruptor_reload_delay', _auto_eruptor_reload_delay.value)
})
ipcRenderer.on('auto_eruptor_reload_delay', v => {
  _auto_eruptor_reload_delay.value = v
})
const _auto_crossbow_reload_delay = ref(3300)
watch(_auto_crossbow_reload_delay, () => {
  ipcRenderer.send('auto_crossbow_reload_delay', _auto_crossbow_reload_delay.value)
})
ipcRenderer.on('auto_crossbow_reload_delay', v => {
  _auto_crossbow_reload_delay.value = v
})
const _auto_purifier_reload_delay = ref(2000)
watch(_auto_purifier_reload_delay, () => {
  ipcRenderer.send('auto_purifier_reload_delay', _auto_purifier_reload_delay.value)
})
ipcRenderer.on('auto_purifier_reload_delay', v => {
  _auto_purifier_reload_delay.value = v
})
const _apw_start_rate = ref(240)
watch(_apw_start_rate, () => {
  ipcRenderer.send('apw_start_rate', _apw_start_rate.value)
})
ipcRenderer.on('apw_start_rate', v => {
  _apw_start_rate.value = v
})
const _heavy_rpm = ref(750)
watch(_heavy_rpm, () => {
  ipcRenderer.send('heavy_rpm', _heavy_rpm.value)
})
ipcRenderer.on('heavy_rpm', v => {
  _heavy_rpm.value = v
})
const _heavy_start_rate = ref(240)
watch(_heavy_start_rate, () => {
  ipcRenderer.send('heavy_start_rate', _heavy_start_rate.value)
})
ipcRenderer.on('heavy_start_rate', v => {
  _heavy_start_rate.value = v
})
const _purifier_move_rate = ref(10)
watch(_purifier_move_rate, () => {
  ipcRenderer.send('purifier_move_rate', _purifier_move_rate.value)
})
ipcRenderer.on('purifier_move_rate', v => {
  _purifier_move_rate.value = v
})
const _mousestratagem_enabled = ref(false)
watch(_mousestratagem_enabled, () => {
  ipcRenderer.send('mousestratagem_enabled', _mousestratagem_enabled.value)
})
ipcRenderer.on('mousestratagem_enabled', v => {
  _mousestratagem_enabled.value = v
})
const _mousestratagem_with_console = ref(false)
watch(_mousestratagem_with_console, () => {
  ipcRenderer.send('mousestratagem_with_console', _mousestratagem_with_console.value)
})
ipcRenderer.on('mousestratagem_with_console', v => {
  _mousestratagem_with_console.value = v
})
const _mousestratagem_threshold = ref(50)
watch(_mousestratagem_threshold, () => {
  ipcRenderer.send('mousestratagem_threshold', _mousestratagem_threshold.value)
})
ipcRenderer.on('mousestratagem_threshold', v => {
  _mousestratagem_threshold.value = v
})
const _mousestratagem_delay = ref(100)
watch(_mousestratagem_delay, () => {
  ipcRenderer.send('mousestratagem_delay', _mousestratagem_delay.value)
})
ipcRenderer.on('mousestratagem_delay', v => {
  _mousestratagem_delay.value = v
})
const _autorecord = ref(true)
watch(_autorecord, () => {
  ipcRenderer.send('autorecord', _autorecord.value)
})
ipcRenderer.on('autorecord', v => {
  _autorecord.value = v
})
const _record_duration = ref(30)
watch(_record_duration, () => {
  ipcRenderer.send('record_duration', _record_duration.value)
})
ipcRenderer.on('record_duration', v => {
  _record_duration.value = v
})
const _record_framerate = ref(60)
watch(_record_framerate, () => {
  ipcRenderer.send('record_framerate', _record_framerate.value)
})
ipcRenderer.on('record_framerate', v => {
  _record_framerate.value = v
})
const _record_quality = ref(30)
const _record_quality_map = [
  { name: '원본', value: 5 },
  { name: '고화질', value: 19 },
  { name: '균형', value: 30 },
  { name: '웹배포용', value: 40 },
  { name: '저화질', value: 50 }
]
watch(_record_quality, () => {
  ipcRenderer.send('record_quality', _record_quality.value)
})
ipcRenderer.on('record_quality', v => {
  _record_quality.value = v
})
const _deathcam_enabled = ref(true)
watch(_deathcam_enabled, () => {
  ipcRenderer.send('deathcam_enabled', _deathcam_enabled.value)
})
ipcRenderer.on('deathcam_enabled', v => {
  _deathcam_enabled.value = v
})
const _deathcam_seconds = ref(5)
watch(_deathcam_seconds, () => {
  ipcRenderer.send('deathcam_seconds', _deathcam_seconds.value)
})
ipcRenderer.on('deathcam_seconds', v => {
  _deathcam_seconds.value = v
})
const _deathcam_delay = ref(2)
watch(_deathcam_delay, () => {
  ipcRenderer.send('deathcam_delay', _deathcam_delay.value)
})
ipcRenderer.on('deathcam_delay', v => {
  _deathcam_delay.value = v
})
const _deathcam_preview = ref(true)
watch(_deathcam_preview, () => {
  ipcRenderer.send('deathcam_preview', _deathcam_preview.value)
})
ipcRenderer.on('deathcam_preview', v => {
  _deathcam_preview.value = v
})
const _deathcam_size = ref(100)
watch(_deathcam_size, () => {
  ipcRenderer.send('deathcam_size', _deathcam_size.value)
})
ipcRenderer.on('deathcam_size', v => {
  _deathcam_size.value = v
})
const _deathcam_max_counts = ref(10)
watch(_deathcam_max_counts, () => {
  ipcRenderer.send('deathcam_max_counts', _deathcam_max_counts.value)
})
ipcRenderer.on('deathcam_max_counts', v => {
  _deathcam_max_counts.value = v
})
const _deathcam_webp = ref(false)
watch(_deathcam_webp, () => {
  ipcRenderer.send('deathcam_webp', _deathcam_webp.value)
})
ipcRenderer.on('deathcam_webp', v => {
  _deathcam_webp.value = v
})
const _output_idx = ref(0)
watch(_output_idx, () => {
  ipcRenderer.send('output_idx', _output_idx.value)
})
ipcRenderer.on('output_idx', v => {
  _output_idx.value = v
})
const _displaylength = ref(1)
ipcRenderer.on('displaylength', v => {
  _displaylength.value = v
})

const _bindkeys = ref({
  reinforce: 'OEM_3',

  rotatekey: 'T',
  rotatekey_reverse: 'H',
  rotate_cancel: 'RBUTTON',
  HANGUL: 'HANGUL',
  mousestratagem: 'SPACE',
  autokey: 'XBUTTON1',
  autokey_sub: 'XBUTTON2',
  autokey_sub2: 'MBUTTON',
  record: 'F1',
  autoselect: null,
})

ipcRenderer.on('initSettings', v => {
  _stratagem_instant_fire.value = v.instantfire
  _stratagem_instant_fire_delay.value = v.instantfire_delay
  _default_delay.value = v.inputDelay
  _chatinputdelay.value = v.chatinputdelay
  _rotate_delay.value = v.rotate_delay
  _instant_chat.value = v.instant_chat
  _cinematic_mode.value = v.cinematic_mode
  _autokey_enabled.value = v.autokey_enabled
  _autokey_type.value = v.autokey_type
  _autokey_type_sub.value = v.autokey_type_sub
  _autokey_type_sub2.value = v.autokey_type_sub2
  _autokey_with_goodarmor.value = v.autokey_with_goodarmor
  _auto_arc_delay.value = v.auto_arc_delay
  _auto_railgun_delay.value = v.auto_railgun_delay
  _auto_railgun_reload_delay.value = v.auto_railgun_reload_delay
  _auto_eruptor_delay.value = v.auto_eruptor_delay
  _apw_start_rate.value = v.apw_start_rate
  _heavy_rpm.value = v.heavy_rpm
  _heavy_start_rate.value = v.heavy_start_rate
  _purifier_move_rate.value = v.purifier_move_rate
  _mousestratagem_enabled.value = v.mousestratagem_enabled
  _mousestratagem_with_console.value = v.mousestratagem_with_console
  _mousestratagem_threshold.value = v.mousestratagem_threshold
  _mousestratagem_delay.value = v.mousestratagem_delay
  _autorecord.value = v.autorecord
  _record_duration.value = v.record_duration
  _record_framerate.value = v.record_framerate
  _record_quality.value = v.record_quality
  _deathcam_enabled.value = v.deathcam_enabled
  _deathcam_seconds.value = v.deathcam_seconds
  _deathcam_max_counts.value = v.deathcam_max_counts
  _deathcam_delay.value = v.deathcam_delay
  _deathcam_preview.value = v.deathcam_preview
  _deathcam_size.value = v.deathcam_size
  _deathcam_webp.value = v.deathcam_webp
  _output_idx.value = v.output_idx
  _displaylength.value = v.displaylength
  if (Array.isArray(v.presets)) _presets.value = v.presets
  if (Array.isArray(v.disabledItems)) _disabled_items.value = v.disabledItems
  if (v.autoselect_enabled !== undefined) _autoselect_enabled.value = v.autoselect_enabled
  if (v.autoselect_input_delay !== undefined) _autoselect_input_delay.value = v.autoselect_input_delay
  _bindkeys.value = v.keyBinds
})

// onMounted(() => {
//   _stratagem_instant_fire.value = localStorage.getItem('instantfire') ? localStorage.getItem('instantfire') == 'true' : true
//   _stratagem_instant_fire_delay.value = localStorage.getItem('instantfire_delay') ? parseInt(localStorage.getItem('instantfire_delay')) : 1000
//   _default_delay.value = localStorage.getItem('inputDelay') ? parseInt(localStorage.getItem('inputDelay')) : 20
//   _cinematic_mode.value = localStorage.getItem('cinematic_mode') ? localStorage.getItem('cinematic_mode') == 'true' : false
// })

const f_chat_lefttop = () => {
  ipcRenderer.send('chat_lefttop')
}

const _steaminfo = ref({})
ipcRenderer.on('steaminfo', v => {
  _steaminfo.value = v
})

const f_open_config_path = () => {
  ipcRenderer.send('open_config_path')
}

const _key_modal = ref()
const _key_watching = ref()
const _key_watching_key = ref()
const f_set_key = (key, name) => {
  _key_watching.value = key
  ipcRenderer.send('key_watching', key)
  _key_modal.value = name
  _key_watching_key.value = _bindkeys.value[key]
}
ipcRenderer.on('key_watching', v => {
  _key_watching_key.value = v
})
const f_save_key = () => {
  _bindkeys.value[_key_watching.value] = _key_watching_key.value
  if (_key_watching_key.value) ipcRenderer.send('save_bindkeys', { target: _key_watching.value, key: _key_watching_key.value })
  _key_watching.value = null
  _key_watching_key.value = null
  _key_modal.value = null
}
const f_cancel_key = () => {
  _key_modal.value = null
  _key_watching.value = null
  _key_watching_key.value = null
  ipcRenderer.send('cancel_key', true)
}

const f_get_key_string = (key) => {
  const key_string_map = {
    'RBUTTON': '마우스 우클릭',
    'HANGUL': '한/영',
    'SPACE': '스페이스바',
    'MBUTTON': '마우스휠클릭',
    'XBUTTON1': '마우스버튼1',
    'XBUTTON2': '마우스버튼2',
    'XBUTTON3': '마우스버튼3',
    'XBUTTON4': '마우스버튼4',
    'XBUTTON5': '마우스버튼5',
    'XBUTTON6': '마우스버튼6',
    'XBUTTON7': '마우스버튼7',
    'XBUTTON8': '마우스버튼8',
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

const f_open_video_folder = () => {
  ipcRenderer.send('open_video_folder')
}
const f_clear_video_folder = () => {
  ipcRenderer.send('clear_video_folder')
}

const _video_path_size = ref(0)
ipcRenderer.on('video_path_size', v => {
  _video_path_size.value = v
})

const f_format_size = (size) => {
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let index = 0
  while (size >= 1024 && index < units.length - 1) {
    size /= 1024
    index++
  }
  return `${size.toFixed(2)} ${units[index]}`
}


const _game_display = ref({})
ipcRenderer.on('game_display', v => {
  _game_display.value = v
})

const _copy_result = ref(null)
const _is_copying = ref(false)
const f_copy_game_invite = () => {
  _is_copying.value = true
  ipcRenderer.send('copy-game-invite')
}

ipcRenderer.on('copy-game-invite-result', (result) => {
  _copy_result.value = result
  _is_copying.value = false
  setTimeout(() => {
    _copy_result.value = null
  }, 10000)
})


/* ===== 창 크기 자동 맞춤 ===== */
// 가로: 콘텐츠(.page) 실제 폭에 맞춤 → 좌우 빈 공간 제거.
// 세로: 왼쪽 레이아웃(.console)의 자연 높이 기준 → 오른쪽이 더 길면 오른쪽 독립 스크롤이 처리.
// 측정값은 창 크기와 무관(scrollHeight=콘텐츠 자연높이, .page=shrink-to-fit)하므로 피드백 루프가 없다.
let _fitObserver = null
let _fitScheduled = false
let _lastFitW = 0
let _lastFitH = 0
// .console 의 "콘텐츠" 자연 높이를 측정한다.
// scrollHeight 는 콘텐츠가 박스보다 작을 때 clientHeight(=박스 높이)와 같아져서
// 창을 키우면 같이 커진다 → 그러면 최소크기가 부풀어 다시 못 줄인다.
// 대신 자식 요소들의 실제 범위(top~bottom)를 재면 창 크기와 무관하게 안정적이다.
const f_content_height = (el) => {
  let top = Infinity, bottom = -Infinity
  for (const kid of el.children) {
    const r = kid.getBoundingClientRect()
    if (!r.height) continue
    if (r.top < top) top = r.top
    if (r.bottom > bottom) bottom = r.bottom
  }
  return bottom > top ? (bottom - top) : el.scrollHeight
}
const f_fit_window = () => {
  const pageEl = document.querySelector('._main > .page')
  const leftEl = document.querySelector('.loadout-area')
  if (!pageEl || !leftEl) return
  const width = Math.ceil(pageEl.getBoundingClientRect().width) + 40        // ._main 좌우 패딩(20+20)
  const height = Math.ceil(f_content_height(leftEl)) + 60 + 8               // 타이틀바40 + ._main 하단패딩20 + 여유8 (상단 네비 헤더 제거됨)
  if (Math.abs(width - _lastFitW) <= 2 && Math.abs(height - _lastFitH) <= 2) return  // 떨림 방지
  _lastFitW = width
  _lastFitH = height
  ipcRenderer.send('fit-window', { width, height })
}
const f_schedule_fit = () => {
  if (_fitScheduled) return
  _fitScheduled = true
  requestAnimationFrame(() => {
    _fitScheduled = false
    f_fit_window()
  })
}
// 콘텐츠 변경(프리셋/미션 슬롯/언어)으로 왼쪽 높이가 바뀌면 다시 측정
watch([_presets, _mission_stratagems, _i18n], () => nextTick(f_schedule_fit), { deep: true })
onMounted(() => {
  nextTick(f_fit_window)
  if (typeof ResizeObserver !== 'undefined') {
    _fitObserver = new ResizeObserver(f_schedule_fit)
    const pageEl = document.querySelector('._main > .page')
    const leftEl = document.querySelector('.loadout-area')
    if (pageEl) _fitObserver.observe(pageEl)
    if (leftEl) _fitObserver.observe(leftEl)
  }
})
onBeforeUnmount(() => {
  if (_fitObserver) { _fitObserver.disconnect(); _fitObserver = null }
})
</script>

<style lang="scss" scoped>

._main {
  color: white;
  display: flex;
  height: calc(100% - 80px);
  &.navless {
    // 상단 네비(전투 보조 헤더) 제거 시 네비 높이(80px) 차감 불필요.
    height: 100%;
  }
  padding: 20px;
  padding-top: 0;
  justify-content: space-around;
  box-sizing: border-box;
  .page {
    display: flex;
    justify-content: space-around;
  }
  // 스트라타젬(console) + 장비(equipment)를 2열로, 프리셋은 두 열을 가로질러(2행) 배치.
  .loadout-area {
    display: grid;
    grid-template-columns: auto auto;
    column-gap: 20px;
    // 그리드를 콘텐츠 높이로 고정: 부모(._main) 높이만큼 늘어나며 행이 과도하게
    // stretch되어 방어구가 끝없이 커지는 것을 방지.
    align-self: flex-start;
    // align-items 기본값(stretch): console·equipment가 같은 행(=스트라타젬 콘텐츠) 높이로
    // 늘어나, 장비 열 방어구가 그만큼만 커지며 투척무기 하단이 스트라타젬 2행 하단과 정렬된다.
  }
  // 각 열(스트라타젬/장비/설정) 상단 제목 — 제거한 상단 헤더와 동일한 28px.
  .col-title {
    align-self: flex-start;
    font-size: 28px;
    margin: 0 0 10px;
  }
  img {
    -webkit-user-drag: none;
    user-drag: none;
  }
  .console {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: flex-start;
    min-width: 0;
    box-sizing: border-box;
    .categories {
      display: flex;
      align-items: flex-start;
      // 스트라타젬 목록은 약 1/2 높이로 제한 + 내부 스크롤(창이 과하게 길어지는 것 방지).
      max-height: 510px;
      overflow-y: auto;
      overflow-x: hidden;
      padding-right: 6px;
      &::-webkit-scrollbar {
        width: 6px;
        height: 6px;
      }
      &::-webkit-scrollbar-thumb {
        background: rgba(255, 255, 255, .25);
      }
      .category-col {
        display: flex;
        flex-direction: column;
      }
      .category {
        margin: 10px;
        margin-top: 0;
        .title {
          margin: 0;
        }
        .stratagems {
          display: flex;
          flex-wrap: wrap;
          width: 280px;
          .stratagem {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            border: 3px solid rgba(255, 255, 255, .3);
            margin: 5px;
            cursor: pointer;
            img {
              height: 54px;
            }
            &.selected {
              border-color: rgb(255, 232, 0);
              background: rgba(0, 0, 0, .8);
            }
            &.disabled {
              border-style: dashed;
              border-color: rgba(255, 80, 80, .6);
              img {
                opacity: .25;
                filter: grayscale(1);
              }
            }
          }
          // 보급(supply)도 기본 폭(280px=4열) 사용. 장비 열 추가로 6열 → 4열로 축소.
        }
      }
    }
    .stratagemsets {
      display: flex;
      flex-direction: column;
      .set-row {
        display: flex;
      }
      // 2행(임무/기타)도 1행과 동일한 6열 그리드로 정렬: 그룹 간 추가 간격 없이
      // 슬롯 margin(5px)만으로 윗 행과 열 간격·위치를 맞춘다.
      .set-row.sub {
        margin-top: 4px;
      }
      .stratagem {
        display: flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        border: 3px solid rgba(255, 255, 255, .3);
        height: 85px;
        width: 85px;
        margin: 5px;
        img {
          height: 100%;
        }
        .name {
          position: absolute;
        }
        &.selected {
          border-color: rgb(255, 232, 0);
          background: rgba(0, 0, 0, .8);
        }
      }
      .mission {
        display: flex;
      }
      .default {
        display: flex;
        .hidden {
          opacity: .5;
        }
      }
    }
  }
  // 프리셋: console(스트라타젬)와 equipment(장비) 두 열을 가로질러 배치. (장비도 저장 예정)
  .presets {
    grid-column: 1 / -1;
    width: 100%;
    margin-top: 16px;
    box-sizing: border-box;
    padding: 0 5px;
    .preset-head {
      display: flex;
      align-items: baseline;
      gap: 10px;
      .title {
        margin: 0;
      }
      .hint {
        font-size: 11px;
        opacity: .4;
        font-weight: 300;
      }
    }
    .preset-tabs {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-top: 8px;
      .preset-tab {
        display: flex;
        align-items: center;
        justify-content: center;
        min-width: 70px;
        height: 32px;
        padding: 0 12px;
        box-sizing: border-box;
        border: 2px solid rgba(255, 255, 255, .3);
        background: rgba(0, 0, 0, .3);
        cursor: pointer;
        font-size: 13px;
        user-select: none;
        &.active {
          border-color: rgb(255, 232, 0);
          background: rgba(0, 0, 0, .8);
        }
        &.add {
          min-width: 32px;
          font-size: 18px;
          opacity: .7;
          &:hover {
            opacity: 1;
          }
        }
        .preset-rename {
          width: 80px;
          background: transparent;
          border: none;
          outline: none;
          color: white;
          font-size: 13px;
          text-align: center;
        }
      }
    }
    .preset-save {
      margin-top: 8px;
      height: 30px;
      padding: 0 14px;
      border: 2px solid rgba(255, 232, 0, .6);
      background: rgba(0, 0, 0, .3);
      color: white;
      cursor: pointer;
      font-size: 12px;
      &:not(:disabled):hover {
        background: rgba(0, 0, 0, .8);
      }
      &:disabled {
        border-color: rgba(255, 255, 255, .2);
        color: rgba(255, 255, 255, .35);
        cursor: default;
      }
    }
  }
  .equipment {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    .loadout {
      display: flex;
      flex-direction: column;
      gap: 10px;
      width: 270px;
      // 장비 열이 행 높이만큼 늘어난 만큼 로드아웃도 채운다(방어구 stretch용).
      flex: 1;
    }
    .weapons {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .equip-slot {
      box-sizing: border-box;
      border: 3px solid rgba(255, 255, 255, .3);
      background: rgba(0, 0, 0, .3);
      display: flex;
      flex-direction: column;
      cursor: pointer;
      &:hover {
        border-color: rgba(255, 232, 0, .6);
      }
      .slot-title {
        margin: 0;
        padding: 5px 8px;
        font-size: 13px;
        font-weight: 400;
        background: rgba(0, 0, 0, .4);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        // 선택된 장비 이름 강조(카테고리는 흰색 유지)
        .picked {
          color: rgb(255, 232, 0);
        }
      }
      // 칸 높이는 칸이 결정하고, 이미지는 그 안에 맞춰 축소(이미지가 칸 높이를 늘리지 않게).
      .slot-body {
        height: 90px;          // 무기 슬롯: 고정 높이
        display: flex;
        align-items: center;
        justify-content: center;
        overflow: hidden;
        min-height: 0;
        .plus {
          font-size: 30px;
          opacity: .4;
        }
        img {
          max-width: 100%;
          max-height: 100%;
          object-fit: contain;
        }
      }
      // 방어구는 남는 세로 공간을 채워, 투척무기 하단이 스트라타젬 2행 하단과 정렬되게 한다.
      // slot-body 도 그 높이를 채우되 이미지는 칸 안에 맞춰 축소.
      &.armor {
        flex: 1;
        .slot-body {
          height: auto;
          flex: 1;
          min-height: 120px;
        }
      }
    }
  }
  .settings {
    position: relative;
    // 내용(가장 넓은 .textbox 330px / .section 280px) 너비에 맞춰 자동 축소.
    // 기존 width:100% + max-width:900px 는 1열일 때 내용보다 패널이 과하게 넓어짐.
    width: fit-content;
    flex: none;
    padding: 20px 0;
    box-sizing: border-box;
    margin-left: 20px;
    font-weight: 300;
    word-break: keep-all;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: space-between;
    .error {
      word-break: break-all;
      width: 280px;
      box-sizing: border-box;
      padding: 10px;
      background: rgb(255, 232, 0);
      color: black;
      font-weight: 500;
      padding-right: 20px;
    }
    .username {
      opacity: .3;
      font-size: 13px;
      position: absolute;
      right: 20px;
      top: 0px;
    }
    .options {
      width: 100%;
      flex: 1;
      min-height: 0;
      display: flex;
      flex-direction: column;
      flex-wrap: nowrap;
      justify-content: flex-start;
      align-items: center;
      overflow-y: auto;
      &::-webkit-scrollbar {
        width: 6px;
        height: 6px;
      }
      .section {
        margin: 20px;
        width: 280px;
        .title {
          width: 100%;
          margin: 0;
        }
        .option {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin: 10px 0;
          padding: 3px;
          background: rgba(0, 0, 0, .5);
          .meta {
            display: flex;
            text-align: left;
            align-items: center;
            margin-right: 10px;
            flex-shrink: 0;
            .deco {
              width: 3px;
              height: 15px;
              background: rgb(255, 232, 0);
              margin-right: 5px;
            }
            .name {
              font-weight: 400;
              font-size: 14px;
              word-break: keep-all;
            }
          }
          .description {
            font-size: 14px;
            font-weight: 300;
            color: rgb(150, 150, 150);
            text-align: right;
            word-break: keep-all;
            min-width: 50px;
            width: 100%;
          }
          .shortcut {
            font-size: 14px;
            font-weight: 300;
            color: rgb(150, 150, 150);
            text-align: right;
            word-break: keep-all;
            min-width: 50px;
            width: 100%;
            cursor: pointer;
            height: 20px;
          }
          .input {
            width: 50px;
            text-align: right;
            background: transparent;
            border: none;
            outline: none;
            color: rgb(150, 150, 150);
            &:focus {
              border-bottom: 1px solid rgb(150, 150, 150);
            }
          }
          .unit {
            font-size: 14px;
            color: rgb(150, 150, 150);
          }
          .checkbox {
            &[type="checkbox"] {
                appearance: none; /* 기본(네이티브) 모양을 제거 */
                box-sizing: border-box;
                background-clip: content-box;
                padding: 2px;
                width: 17px;
                height: 17px;
                border: 1.5px solid rgb(150, 150, 150);
                cursor: pointer;
            }
            &[type="checkbox"]:checked {
                border-color: rgb(255, 232, 0);
                background-color: rgb(255, 232, 0);
            }
          }
          .select {
            border: none;
            border-radius: 0;
            background-color: transparent;
            color: rgb(150, 150, 150);
            text-align: right;
            &:focus {
              outline: none;
              box-shadow: none;
            }
            option {
              background: black;
            }
          }
          .radio {
            &[type="radio"] {
              appearance: none;
              width: 17px;
              height: 17px;
              border: 1.5px solid rgb(150, 150, 150);
              border-radius: 50%;
              cursor: pointer;
              position: relative;
            }
            
            &[type="radio"]:checked {
              border-color: rgb(255, 232, 0);
              background-color: transparent;
              
              &::after {
                content: '';
                position: absolute;
                width: 9px;
                height: 9px;
                border-radius: 50%;
                background-color: rgb(255, 232, 0);
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
              }
            }
          }
          .button {
            background: rgb(255, 232, 0);
            padding: 3px 6px;
            font-size: 14px;
            color: black;
          font-weight: 500;
            cursor: pointer;
            &.disabled {
              pointer-events: none;
              color: rgb(100, 100, 100);
              background: rgba(100, 100, 100, .2);
            }
          }
        }
      }
    }
    .textbox {
      margin-top: 20px;
      flex-shrink: 0;
      width: 330px;
      border: 3px solid rgba(255, 255, 255, .3);
      padding: 10px;
      padding-right: 20px;
      box-sizing: border-box;
      font-size: 13px;
      .textboxtitle {
        margin-top: -3px;
        display: flex;
        align-items: center;
        margin-bottom: 4px;
        .textboxdeco {
          width: 3px;
          height: 15px;
          background: rgb(255, 232, 0);
          margin-right: 5px;
        }
        .textboxname {
          font-weight: 400;
          font-size: 16px;
        }
      }
    }
  }
  .key_modal {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    padding: 20px;
    background: rgba(0, 0, 0, .8);
    display: flex;
    justify-content: center;
    align-items: center;
    .inner {
      padding: 20px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      .title {
        position: absolute;
        left: 80px;
        top: 100px;
        display: flex;
        align-items: center;

        font-size: 40px;
        font-weight: 700;
        .deco {
          width: 7px;
          height: 40px;
          background: rgb(255, 232, 0);
          margin-right: 15px;
        }
      }
      .name {
        font-size: 60px;
        text-align: center;
      }
      .key {
        background: rgb(255, 232, 0);
        color: black;
        padding: 10px 20px;
        min-width: 200px;
        font-size: 60px;
        text-align: center;
        margin-bottom: 70px;
        margin-top: 40px;
      }
      .buttons {
        display: flex;

        justify-content: space-between;
        .button {
          width: 200px;
          height: 60px;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 28px;
          font-weight: 400;
          cursor: pointer;
          border: 5px solid transparent;
          &.save {
            border-color: rgb(76, 223, 116);
            color: rgb(76, 223, 116);
            background: rgba(76, 223, 116, .1);
          }
          &.cancel {
            border-color: rgb(100, 100, 100);
            color: rgb(100, 100, 100);
            background: rgba(100, 100, 100, .2);
            margin-right: 100px;
          }

        }
      }
    }
  }
  .update{
    position: fixed;
    background: rgb(255, 232, 0);
    color: black;
    font-size: 28px;
    padding: 20px;
    left: 0;
    right: 0;
    top: 50px;
    cursor: pointer;
    font-weight: 400;
    text-align: center;
  }
  .disabled {
    opacity: .5;
    pointer-events: none;
  }
  .copy-button {
    padding: 8px 16px;
    background: #4CAF50;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    
    &:disabled {
      background: #cccccc;
      cursor: not-allowed;
    }
    
    &:hover:not(:disabled) {
      background: #45a049;
    }
  }

  .copy-result {
    margin-top: 8px;
    padding: 8px;
    border-radius: 4px;
    background: #e8f5e9;
    color: #2e7d32;
    
    &.error {
      background: #ffebee;
      color: #c62828;
    }
  }
  .steam-id {
    margin-top: 8px;
    font-size: 12px;
    color: #666;
  }
}
</style>

