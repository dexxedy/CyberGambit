# CyberGambit — справочник скриптов (ветка `dev`, stable)

Документ для **защиты проекта** с позиции **разработчика (programmer)**.  
Описывает все игровые скрипты в `Assets/Scripts/`, `Assets/VFX/` и редакторский хелпер в `Assets/Editor/`.

**Версия кода:** коммит `stable` (20.05.2026), ветка `dev`.

---

## Содержание

1. [Как пользоваться на защите](#как-пользоваться-на-защите)
2. [Архитектура в двух словах](#архитектура-в-двух-словах)
3. [Общие типы и перечисления](#общие-типы-и-перечисления)
4. [Ядро игры](#ядро-игры)
5. [Сетка и правила хода](#сетка-и-правила-хода)
6. [Камера и режимы](#камера-и-режимы)
7. [Юнит и бой](#юнит-и-бой)
8. [Оружие и эффекты](#оружие-и-эффекты)
9. [Интерфейс: тактика и экшен](#интерфейс-тактика-и-экшен)
10. [Кубик и расстановка армии](#кубик-и-расстановка-армии)
11. [QTE и способности](#qte-и-способности)
12. [Бот и миссионный ИИ](#бот-и-миссионный-ии)
13. [Миссии: общее](#миссии-общее)
14. [Миссия 1 — «Прорыв»](#миссия-1--прорыв)
15. [Миссия 2 — «Осада»](#миссия-2--осада)
16. [Хаб и выбор миссий](#хаб-и-выбор-миссий)
17. [Туман войны](#туман-войны)
18. [Меню, звук, обучение](#меню-звук-обучение)
19. [Редактор Unity](#редактор-unity)
20. [Карта зависимостей](#карта-зависимостей)
21. [Типичные вопросы на защите](#типичные-вопросы-на-защите)

---

## Как пользоваться на защите

**Роль developer в команде** — вы отвечаете за то, *как устроен код*, связи систем, почему выбраны такие модули, что делает каждый компонент.

Рекомендуемый порядок рассказа (15–25 минут):

1. **Поток игрока:** MainMenu → HubBase → миссия → расстановка → тактика → FPS → победа.
2. **Три столпа:** `GameManager` (правила матча), `CameraManager` (два режима), `Unit` (всё про бойца на поле).
3. **Контент миссий:** отдельные менеджеры победы + `IBotMissionBrain` для ИИ.
4. **Фишки:** QTE, способности по классам, туман войны, 3D-кубик в UI.
5. **Данные:** ScriptableObject для миссий, оружия, квестов, настроек тумана.

На вопрос «где это в коде?» — открывайте этот файл по оглавлению или Unity: `Assets/Scripts/`.

---

## Архитектура в двух словах

```
MainMenu ──► HubBase (FPS, терминал) ──► mission1 / mission2
                                              │
                    ┌─────────────────────────┴─────────────────────────┐
                    ▼                                                   ▼
           ArmyDeploymentController                              GameManager
           (сбор отряда за очки)                               (ходы, кубик, пауза)
                    │                                                   │
                    ▼                                                   ▼
           CameraManager ◄──────────────────────────────────────► BotController
           (тактика / экшен)                                      (+ Mission*BotBrain)
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
  TacticalModeUI  Unit      FogOfWarManager
  TacticalMap*    Weapon    MissionQuestUI
  ActionModeUI    QTE...
```

**Паттерны в проекте:**

| Паттерн | Где |
|--------|-----|
| Singleton (`Instance`) | `GameManager`, `CameraManager`, `ChessGrid`, `ChessRulesManager`, `AbilitySystem`, `QTESystem`, UI-менеджеры, `FogOfWarManager` |
| ScriptableObject (данные) | `MissionDefinition`, `MissionObjectivesConfig`, `RangedWeaponConfig`, `FogOfWarSettings` |
| Интерфейс / плагин | `IBotMissionBrain` — разное поведение бота в миссиях |
| События | `GameManager.OnDiceRolled`, `Unit.OnGridPositionChanged` |
| DontDestroyOnLoad | `AudioManager` |

**Важно:** имена `ChessGrid`, `ChessRulesManager`, `ChessUnitType` — **исторические**. Это тактическая сетка и классы отрядов (пешка, конь, слон…), не шахматная партия.

---

## Общие типы и перечисления

| Тип | Файл | Назначение |
|-----|------|------------|
| `Player` | `GameManager.cs` | `Player1` (человек) / `Player2` (враг или второй игрок) |
| `GameMode` | `GameManager.cs` | `PlayerVsPlayer` / `PlayerVsBot` |
| `ChessUnitType` | `ChessRulesManager.cs` | Класс юнита: Pawn, Horse, Bishop, Queen, King, Guardian |
| `UnitCombatRole` | `Unit.cs` | Standard / Tank — для правил выбора цели ботом |
| `FogCellState` | `FogOfWarManager.cs` | Unexplored / Explored / Visible |
| `QTEResult`, `QTEKey` | `QTESystem.cs` | Результат мини-игры и клавиши Z/X/C |
| `DeploymentOfferEntry` | `ArmyDeploymentController.cs` | Запись в каталоге карточки расстановки (префаб, цена, лимит копий) |

---

## Ядро игры

### `GameManager.cs`
**Путь:** `Assets/Scripts/GameManager.cs`  
**Роль:** главный менеджер матча (singleton).

**Что делает:**

- Хранит текущего игрока (`Player1` / `Player2`), режим `PlayerVsPlayer` / `PlayerVsBot`.
- Управляет **очередью ходов**: `SwitchTurn`, сброс кубика на новый ход.
- **Кубик:** диапазон броска, перевод очков в метры движения (`metersPerDicePoint`), событие `OnDiceRolled` для UI.
- **Фаза расстановки:** `BeginArmyDeploymentPhase` / `CompleteArmyDeploymentPhase` / `IsArmyDeploymentPhase` — пока игрок ставит армию, обычный ход не идёт.
- Определяет направление «вперёд» для каждой стороны по стартовым позициям юнитов (важно для первого хода пешки и подсветки клеток).
- **Конец игры:** `EndGame`, панель победы/поражения, остановка логики.
- **Пауза:** Esc, панель настроек, громкость музыки/SFX через `AudioManager`.
- Перезагрузка сцены, выход в главное меню.
- При смене хода дергает `AbilitySystem.OnTurnSwitch`, обновляет туман войны.

**С кем связан:** почти все системы; обязательно знать для защиты.

---

### `Unit.cs`
**Путь:** `Assets/Scripts/Unit.cs`  
**Роль:** один боец на карте — и в тактике, и в FPS.

**Что делает:**

- **Сетка:** `currentGridPosition`, привязка к `ChessGrid`, событие `OnGridPositionChanged`.
- **Движение:** бюджет шагов/метров от кубика; ход по клеткам; в экшен-режиме — `CharacterController`, WASD, прицел мышью.
- **Бой:** HP, урон, `TakeDamage`, `Attack`; поддержка ближнего боя и дальнего через компонент `Weapon`.
- **Роли:** `ChessUnitType`, флаг танка (`UnitCombatRole` / `TankController` в миссии 2).
- **Управление:** `SetControlled` — только один юнит под игроком в экшене.
- **Способности:** баффы отражения, щита, усиления урона; синхронизация с `UnitAbilities`.
- **QTE:** режим блокировки урона при успешной реакции.
- **Хаб:** `hubExploreNoCombat` — на базе нет боя, бесконечный бюджет хода.
- **Смерть:** анимация, звук, уничтожение; смерть короля → `GameManager.EndGame`.

**С кем связан:** `CameraManager`, `GameManager`, `ChessGrid`, `Weapon`, `QTESystem`, миссии, бот.

---

### `BotController.cs`
**Путь:** `Assets/Scripts/BotController.cs`  
**Роль:** ИИ противника в режиме PvBot.

**Что делает:**

- Корутина `ExecuteBotTurn`: выбор юнита → камера → бросок кубика → ход/атака → конец хода.
- Оценка клеток и целей (ценность фигур, дистанция, видимость).
- Движение по NavMesh, атака в радиусе, проверка линии видимости.
- **QTE для бота:** `TryBotQTEBlock` — шанс «заблокировать» атаку игрока.
- **Танк:** обычные юниты не выбирают танк целью; Guardian может бить танк.
- Делегирует стратегию миссии компонентам `IBotMissionBrain` (`Mission1BotBrain`, `Mission2BotBrain`), если они есть на сцене.

**С кем связан:** `GameManager`, `Unit`, `CameraManager`, `FogOfWarManager`, мозги миссий.

---

### `MainMenu.cs`
**Путь:** `Assets/Scripts/MainMenu.cs`  
**Роль:** главное меню.

**Что делает:**

- Кнопка «Играть» → загрузка сцены **HubBase** (не сразу в миссию).
- Выход из приложения.
- Панель настроек: слайдеры громкости, `ApplySettings` → `PlayerPrefs`.
- Звуки кнопок через `AudioManager`.

---

### `AudioManager.cs`
**Путь:** `Assets/Scripts/AudioManager.cs`  
**Роль:** глобальный звук (часто `DontDestroyOnLoad`).

**Что делает:**

- `EnsureInstanceExists` — создаёт себя при старте меню/хаба.
- UI: клик, выбор юнита, вход в экшен.
- QTE: старт, успех, провал.
- Музыка: меню, бой, победа, поражение; перезапуск, если застряла.
- `PlayerPrefs`: `MusicVolume`, `SFXVolume`.

---

## Сетка и правила хода

### `ChessGrid.cs`
**Путь:** `Assets/Scripts/ChessGrid.cs`  
**Роль:** тактическая сетка (singleton).

**Что делает:**

- Параметры поля: ширина, высота, размер клетки, точка начала координат.
- Перевод мир ↔ клетка (`WorldToGridCoords`, `GridToWorldPosition`).
- Шахматная нотация для отладки (`A1`, `C4` ↔ `Vector2Int`).
- `IsValidCoord`; отрисовка сетки в Scene View (Gizmos).

**Зачем:** единая математика для хода, расстановки, миникарты, бота.

---

### `ChessRulesManager.cs`
**Путь:** `Assets/Scripts/ChessRulesManager.cs`  
**Роль:** проверка **геометрии** хода по типу юнита (singleton).

**Что делает:**

- При старте собирает все `Unit` на сцене, опционально `SnapToGrid`.
- `IsMoveValid(start, end, type)` — пешка (включая первый ход вперёд), конь (Г-образный), ладья/слон/ферзь/король по линиям.
- `GetHorsePossibleMoves` — 8 направлений коня в пределах сетки.
- **Не проверяет** занятость клетки в полной шахматной логике — это дополняется в `Unit` / подсветке.

**Зачем:** ограничить тактические перемещения «как у класса», не свободным бегом по всему полю.

---

### `GridHighlighter.cs` (файл `GridHilghlighter.cs`)
**Путь:** `Assets/Scripts/GridHilghlighter.cs`  
**Роль:** подсветка **следующего** шага на сетке.

**Что делает:**

- `ShowAllowedMoves(unit)` — одна ортогональная клетка от текущей позиции, если есть бюджет хода.
- Учитывает занятость клетки другим юнитом.
- Первый ход в матче может быть только «вперёд» по направлению стороны из `GameManager`.
- Спавнит префаб подсветки на клетках.

**Зачем:** игрок видит, куда может сделать тактический шаг.

---

## Камера и режимы

### `CameraManager.cs`
**Путь:** `Assets/Scripts/CameraManager.cs`  
**Роль:** переключение тактики и экшена (singleton).

**Что делает:**

- **Тактическая камера:** полёт над полем, зум, поворот, скролл к краям экрана.
- **Экшен-камера:** привязка к `Unit.cameraAttachPoint`, FPS-вид.
- Выбор юнита на карте / Tab → вход в экшен; выход → `GameManager.SwitchTurn`.
- **Ход бота:** кинематографическое следование, скрытие тел юнитов при тактике сверху (`TacticalUnitPresentation`).
- **Расстановка армии:** фиксированный вид сверху, плавный выход после Confirm.
- **Хаб:** `EnterHubExploreMode` — только FPS, без возврата в тактику.
- Esc: сначала закрыть UI миссий в хабе, иначе пауза через `GameManager`.
- При game over блокирует переключения.

**Зачем:** ключевая «фишка» проекта — два режима в одной сцене.

---

### `TacticalUnitPresentation.cs`
**Путь:** `Assets/Scripts/TacticalUnitPresentation.cs`  
**Роль:** видимость **3D-модели** юнита (не коллайдеров).

**Что делает:**

- `SetBodyVisualVisible` — вкл/выкл рендереры на юните.
- Глобально: в тактике сверху тела скрыты, на карте показываются иконки.

---

## Юнит и бой

*(основная логика в `Unit.cs` — см. выше)*

### `UnitColorController.cs`
**Путь:** `Assets/Scripts/UnitColorController.cs`  
**Роль:** разовая раскраска всех юнитов на сцене по стороне.

**Что делает:** в `Start` красит материалы в синий (Player1) / красный (Player2).

**Зачем:** быстрая визуальная дифференциация без ручной настройки каждого префаба.

---

## Оружие и эффекты

### `Weapon.cs`
**Путь:** `Assets/Scripts/Weapon.cs`  
**Роль:** стрелковое оружие на префабе.

**Что делает:**

- Магазин, запас, перезарядка, кулдаун выстрела из `RangedWeaponConfig`.
- `TryFire` — луч, урон по `Unit`, опционально friendly fire.
- VFX: дульная вспышка, трассер, попадание; `CameraShake`.

---

### `RangedWeaponConfig.cs`
**Путь:** `Assets/Scripts/RangedWeaponConfig.cs`  
**Роль:** данные оружия (ScriptableObject).

**Поля:** дальность, урон, RPM, размер магазина, время перезарядки, маска попаданий, friendly fire.

**Зачем:** баланс без правки кода — разные asset для винтовки, пушки танка и т.д.

---

### `WeaponCollider.cs`
**Путь:** `Assets/Scripts/WeaponCollider.cs`  
**Роль:** **ближний** бой по триггеру на оружии.

**Что делает:**

- Урон только на ходу владельца, во время анимации атаки.
- Учитывает `QTESystem.WasAttackBlocked` — при блоке урона нет.
- Урон с множителем способностей; вызов `TakeDamage` с атакующим для отражения.
- Звук и VFX удара.

---

### `HandIKController.cs`
**Путь:** `Assets/Scripts/HandIKController.cs`  
**Роль:** IK рук на оружии в FPS.

**Что делает:**

- `OnAnimatorIK` — левая/правая рука к точкам хвата на модели оружия.
- `RefreshGrips` после смены оружия.

---

### `CameraShake.cs`
**Путь:** `Assets/VFX/CameraShake.cs`  
**Роль:** тряска камеры при выстрелах и ударах.

**Что делает:** singleton, корутина случайного смещения `localPosition` на заданное время.

---

## Интерфейс: тактика и экшен

### `TacticalModeUI.cs`
**Путь:** `Assets/Scripts/TacticalModeUI.cs`  
**Роль:** HUD **тактического** режима (singleton).

**Что делает:**

- Текст хода (ваш / бот / игрок 2).
- Отображение результата кубика и оставшегося запаса хода.
- Обучающая панель при первом запуске (цель, управление).
- Скрывается при расстановке армии, паузе, экшен-режиме.

---

### `ActionModeUI.cs`
**Путь:** `Assets/Scripts/ActionModeUI.cs`  
**Роль:** HUD **экшен**-режима (singleton).

**Что делает:**

- Прицел: цвет (нейтральный / союзник / враг) по raycast из центра экрана.
- Панель врага: имя класса, полоска HP.
- Способность: название, описание, кулдаун (скрыто для пешки).
- HP игрока, «целостность» (оставшиеся метры хода), патроны/перезарядка.
- `ShowHUD` / `HideHUD` при смене режима и QTE.

---

### `TacticalMapUIController.cs`
**Путь:** `Assets/Scripts/TacticalMapUIController.cs`  
**Роль:** **миникарта** на UI.

**Что делает:**

- Иконка на каждый живой `Unit`; обновление позиции при смене клетки.
- Режим координат: по сетке или по миру (XZ).
- PvBot + туман: враги через `FogWarIntelTracker` (последняя известная позиция).
- Клик по своей иконке → `CameraManager.TrySwitchToActionModeFromMap` (если уже бросили кубик и не ход бота).

---

### `TacticalMapUnitIcon.cs`
**Путь:** `Assets/Scripts/TacticalMapUnitIcon.cs`  
**Роль:** одна иконка на миникарте.

**Что делает:** клик → контроллер карты → вход в экшен с этим юнитом.

---

### `TacticalWorldIconsController.cs`
**Путь:** `Assets/Scripts/TacticalWorldIconsController.cs`  
**Роль:** иконки юнитов **над полем** (проекция с 3D в экран).

**Что делает:**

- Синхронизация карточек `TacticalWorldUnitIcon` для каждого юнита.
- `LateUpdate`: позиция по `WorldToScreenPoint` тактической камеры.
- Скрытие за камерой, при ходе бота, при расстановке.
- Те же правила тумана, что у миникарты.

---

### `TacticalWorldUnitIcon.cs`
**Путь:** `Assets/Scripts/TacticalWorldUnitIcon.cs`  
**Роль:** одна карточка над юнитом (портрет, рамка союзник/враг).

**Что делает:** клик/hover → переход в экшен или подсветка цели.

---

### `BotTurnTacticalOverlay.cs`
**Путь:** `Assets/Scripts/BotTurnTacticalOverlay.cs`  
**Роль:** визуализация хода бота на тактике.

**Что делает:** линия пути, маркер цели, вспышка атаки — без смещения камеры.

---

## Кубик и расстановка армии

### `DiceRenderUI.cs`
**Путь:** `Assets/Scripts/DiceRenderUI.cs`  
**Роль:** **3D-кость** в интерфейсе.

**Что делает:**

- Отдельная камера рендерит кость в `RenderTexture` на UI.
- Подписка на `GameManager.OnDiceRolled` — анимация вращения и остановка на значении.
- Клик / Space — бросок в тактическом ходу игрока.
- Скрывается в экшене, расстановке, паузе.

---

### `ArmyDeploymentController.cs`
**Путь:** `Assets/Scripts/ArmyDeploymentController.cs`  
**Роль:** **расстановка армии** перед боем (PvBot).

**Что делает:**

- Бюджет очков, прямоугольная зона на сетке (`deploymentZoneMin/Max`).
- Список `DeploymentOfferEntry`: префаб, стоимость, лимит копий.
- Drag-and-drop карточек на поле; возврат юнита в руку.
- Правила: минимум один юнит, обязательный король, Confirm → `GameManager.CompleteArmyDeploymentPhase`.
- Может сам создать минимальный UI, если не назначен в инспекторе.
- Включает камеру расстановки, скрывает тактические иконки.

**Структура `DeploymentOfferEntry`:** в том же файле — данные одной карточки в каталоге.

---

### `DeploymentCardUI.cs`
**Путь:** `Assets/Scripts/DeploymentCardUI.cs`  
**Роль:** UI-карточка юнита в руке.

**Что делает:** drag на сетку, ghost при перетаскивании, показ инфо-панели при наведении.

---

### `DeploymentUnitInfoPanelUI.cs`
**Путь:** `Assets/Scripts/DeploymentUnitInfoPanelUI.cs`  
**Роль:** правая панель статов/способности при выборе карточки.

**Что делает:** текст из `UnitAbilityDisplayTexts`, стоимость, имя класса.

---

### `DeploymentPlacedUnitMarker.cs`
**Путь:** `Assets/Scripts/DeploymentPlacedUnitMarker.cs`  
**Роль:** снятие юнита с поля обратно в пул.

**Что делает:** правый клик по поставленному юниту → возврат очков и карточки.

---

## QTE и способности

### `QTESystem.cs`
**Путь:** `Assets/Scripts/QTESystem.cs`  
**Роль:** логика **Quick Time Event** при атаке врага.

**Что делает:**

- С шансом (`qteChance`) при атаке по игроку запускает окно реакции.
- Последовательность из 3 клавиш (Z, X, C); `Time.timeScale = 0` на время ввода.
- Успех — полный блок урона, сброс атаки врага; провал — урон как обычно.
- `WasAttackBlocked` для `WeaponCollider`.
- Скрывает `ActionModeUI`, показывает `QTEManager`, звуки `AudioManager`.

**Типы:** `QTEResult`, `QTEKey`, `QTECapabilities` (все юниты могут блокировать).

---

### `QTEManager.cs`
**Путь:** `Assets/Scripts/QTEManager.cs`  
**Роль:** только **UI** QTE.

**Что делает:** панель, подсветка нужной клавиши, таймер, прогресс последовательности.

---

### `AbilitySystem.cs`
**Путь:** `Assets/Scripts/AbilitySystem.cs`  
**Роль:** глобальный учёт кулдаунов способностей (singleton).

**Что делает:**

- Список всех `UnitAbilities` на сцене.
- `OnTurnSwitch` — уменьшение CD на 1 ход у всех.

---

### `UnitAbilities.cs`
**Путь:** `Assets/Scripts/UnitAbilities.cs`  
**Роль:** способности **по классу** в экшен-режиме (клавиша Q).

| Класс | Способность | CD (ходов) |
|-------|-------------|------------|
| Horse | Скачок к точке взгляда | 1 |
| Bishop | Отражение урона | 3 |
| Guardian | Снижение урона 50% | 3 |
| Queen | +20% урона | 5 |
| King | Лечение союзника под прицелом | 4 |
| Pawn | нет активной | — |

**Что делает:** проверка режима, паузы, кулдауна; вызов логики на `Unit`; VFX через `AbilityVisualEffects`.

---

### `AbilityVisualEffects.cs`
**Путь:** `Assets/Scripts/AbilityVisualEffects.cs`  
**Роль:** частицы и звуки способностей.

**Что делает:** one-shot эффекты; постоянные индикаторы (щит, отражение, бафф).

---

### `UnitAbilityDisplayTexts.cs`
**Путь:** `Assets/Scripts/UnitAbilityDisplayTexts.cs`  
**Роль:** статические строки для UI (название, описание, CD).

**Зачем:** один источник текстов для `ActionModeUI` и панели расстановки.

---

## Бот и миссионный ИИ

### `IBotMissionBrain.cs`
**Путь:** `Assets/Scripts/BotBrains/IBotMissionBrain.cs`  
**Роль:** контракт **миссионного** ИИ.

**Методы:**

- `CanRun()` — есть ли на сцене объекты этой миссии.
- `PickActingUnitBeforeDice` — какой юнит бота ходит.
- `ExecuteTurn` — стратегия хода; в конце обязан завершить ход через `BotController`.

**Реализации:** `Mission1BotBrain`, `Mission2BotBrain`.

---

## Миссии: общее

### `MissionObjectivesConfig.cs`
**Путь:** `Assets/Scripts/Mission/MissionObjectivesConfig.cs`  
**Роль:** ScriptableObject — тексты целей миссии.

**Поля:** заголовок панели, массив строк задач.

---

### `MissionQuestUI.cs`
**Путь:** `Assets/Scripts/Mission/MissionQuestUI.cs`  
**Роль:** панель квестов на HUD (singleton).

**Что делает:**

- `SetProgress(index, current, max)` — счётчик (например 2/3 флага).
- `Complete(index)` — зачёркивание выполненной цели.
- Скрывается при расстановке, паузе, game over, иногда при ходе бота.

**Связь:** менеджеры миссий 1/2 обновляют индексы; asset задаётся в `MissionDefinition` или на сцене.

---

## Миссия 1 — «Прорыв»

### `Mission1FlagZone.cs`
**Путь:** `Assets/Scripts/Mission1/Mission1FlagZone.cs`  
**Роль:** одна точка захвата (триггер).

**Что делает:**

- Внутри зоны юнит Player1/Player2 → смена владельца.
- Владелец **сохраняется**, если зона пустая (не сбрасывается).
- Опционально цвет кольца на земле.

---

### `Mission1CaptureManager.cs`
**Путь:** `Assets/Scripts/Mission1/Mission1CaptureManager.cs`  
**Роль:** условия **победы/поражения** миссии 1.

**Что делает:**

- Следит за 3 флагами; победа — все у Player1 **или** все враги мертвы.
- Поражение — нет живых юнитов игрока (не во время расстановки).
- Обновляет квесты в `MissionQuestUI`; вызывает `GameManager.EndGame`.
- Может создать флаги на сцене, если не назначены.

---

### `Mission1BotObjectiveProvider.cs`
**Путь:** `Assets/Scripts/Mission1/Mission1BotObjectiveProvider.cs`  
**Роль:** выбор **приоритетного флага** для бота.

**Что делает:** скоринг: флаг у игрока, угроза, дистанция до бота → `SelectTargetFlag()`.

---

### `Mission1BotBrain.cs`
**Путь:** `Assets/Scripts/Mission1/Mission1BotBrain.cs`  
**Роль:** ИИ бота в миссии 1 (`IBotMissionBrain`).

**Что делает:**

- `CanRun` — на сцене есть флаги.
- Выбор юнита бота ближе к важным флагам; атака в радиусе; движение к захвату/перехвату.
- Ротация юнитов, чтобы не ходил один и тот же.

---

### `TacticalFlagMarker.cs`
**Путь:** `Assets/Scripts/Mission1/TacticalFlagMarker.cs`  
**Роль:** буква A/B/C на тактическом UI для флага.

---

### `TacticalFlagMarkersController.cs`
**Путь:** `Assets/Scripts/Mission1/TacticalFlagMarkersController.cs`  
**Роль:** позиционирование маркеров флагов на экране.

**Что делает:** привязка к `Mission1FlagZone`, цвет по владельцу, скрытие в экшен-режиме.

---

## Миссия 2 — «Осада»

*Скрипты в namespace `Mission2`.*

### `DestructibleObjective.cs`
**Путь:** `Assets/Scripts/Mission2/DestructibleObjective.cs`  
**Роль:** разрушаемая цель (здание, объект).

**Что делает:** HP, `ApplyDamage`, VFX; событие уничтожения; метка на тактике (O1, O2…); урон блокируется активным щитом.

---

### `Mission2ObjectiveShield.cs`
**Путь:** `Assets/Scripts/Mission2/Mission2ObjectiveShield.cs`  
**Роль:** защитный купол на цели.

**Что делает:** пока активен — урон по objective не проходит; `SetProtectionActive` после взлома терминала.

---

### `Mission2DefenseTerminal.cs`
**Путь:** `Assets/Scripts/Mission2/Mission2DefenseTerminal.cs`  
**Роль:** терминал взлома.

**Что делает:**

- Игрок (не танк) в экшене в радиусе + **E** → взлом, снятие щитов, квест выполнен.
- Метка на тактической карте; смена спрайта после взлома.

---

### `Mission2DestructionManager.cs`
**Путь:** `Assets/Scripts/Mission2/Mission2DestructionManager.cs`  
**Роль:** победа миссии 2.

**Что делает:** когда все `DestructibleObjective` уничтожены → `EndGame`; прогресс квеста.

---

### `TankController.cs`
**Путь:** `Assets/Scripts/Mission2/TankController.cs`  
**Роль:** управление **танком**.

**Что делает:**

- Игрок: движение корпуса относительно камеры, башня и ствол мышью, выстрел ЛКМ по целям.
- Бот: NavMesh, стрельба по objectives.
- Урон по objectives с учётом щитов; расход бюджета хода через `Unit`.

---

### `Mission2BotBrain.cs`
**Путь:** `Assets/Scripts/Mission2/Mission2BotBrain.cs`  
**Роль:** ИИ бота в миссии 2 (`IBotMissionBrain`).

**Что делает:**

- Guardian бьёт танк; остальные давят пехоту игрока у танка.
- Без цели — движение к objectives / терминалу / флангу.
- Если танк игрока уничтожен — победа игрока.

---

### `TacticalMission2Marker.cs` / `TacticalMission2MarkersController.cs`
**Путь:** `Assets/Scripts/Mission2/`  
**Роль:** подписи целей и терминала на тактике (как флаги в миссии 1).

**Что делает:** цвет: objective под щитом / открыт; терминал взломан / активен.

---

## Хаб и выбор миссий

### `MissionDefinition.cs`
**Путь:** `Assets/Scripts/Hub/MissionDefinition.cs`  
**Роль:** ScriptableObject — одна миссия в каталоге.

**Поля:** id, название, краткая цель, превью, имя сцены для загрузки, ссылка на `MissionObjectivesConfig`.

**Примеры asset:** `Assets/Data/Missions/Mission1.asset` («Прорыв»), `Mission2.asset` («Осада»).

---

### `MissionSelectUI.cs`
**Путь:** `Assets/Scripts/Hub/MissionSelectUI.cs`  
**Роль:** полноэкранный выбор миссии (singleton).

**Что делает:**

- Карусель миссий: стрелки, превью, описание.
- «Выбрать» → `SceneManager.LoadScene(sceneName)`.
- `Show`/`Hide`, разблокировка курсора; при закрытии в хабе — снова FPS.

---

### `HubComputerTerminal.cs`
**Путь:** `Assets/Scripts/Hub/HubComputerTerminal.cs`  
**Роль:** терминал на базе.

**Что делает:** игрок рядом + **E** в режиме экшена → открыть `MissionSelectUI`; подсказка через `HubInteractionHintUI`.

---

### `HubInteractionHintUI.cs`
**Путь:** `Assets/Scripts/Hub/HubInteractionHintUI.cs`  
**Роль:** текст «Нажмите E» (singleton).

---

### `HubSceneBootstrap.cs`
**Путь:** `Assets/Scripts/Hub/HubSceneBootstrap.cs`  
**Роль:** старт сцены **HubBase**.

**Что делает:**

- Спавн/поиск юнита игрока, режим исследования без боя.
- Отключает бота, расстановку, кубик, тактический HUD, подсветку сетки, иконки над полем.
- Включает `CameraManager.EnterHubExploreMode`.

---

## Туман войны

Работает в **PvBot** на сценах `mission1` и `mission2`.

### `FogOfWarSettings.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogOfWarSettings.cs`  
**Роль:** ScriptableObject — размер карты тумана, радиусы/конус зрения, цвета, режим отображения.

---

### `FogOfWarManager.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogOfWarManager.cs`  
**Роль:** ядро тумана (singleton).

**Что делает:**

- Две R8-текстуры: «исследовано» и «видно сейчас».
- Штамп зрения от юнитов Player1 и от камеры в экшене.
- При загрузке миссии — настройка, exempt для целей миссии.
- На ходе бота — отдельный режим «спектатор» для честного UI.
- API: `GetCellState`, материалы для шейдеров.

---

### `FogWarVisionStamper.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogWarVisionStamper.cs`  
**Роль:** статические методы — рисование круга и конуса на CPU в текстуру.

---

### `FogOfWarOverlay.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogOfWarOverlay.cs`  
**Роль:** меш тумана на земле в **тактическом** виде.

---

### `FogOfWarActionFeature.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogOfWarActionFeature.cs`  
**Роль:** URP Renderer Feature — туман поверх кадра в тактике и экшене.

---

### `FogOfWarSceneSetup.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogOfWarSceneSetup.cs`  
**Роль:** на сцене подменяет asset настроек тумана при старте.

---

### `FogWarExempt.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogWarExempt.cs`  
**Роль:** маркер «всегда видно» (флаги, objectives, терминал).

---

### `FogWarIntelTracker.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogWarIntelTracker.cs`  
**Роль:** разведка противника для **миникарты**.

**Что делает:** запоминает, кого видели; last known position; что показывать иконке врага.

---

### `FogWarUnitVisibility.cs`
**Путь:** `Assets/Scripts/FogOfWar/FogWarUnitVisibility.cs`  
**Роль:** скрытие **3D-модели** врага в экшене, если клетка не в зоне видимости.

---

### `EnemyIntelTracker.cs`
**Путь:** `Assets/Scripts/EnemyIntelTracker.cs`  
**Роль:** обёртка совместимости — перенаправляет на `FogWarIntelTracker`.

**Зачем:** старый код мог ссылаться на это имя; логика в одном месте.

---

## Меню, звук, обучение

### `TutorialVideoManager.cs`
**Путь:** `Assets/Scripts/TutorialVideoManager.cs`  
**Роль:** карусель обучающих роликов.

**Что делает:** несколько клипов, вперёд/назад, счётчик, воспроизведение при открытии панели.

---

## Редактор Unity

### `MissionObjectivesEditorMenu.cs`
**Путь:** `Assets/Editor/MissionObjectivesEditorMenu.cs`  
**Роль:** только Editor — пункт меню **CyberGambit → Create Mission Quest Config**.

**Что делает:** создаёт новый asset `MissionObjectivesConfig` с заготовкой текстов целей.

**На защите:** это инструмент дизайнера/программиста, в билд не попадает.

---

## Карта зависимостей

### Кто от кого зависит (упрощённо)

| Система | Главные потребители |
|---------|---------------------|
| `GameManager` | UI, бот, миссии, кубик, пауза, способности |
| `CameraManager` | Unit, UI, бот, хаб, туман, расстановка |
| `ChessGrid` | Unit, подсветка, расстановка, миникарта, бот |
| `Unit` | Всё боевое + миссии + fog |
| `BotController` | GameManager, Mission*BotBrain |
| `FogOfWarManager` | UI карты, видимость моделей, бот-спектатор |
| `MissionQuestUI` | Mission1/2 managers, терминал |

### Поток данных за один ход игрока (PvBot)

1. `TacticalModeUI` — игрок бросает кубик → `GameManager.RollDiceForCurrentTurn` → `OnDiceRolled` → `DiceRenderUI`.
2. `GridHighlighter` показывает допустимую клетку → клик → `Unit.MoveToGridPosition`.
3. Клик по иконке → `CameraManager` → экшен → `ActionModeUI`.
4. Стрельба / QTE / способность Q → конец хода → `SwitchTurn` → `BotController.ExecuteBotTurn`.

---

## Типичные вопросы на защите

**Почему два режима камеры, а не одна?**  
Тактика даёт обзор поля и пошаговые решения; FPS — напряжение боя и прицеливание. `CameraManager` изолирует переключение.

**Почему «Chess» в названиях?**  
Ранний прототип использовал шахматные типы фигур для классов юнитов. Механика давно своя; переименование — техдолг.

**Как добавить третью миссию?**  
1) ScriptableObject `MissionDefinition` + сцена. 2) Менеджер победы (по образцу Capture/Destruction). 3) Опционально `IBotMissionBrain`. 4) Запись в список `MissionSelectUI`. 5) Build Settings.

**Где баланс кубика и урона?**  
`GameManager` (diceMin/Max, metersPerDicePoint), `Unit` (damage, HP), `RangedWeaponConfig`, `ArmyDeploymentController` (бюджет очков).

**Как работает туман войны технически?**  
Две текстуры 256×256 (условно), CPU-штампы зрения (`FogWarVisionStamper`), шейдеры overlay + URP feature. Состояние клетки: неизведано / исследовано / видно.

**Что делаете, если бот завис?**  
`BotController` — корутина с этапами; миссионный brain обязан вызвать конец хода. На защите можно показать `ExecuteBotTurn` в IDE.

**Чем ваш вклад как developer отличается от художника?**  
Вы — связка систем: менеджеры, UI, ИИ, fog, хаб, расстановка, QTE. Художник — модели, анимации, префабы (`Mission2Models`, VFX), на которые вешаются эти скрипты.

---

## Полный список файлов (67)

| # | Файл |
|---|------|
| 1 | `GameManager.cs` |
| 2 | `Unit.cs` |
| 3 | `CameraManager.cs` |
| 4 | `ChessGrid.cs` |
| 5 | `ChessRulesManager.cs` |
| 6 | `BotController.cs` |
| 7 | `MainMenu.cs` |
| 8 | `AudioManager.cs` |
| 9 | `ActionModeUI.cs` |
| 10 | `TacticalModeUI.cs` |
| 11 | `TacticalMapUIController.cs` |
| 12 | `TacticalMapUnitIcon.cs` |
| 13 | `TacticalWorldIconsController.cs` |
| 14 | `TacticalWorldUnitIcon.cs` |
| 15 | `TacticalUnitPresentation.cs` |
| 16 | `GridHilghlighter.cs` → класс `GridHighlighter` |
| 17 | `DiceRenderUI.cs` |
| 18 | `ArmyDeploymentController.cs` |
| 19 | `DeploymentCardUI.cs` |
| 20 | `DeploymentUnitInfoPanelUI.cs` |
| 21 | `DeploymentPlacedUnitMarker.cs` |
| 22 | `QTESystem.cs` |
| 23 | `QTEManager.cs` |
| 24 | `AbilitySystem.cs` |
| 25 | `UnitAbilities.cs` |
| 26 | `AbilityVisualEffects.cs` |
| 27 | `UnitAbilityDisplayTexts.cs` |
| 28 | `Weapon.cs` |
| 29 | `WeaponCollider.cs` |
| 30 | `RangedWeaponConfig.cs` |
| 31 | `HandIKController.cs` |
| 32 | `CameraShake.cs` (VFX) |
| 33 | `BotBrains/IBotMissionBrain.cs` |
| 34 | `BotTurnTacticalOverlay.cs` |
| 35 | `EnemyIntelTracker.cs` |
| 36 | `TutorialVideoManager.cs` |
| 37 | `UnitColorController.cs` |
| 38 | `Mission/MissionObjectivesConfig.cs` |
| 39 | `Mission/MissionQuestUI.cs` |
| 40 | `Mission1/Mission1FlagZone.cs` |
| 41 | `Mission1/Mission1CaptureManager.cs` |
| 42 | `Mission1/Mission1BotObjectiveProvider.cs` |
| 43 | `Mission1/Mission1BotBrain.cs` |
| 44 | `Mission1/TacticalFlagMarker.cs` |
| 45 | `Mission1/TacticalFlagMarkersController.cs` |
| 46 | `Mission2/DestructibleObjective.cs` |
| 47 | `Mission2/Mission2ObjectiveShield.cs` |
| 48 | `Mission2/Mission2DefenseTerminal.cs` |
| 49 | `Mission2/Mission2DestructionManager.cs` |
| 50 | `Mission2/TankController.cs` |
| 51 | `Mission2/Mission2BotBrain.cs` |
| 52 | `Mission2/TacticalMission2Marker.cs` |
| 53 | `Mission2/TacticalMission2MarkersController.cs` |
| 54 | `Hub/MissionDefinition.cs` |
| 55 | `Hub/MissionSelectUI.cs` |
| 56 | `Hub/HubComputerTerminal.cs` |
| 57 | `Hub/HubInteractionHintUI.cs` |
| 58 | `Hub/HubSceneBootstrap.cs` |
| 59 | `FogOfWar/FogOfWarSettings.cs` |
| 60 | `FogOfWar/FogOfWarManager.cs` |
| 61 | `FogOfWar/FogWarVisionStamper.cs` |
| 62 | `FogOfWar/FogOfWarOverlay.cs` |
| 63 | `FogOfWar/FogOfWarActionFeature.cs` |
| 64 | `FogOfWar/FogOfWarSceneSetup.cs` |
| 65 | `FogOfWar/FogWarExempt.cs` |
| 66 | `FogOfWar/FogWarIntelTracker.cs` |
| 67 | `FogOfWar/FogWarUnitVisibility.cs` |
| + | `Editor/MissionObjectivesEditorMenu.cs` |

---

*Связанные документы: `PROTOTYPING_STAGES.md` (этапы по коммитам), `Этапы_прототипирования_CyberGambit.txt` (текст для отчёта), планы в `Assets/Plans/`.*
