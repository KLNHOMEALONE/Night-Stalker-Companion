# PardVision2 → Night Stalker 4K: recovered device API

> **ЧИТАТЬ ПЕРВЫМ ДЕЛОМ (22.11).** Этот документ написан 17.11 по **строкам** из
> бинарника приложения и с тех пор частично опровергнут живыми прогонами. Не
> путать «строка есть в бинарнике» с «прибор так умеет». Актуальное состояние
> канала записи и перечень того, чего прицел **не** делает, — в
> `notes/CMD_TABLE.md`, раздел «ОКНО 22.11». Из утверждений этого файла проверены
> и верны: адрес `192.168.1.254`, HTTP-конверт `custom=1&cmd=`, RTSP `track1`,
> листинг и загрузка файлов через hfs. Не подтвердились: работающий DCT-сокет
> (прицел молчит на 80/443/554/8000 даже штатному приложению), веб-интерфейс и
> какие-либо JS-ассеты (всё, кроме листинга hfs, — «Page Not found» на 128 байт),
> и «самоописываемость» прибора: `query_cmd_list`/`query_menuitem` в переборе
> `cmd=1..8191` ничем не проявили себя, а `POST` с `status=` отвечает
> `<Cmd>3027</Cmd><Status>0</Status>` на **любое** тело, включая выдуманное имя.

Цель: hardware-level доступ к прицелу (файлы, настройки, баллистика) без приложения.
Всё ниже получено статическим анализом `App.framework/App` (Dart AOT, arm64) из
`/Applications/PardVision2.app/Wrapper/Runner.app/`.

## 0. Что за приложение

| факт | значение |
|---|---|
| Стек | **Flutter** (iOS-сборка, запущена на macOS как «My Design for iPad» bundle) |
| Bundle id | `pard.pardvision2.pard` |
| Версия | 3.0.1 (build 1) |
| Основной бинарник логики | `Frameworks/App.framework/App` (9.3 MB, Mach-O dylib, arm64, Dart AOT) |
| Плеер | `Frameworks/mdk.framework` (MDK + libavformat/RTSP), `fvp.framework` |
| Логика не обфусцирована | в снапшоте остались пути `package:pardvision2/...` → восстановлено **126 файлов** дерева исходников (`00_recon/dart_source_paths.txt`) |

Ключевые плагины, задающие архитектуру: `dio`, `http`, `xml`, `xml2json`, `wifi_iot`
(смена Wi-Fi сети телефоном), `sqflite` (локальная БД), `gal` (галерея), `get` (GetX),
`rxdart`, `crypto` (MD5 для файлового сокета).

Из `Info.plist`:
- `NSLocalNetworkUsageDescription` = “discover and connect to **PARD devices**”
- `com.apple.developer.networking.multicast` = true (mDNS/UDP-broadcast discovery)
- `UIRequiresPersistentWiFi` = true
- `NSAppTransportSecurity → NSAllowsArbitraryLoads` = true → **разрешён чистый HTTP**

**Вывод: связь с прицелом идёт по Wi-Fi (IP-протокол), не по USB.** USB в `ioreg`/
`system_profiler` прицела не виден (только хаб и мышь) — см. `00_recon/usb_before.txt`.
USB у таких устройств — только charging/MSC, управляющего протокола там нет.

## 1. Транспорт — 4 независимых канала

Прицел поднимает собственную точку доступа; адрес девайса зашит в код:

```
AP-адрес:  http://192.168.1.254
```

| # | Канал | Назначение | Доказательство (строка из бинарника) |
|---|---|---|---|
| 1 | **HTTP `{"status":...,"value":...}`** | команды: настройки, статус, баллистика, прицельная сетка, Wi-Fi, время | строки `"status":"` и `,"value":"`, `package:pardvision2/conf/network/c_request_device.dart`, `c_set.dart` |
| 2 | **TCP сокет, файловый протокол `DCT_Socket_Cmd=5005`** | листание/скачивание/удаление файлов с SD, чанками + MD5 | `DCT_Socket_Cmd=5005&Function=0`, `...Function=1&FileDownLoad=1&FileSizeSum=`, `...Function=2&FilePath=`, рядом `File_Create`, `File_GetPointer`, `&PackDataSize=`, `&PackDataNum=`, `&MD5DataValue=`, `&FilePackSum=` |
| 3 | **RTSP** | live view + воспроизведение записей | `rtsp://192.168.1.254/xxxx.mov`, `query_rtsp_stream`, `movie_liveview_start`, `avformat.rtsp_transport`, `avformat.rtsp_flags`, `video/h264` |

> **Медиа в архиве нет.** Ни в одном дампе (`api-20261118-215251/session.pcap`,
> `sprint-20261118-213153/scope.pcap`, `streams/*.txt`, `payloads/*`) не записано
> ни одного кадра: сохранены только RTSP-управление (OPTIONS/DESCRIBE/SETUP) и SDP.
> Проверено поиском `start-code + NAL type 5/7/8` по всем файлам от 8 КБ: ноль
> вхождений. Раньше здесь же утверждалось обратное («2478 start-code в
> session.pcap») — это ложные срабатывания на TCP-опциях `00 00 00 01 01 08 0a`
> (NOP NOP Timestamp), не H.264. Следствие: показать чужой live view из архива
> нельзя, можно снять заново или генерировать свой поток.
| 4 | **HTTP firmware** | апгрейд прошивки | `http://192.168.1.254/upload` (multipart: `------WebKitFormBoundary…`), `/?custom=1&cmd=<N>` рядом со строкой `Firm Update`, `http://192.168.1.254` рядом с `_executeFirmwareUpgrade` |

Формат канала 1 (уверенность высокая): пара `status` = имя команды, `value` = аргумент.
Командные имена лежат в таблице строк рядом (`set_ssid`, `query_battery`, …), т.е. это
значения поля `status`. Рядом же найдены шаблоны лога `cmd_zoom  status : ` и
`cmd_record  status : ` — значит ответ приходит как «status : <значение>».

Самоописываемость: есть `query_cmd_list` и `query_menuitem`, плюс флаг `isSupportMenuCmd`
→ устройство **само перечисляет поддерживаемые команды/пункты меню**. Числовые ID пунктов
меню в код не зашиты (в `__const` их нет) — они приходят от устройства. Это снимает
половину задачи реверса: ID надо не восстанавливать, а снять с живого устройства один раз.

## 2. Канал 2: файловый сокет (фото/видео с SD-карты)

Размерные/пакетные параметры, извлечённые дословно:

```
DCT_Socket_Cmd=5005&Function=0                                  # handshake / init
DCT_Socket_Cmd=5005&Function=1&FileDownLoad=1&FileSizeSum=<n>   # скачать чанками
DCT_Socket_Cmd=5005&Function=2&FilePath=<path>                  // явный путь
&FilePackSum= &PackDataNum= &PackDataSize= &MD5DataValue= &RestartFlag= &SF= &GroupId= &Gtitle=
```

Модель: файл разбивается на пакеты фиксированного размера, на каждый пакет ожидается
MD5-подтверждение (`crypto` в зависимостях, `&MD5DataValue=`). `Function=0` — вход в
режим передачи. Рядом в строках — нативные имена `File_Create`, `File_GetPointer`,
`getAllDocs` (последнее — локальная SQLite-база приложения, `util/dbhelper.dart`).

Файлы на устройстве: строка `/mnt//Movie` (шаблон `/mnt/<card>/Movie`) — каталог видео.
Фото, судя по `photo_taking` / `video_snapshot` — рядом (обычно `Photo`/`Image`, требует
уточнения на живом устройстве).

## 3. Извлечённый словарь команд и полей

Полный список: `notes/device_command_vocab.txt`. Ниже — релевантное железу.

### 3.1 Статус / discovery
`query_cmd_list`, `query_cur_status`, `query_battery`, `query_bi_cell` (банкам/ячейки
аккумулятора), `get_card_status`, `get_connect`, `query_rtsp_stream`,
`query_menuitem`, `isSupportMenuCmd`
Поля: `device_storage`, `sd_card_not_inserted`, `sdcard_will_formatted`,
`firmware_version_current`, `firmware_version_latest`, `battery_low_need_charge`,
`current_date`, `current_time`, `current_timestamp`, `device_region`

### 3.2 Фото / видео / запись
`start_recording`, `stop_recording`, `cmd_record`, `cmd_zoom`, `photo_taking`,
`video_snapshot`
Поля/события: `movie_rec_size`, `movie_recording_time`, `movie_ev`, `movie_gsensor_sens`,
`movie_liveview_start`, `rec_set`, `cyclic_rec` (циклическая запись), `video_recording`,
`video_in_progress`, `video_thumbnail`, `photo_taking_successfully`,
`recording_start_failed`, `recording_stopping`, `reconnecting_video`

### 3.3 Настройки картинки / ИК-подсветки
Классы-сеттеры (остались в снапшоте как имена функций):
`_cSet`, `_cSetIRBrightness`, `_cSetIRContrast`, `_cSetIRDetail`, `_cSetIRSharpness`
по файлу `conf/network/c_set.dart`.
Поля: `ir_brightness`, `ir_contrast`, `ir_detail`, `ir_sharpness`, `ir_mode`,
`ir_fill_light`, `ir_brightness_disable`, `ir_brightness_enable`, `default_ir`,
`image_setting`, `default_color`, `color_effect`, `default_magn`, `defaul_zoom`,
`blind_pix`, `blind_pixel_comp` (коррекция битых пикселей — характерно для сенсора),
`auto_power_off`, `auto_power_off_on`, `auto_poweroff_time`, `activity_recognition`,
`charge_mode_upgrade`

### 3.4 Прицельная сетка (reticle)
`get_reticle`, `set_reticle`; UI/поля: `reticle_adjustment`, `reticle_enable`,
`reticle_disable`, `point_sharp_color`, файлы `pages/reticle/*`
(`reticle.dart`, `reticle_page.dart`, `reticle_postion.dart`, `reticle_model.dart`)

### 3.5 Баллистический калькулятор
Файлы: `pages/ballistic/blc.dart`, `blc_calculate.dart`, `blc_calibrate.dart`,
`controller/blc_controller.dart`, `views/mem/*`, модели `model/blc_data.dart`,
`blc_mem_data.dart`, `blc_model.dart`.
Команды: `get_ballistic`, `set_ballistic`, `set_mem_ballistic` (запись в память прицела),
`set_group_name`, `group_name_list`
Поля: `ballistic_data`, `ballistic_model`, `zero_range`, `bullet_weight`, `bullet_bc`
(баллистический коэффициент), `assets/ballistic/icon_BallisticRPSC_*` (иконки по
семействам прицельных марок)
Контроллер наследуется от `SocketMessageGetxMixin`
(`_BLCController&GetxController&SocketMessageGetxMixin@956223281`) → **баллистика
обменивается с устройством через канал 2 (сокет) сообщениями**, а не через HTTP.
Калибровка: `calibrate_success`, `calibrate_failed`.

### 3.6 Wi-Fi прицела (смена SSID/пароля точки доступа)
`set_ssid`, `set_passphrase`, `update_wifi_name`, `update_wifi_password`,
`update_wifi_name_failed/_success`, `update_wifi_password_failed/_success`,
`update_wifi_name_regex_failed`, `wifi_name`, `wifi_password`, `wifi_signal`
UI: `pages/vision/menu/func_setting/wifiSetttings.dart`
+ плагин `wifi_iot` и `socketState.dart`

### 3.7 Прочее железо
`set_time`, `set_date`, `datestamp_on`, `datestamp_off`, `SetPassword`, `access_device`,
`add_device`, `device_lost`, `Device Disconnected`

## 4. Где ещё искать (следующие шаги)

1. **Числа для `?custom=1&cmd=<N>`** — это immediates в машинном коде Dart AOT.
   Инструменты готовы: Ghidra 12.2 + JDK 21 arm64 (`tools/jdk/...`), бинарник уже
   импортирован в проект `01_ghidra/proj/pardvision_app` (`AARCH64:LE:64:AppleSilicon`),
   полный анализ ещё не запускался. Либо `blutter` по Android-APK (`com.nt.pard`) —
   Mach-O/iOS blutter не читает (в README: TODO). Приоритет ниже, чем п.2,
   потому что ID приходят от `query_cmd_list`.
2. **Снять живой трафик** — один захват даёт больше, чем неделя статики: полный
   `query_cmd_list`, реальные пути файлов, реальный RTSP URL, порт сокета.
3. **Порт TCP-сокета канала 2** — в строках не лежит (число в коде). Определяется
   сканом/снифером при скачивании файла.
4. `xml` + `xml2json` в зависимостях → возможно часть ответов (особенно меню/прошивок)
   в XML, а не JSON. Проверить на живом устройстве.
5. Облако (`https://api.pardvision.top`, `conf/network/aliyun_*.dart`) — намеренно
   исключено из скоупа, к железу не относится.

## 5. Важное ограничение среды
Приложение **никогда не запускалось на этом Mac** — контейнера `pard.pardvision2.pard`
нет, значит кеша `pard.db` (`util/dbhelper.dart`) с сохранённым деревом команд/меню прицела
на диске не существует. Само оно тоже не запускается: `open -gj` не поднимает процесс,
прямой exec `Runner` → `Killed: 9` (подпись/taskgates). Снимать трафик самого приложения
с этой машины нельзя в принципе — только с телефона (нужен шлюз, см. CONNECTIVITY).

## 6. Артефакты в этой папке

```
00_recon/
  app_strings_ascii.txt      45503 строки из App (ASCII)
  app_strings_utf16.txt      546 строк UTF-16LE
  dart_source_paths.txt      126 восстановленных путей package:pardvision2/**.dart
  dart_class_groups.txt      классы Dart, сгруппированные по library-тегам (@NNN)
  dct_protocol.txt           семейство DCT_Socket_Cmd + все &Param=
  http_paths.txt             все URL-пути + `/?custom=1&cmd=`
  response_format.txt        ключи "status"/"value"
  streaming.txt              RTSP/h264/плейерные строки
  url_clusters.txt           контекст ±2.6KB вокруг `/?custom=1&cmd=` и DCT_Socket_Cmd
  status_clusters.txt        контекст вокруг всех команд-якорей
  baseurl_clusters.txt       контекст вокруг http/rtsp/mnt
  cmd_cluster_raw.txt        сырой кластер строк таблицы команд
  cmd_candidates.txt         кандидаты в имена команд
  usb_before.txt             снимок USB-шин (прицела там нет)
  net_live.txt               маршруты/интерфейсы (маршрут к .254 идёт через default GW)
01_ghidra/App_bin            копия App для дизассемблирования
notes/device_command_vocab.txt  словарь команд и полей
tools/blutter                blutter (для ELF/Android-варианта)
```
