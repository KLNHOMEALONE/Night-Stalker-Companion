# Деплой MAUI-приложения на настоящий iPhone

Рабочая процедура, извлечённая из сессии 22.11.2026. Цель — поставить
`NightStalkerCompanion` на физический iPhone 17 и запустить его.

Параметры окружения:

| | |
|---|---|
| Телефон | iPhone 17 (`iPhone18,3`), iOS 27.2.1 |
| UDID | не публикуется; взять в Xcode → Window → Devices and Simulators, или `xcrun devicectl list devices` |
| Команда | Personal Team (`DEVELOPMENT_TEAM` из Xcode → Settings → Accounts), тот же Apple ID |
| .NET | 10.0.404 в `~/.dotnet` (в `/usr/local/share/dotnet` только 6/7) |
| Xcode | 27.2 (27B52) в `/Applications/Xcode.app` |
| Оболочка | x86_64 под Росеттой → `xcrun` из CLT нерабочий |

## 0. Переменные вместо `xcode-select`

Активная developer-директория — Command Line Tools, а её `libxcrun` собран только
под arm64, поэтому из x86_64-шелла `xcrun`/`xcodebuild`/`devicectl` падают.

```bash
export DOTNET_ROOT=$HOME/.dotnet
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
```

После этого `xcodebuild` и `simctl` работают, `sudo xcode-select -s` не нужен.
Команды Apple запускать под `arch -arm64`.

## 1. Подпись: отсутствующий промежуточный WWDR G3

Симптом: сертификат и ключ в порядке, но

```
$ security find-identity -v -p codesigning
     0 valid identities found
$ codesign --force --sign "Apple Development: …" /tmp/t.bin
  Warning: unable to build chain to self-signed root … errSecInternalComponent
```

Диагностика по шагам:

```bash
security find-identity -p codesigning        # БЕЗ -v показывает невалидные!
openssl x509 -in ad.pem -noout -issuer       # издатель листа → нужон WWDR с этим OU
security find-certificate -a -c "Apple Worldwide Developer Relations" -p \
  | csplit -z -f ww -b '%d.pem' '/-----BEGIN/' '{*}'
for f in ww*.pem; do openssl x509 -in $f -noout -subject -issuer; done
```

Две ловушки:

- `-v` скрывает невалидные идентичности. Без флага было видно `1 identities found`.
- `find-certificate -c` матчит по **CN**, а промежуточные WWDR различаются только `OU`.
  Классический WWDR (`OU=Apple Worldwide Developer Relations`) в keychain был, а
  нужный `OU=G3` — отсутствовал. Вывод «промежуточный есть» без проверки OU ложный.

Лечение:

```bash
curl -sSfL -o AppleWWDRCAG3.cer https://www.apple.com/certificateauthority/AppleWWDRCAG3.cer
security add-certificates -k ~/Library/Keychains/login.keychain-db AppleWWDRCAG3.cer
security find-identity -v -p codesigning     # → 1 valid identities found
```

## 2. Профиль провижининга без GUI Xcode

В .NET для iOS **нет** `AutoProvision`/`AutoSign`. SDK читает только
`CodesignKey`, `CodesignProvision`, `ProvisioningProfile`, `CodesignEntitlements`
(`grep` по `~/.dotnet/packs/Microsoft.iOS.Sdk.net10.0_27.2/*/tools/msbuild`).
Значит free-профиль нужно выпустить явно.

`xcodegen` сюда не поставить: Intel Homebrew вызывает `xcrun` из CLT и получает
`CompilerSelectionError`. Поэтому минимальный `.xcodeproj` пишется руками
(`bundle id` = `ApplicationId` из csproj, `CODE_SIGN_STYLE=Automatic`,
`DEVELOPMENT_TEAM=<ваш Team ID>`, `GENERATE_INFOPLIST_FILE=YES`), плюс **общая схема**
`xcshareddata/xcschemes/*.xcscheme`.

```bash
xcodebuild -project ProvisionMint.xcodeproj -scheme ProvisionMint \
  -configuration Debug -destination "id=$UDID" \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build
```

Важно: **обязательно `-scheme`**. С `-target` регистрация устройства не выполняется,
и портал отвечает `Your team has no devices from which to generate a provisioning profile.`

Профиль появляется в `~/Library/Developer/Xcode/UserData/Provisioning Profiles/`,
читается так:

```bash
security cms -D -i "<профиль>.mobileprovision" > prov.plist
/usr/libexec/PlistBuddy -c 'Print :Name' prov.plist
/usr/libexec/PlistBuddy -c 'Print :Entitlements:application-identifier' prov.plist
/usr/libexec/PlistBuddy -c 'Print :ProvisionedDevices' prov.plist   # должен содержать UDID
```

Для бесплатной команды срок жизни — 7 дней.

## 3. Сборка и установка

```bash
~/.dotnet/dotnet build NightStalkerCompanion/NightStalkerCompanion/NightStalkerCompanion.csproj \
  -f net10.0-ios -r ios-arm64 -c Debug \
  -p:CodesignKey="Apple Development: <Apple ID> (<Team ID>)" \
  -p:CodesignProvision="iOS Team Provisioning Profile: com.companyname.nightstalkercompanion"
```

Проверка подписи: `codesign --verify --verbose=1 <App>.app` → `valid on disk`,
`Authority=… → WWDR → Apple Root CA`.

```bash
arch -arm64 xcrun devicectl device install app --device $UDID <App>.app
arch -arm64 xcrun devicectl device process launch --device $UDID com.companyname.nightstalkercompanion
```

## 4. Стена доверия на устройстве

Установка проходит, запуск отказывает:

```
FBSOpenApplicationErrorDomain error 3 — Security
Unable to launch … because it has an invalid code signature, inadequate
entitlements or its profile has not been explicitly trusted by the user
```

Локальная подпись при этом полностью валидна. Причина в том, что приложение
положено через `devicectl`, а не потоком Run из Xcode, который прокидывает доверие
сам. Требуется первое открытие из Springboard / явное доверие на телефоне:
`Settings → General → VPN & Device Management` → доверить `Apple Development: …`,
либо просто тапнуть по иконке приложения при разблокированном телефоне.

Проверенные и отброшенные версии причин: `Developer Mode` (проверить можно
заранее — `devicectl device info details` печатает `Developer Mode Status: Enabled (1)`),
регистрация UDID в профиле, срок профиля, `get-task-allow`.
