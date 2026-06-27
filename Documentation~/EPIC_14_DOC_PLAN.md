# Epic 14 — Documentation overhaul: план виконання

Цей файл — повний план роботи над документацією для v0.9.0. Не заміна PLAN.md, а операційний рівень: що писати, де, скільки деталей, як писати.

---

## 0. Правила написання (обов'язково читати перед стартом)

### 0.1 Головне правило

Документація **не має виглядати як AI-згенерований текст**. Це не стилістична примха — це читабельність. AI-текст відштовхує технічну аудиторію, бо вони бачать його щодня і впізнають миттєво.

### 0.2 Заборонені конструкції

**Тире (—) заборонені в будь-якому контексті.** Ніяких em-dash. Якщо хочеться вставити уточнення через тире — переписати речення.

Погано:
```
This action modifies the texture importer — it does not touch the asset file itself.
```
Добре:
```
This action modifies the texture importer. It does not touch the asset file on disk.
```

**Заборонені фрази-наповнювачі:**
- "It's worth noting that..."
- "It's important to understand that..."
- "Please note that..."
- "This is a powerful feature that..."
- "In order to..."
- "Utilize" (замість "use")
- "Additionally," / "Furthermore," / "Moreover," на початку речення
- "A variety of" / "a number of"
- "This allows you to..."
- "Simply..." (особливо "simply add", "simply click")

**Заборонений стиль:**
- Пасивний стан де є активна альтернатива ("the asset is moved to" → "the postprocessor moves the asset to")
- Речення з трьох частин через кому замість двох окремих речень
- Bullet-списки з 7+ пунктів без групування (максимум 5, решта — в підзаголовки)

### 0.3 Як писати добре

**Говори від конкретного класу або кнопки.** Не "the system processes the asset" — а "OnPostprocessAllAssets moves the file and runs the action chain".

**Пиши від першої особи дії, не від системи.** "Open Tools > Asset Router Settings" замість "The settings window can be opened via Tools > Asset Router Settings".

**Якщо є обмеження — скажи прямо і одразу.** "This action works only on 16-bit PCM WAV files." Не "this action may have limitations with certain audio formats".

**Код у тексті завжди в backticks.** Назви класів, методів, полів — завжди. `AssetImportActionAsset`, `CanRunOn`, `targetFolder`.

**Приклади — конкретні.** Не "for example, a texture named according to your convention" — а "for example, a file named `T_Rock_D.png`".

---

## 1. Scope: що документувати детально, що пропустити

### 1.1 Детально документувати

| Тема | Причина |
|------|---------|
| `IAssetImportAction` / `AssetImportActionAsset` | Це публічний extension point. Розробники, які пишуть власні actions, мають знати контракт точно. |
| Кожен з 10 built-in actions | Без цього impossible зрозуміти чи action підходить для задачі. |
| Glob pattern syntax | Найчастіше питання нових користувачів. |
| Весь постпроцесор flow (від drop до move) | Треба знати "що відбувається" щоб дебажити. |
| JSON export/import | Нетривіальний workflow з caveat (portability, fileId). |
| Migration v1 to v2 | Автоматична і незворотня — користувачі мусять знати це заздалегідь. |
| `AssetImportContext` struct | Передається в кожен action, треба знати поля. |
| Dry Run tab | Непочатковий flow, але дуже важливий для команди. |
| History tab і Undo | Тут є caveat: тільки moves, не settings changes. |

### 1.2 Не документувати або мінімально

| Тема | Причина пропуску |
|------|-----------------|
| `PatternMatcher` internals | `internal static`. Розробник не торкається напряму. |
| `RuleValidator` internals | Те саме. |
| `DatabaseLocator` | `internal static`. |
| `DiagnosticLog` internals | `internal`. Достатньо документації вікна. |
| `RuleStatsStore` internals | `internal`. Статистика показується у вікні автоматично. |
| `OperationLog` internals | `internal`. |
| `BatchMover` / `UndoEngine` internals | `internal`. |
| `DefaultDatabaseFactory` | `internal`. |
| Весь `Editor/Data/` крім публічних полів | Серіалізовані поля документуються через UI-пояснення, не через internals. |
| Тести (`Tests/`) | Тести документуються тільки в `testing-your-actions.md` як зразок. |

### 1.3 XMLDoc: що потрібно

XMLDoc потрібен на:
- `IAssetImportAction` і обидва методи
- `AssetImportActionAsset` (class-level doc)
- `AssetImportContext` (всі поля і constructor)
- `AssetCatalog` (class + entries field)
- `BaseImportRule` (всі публічні поля: ruleName, isEnabled, patternMode, pattern, matchAgainstFullPath, targetFolder, scopeFolder)
- `ImportRule` (preset, postImportActions)
- `ImporterSettingsDatabase` (всі публічні поля: enableAutoImport, showPopupForUnknownFiles, monitoredExtensions, ignoredFolders, rules)
- `PatternMode` enum і обидва значення (Glob, Regex)
- `IAssetRouterPrefabSetup` (Runtime)
- `IAssetRouterDataSetup` (Runtime)
- Кожен built-in action клас (короткий summary + поля)

XMLDoc не потрібен на internal класах — це не UPM convention. Достатньо markdown docs.

---

## 2. Список файлів для створення

### 2.1 Структура виводу

```
Documentation~/
  index.md                          (новий, UPM entry point)
  DOCUMENTATION_EN.md               (новий, основна EN документація)
  DOCUMENTATION_UA.md               (rename з DOCUMENTATION.md)
  TECH_KNOWLEDGE_BASE.md            (не чіпаємо, внутрішній)
  TEST.md                           (не чіпаємо, manual checklist)
  PLAN.md                           (не чіпаємо)
  EPIC_14_DOC_PLAN.md               (цей файл)
  actions/
    README.md                       (індекс actions)
    SetPivotAction.md
    TrimAudioSilenceAction.md
    AppendToCatalogAction.md
    RegisterAddressableAction.md
    EmitUnityEventAction.md
    CreatePrefabFromTemplateAction.md
    CreateScriptableObjectFromTemplateAction.md
    CreateMaterialFromTextureAction.md
    GenerateSpritePhysicsShapeAction.md
    GenerateNineSliceBordersAction.md
    CreateTilePaletteEntryAction.md
    LegacySamples.md                (про GenerateMeshCollider + RunMenuItem у Samples~)
  api/
    extension-points.md             (як написати власний action)
  migrations/
    v1-to-v2-schema.md
  use-cases/
    mobile-team.md
    legacy-cleanup.md
    solo-developer.md
  testing-your-actions.md

packages/com.kodlon.assetrouter/
  README.md                         (оновити до EN)
  CONTRIBUTING.md                   (новий)
  RELEASE_CHECKLIST.md              (новий)

Samples~/QuickStart/
  README.md                         (оновити до EN, додати use-case секції)

Tests/Actions/
  _ExampleActionTest.cs             (новий, exemplar — навмисно прокоментований)
```

---

## 3. Детальний план по файлах

### 3.1 `Documentation~/index.md`

**Об'єм:** 20-30 рядків.

**Зміст:**
- Одне речення що таке AssetRouter і для чого.
- Посилання на DOCUMENTATION_EN.md (getting started).
- Посилання на `actions/README.md` (action reference).
- Посилання на `api/extension-points.md` (write your own action).
- Посилання на CHANGELOG.md.

**Не включати:** архітектурний опис, списки файлів, технічні деталі. Це entry point, не довідник.

---

### 3.2 `Documentation~/DOCUMENTATION_EN.md`

**Об'єм:** ~400-500 рядків. Переклад і реструктуризація існуючої `DOCUMENTATION.md`.

**Структура:**

```
# Asset Router

## What it does
## How assets move through the system
## Getting started
## The Settings window
  ### Rules list
  ### Rule fields
  ### Pattern syntax (Glob and Regex)
  ### Target folder
  ### Scope folder
  ### Import preset
  ### Post-import actions
## Dry Run tab
## History tab and Undo
## Validate tab
## Diagnostic Window
## JSON export and import
## General settings
  ### Monitored extensions
  ### Ignored folders
  ### Auto-import toggle
  ### Popup for unknown files
## Default rules
## Troubleshooting
```

**Деталі по секціях:**

**"How assets move through the system"** — пояснити flow коротко і точно. Не абстрактно — послідовність: Unity fires `OnPreprocessAsset` -> preset applied -> `OnPostprocessAllAssets` -> `ShouldProcess` check -> rule match -> `MoveAsset` -> action chain runs. Без підпунктів. Один абзац або два.

**"Pattern syntax"** — найважливіша технічна секція. Детально:
- Glob mode: `*` matches any chars except `/`, `?` matches one char except `/`, `**` matches any path segment including `/`.
- Regex mode: standard .NET regex, case-insensitive.
- `matchAgainstFullPath`: чекбокс який вмикає match по повному шляху замість filename. Пояснити коли треба (`Assets/**` pattern).
- Caveat: `Assets/**` matches direct children теж (`Assets/x.png` матчиться), бо `**` транслюється в `.*`.
- Live preview: що показує (3 файли з проєкту або regex error).
- Timeout: якщо regex катастрофічний (ReDoS) — матч завершується через 50ms і повертає false.

**"Dry Run"** — пояснити workflow: Scan -> таблиця -> Select -> Apply Selected. Пояснити "Force re-import" toggle (навіщо якщо файл вже там).

**"History and Undo"** — важливий caveat: undo повертає тільки file moves. Зміни importer settings (preset applied) НЕ скасовуються. Тобто "undo" = файл повертається на старий шлях, але preset залишається застосованим.

**"JSON export and import"** — preamble: JSON не замінює SO, це додатковий формат для git diff і sharing між машинами. Caveat: post-import action references (sub-assets) прив'язані до конкретного проєкту через fileId — cross-machine sharing для actions не підтримується.

**"Troubleshooting"** — ті самі пункти що в українській версії, але конкретніші:
- "My file didn't move": перевір extension у Monitored list, перевір Ignored Folders, відкрий Diagnostic Window.
- "Rule doesn't match": перевір Pattern preview у Settings, перевір чи `matchAgainstFullPath` потрібен для твого pattern.
- "Preset not applied": перевір що пресет valid і не corrupted, перевір що preset type matches importer type.
- "Actions didn't run": перевір Diagnostic Window — moved=false означає файл вже там (треба Force re-import).

---

### 3.3 `Documentation~/actions/README.md`

**Об'єм:** ~60 рядків.

Таблиця всіх 10 actions (+ 2 legacy в Samples~) з колонками: Action / Applies to / What it does / Tier.

Коротке пояснення tier system (A через G) з одним реченням на tier. Не описовий текст — таблиця і tier legend достатньо.

Після таблиці: один абзац про те, що кожен action — окрема markdown сторінка з прикладом і edge cases.

---

### 3.4 Action pages (`Documentation~/actions/<Name>.md`)

**Шаблон для кожної сторінки:**

```markdown
# <ActionName>

<1-sentence what it does, specific>

**Applies to:** <asset types — e.g. "PNG, JPG, TGA (any texture file)">

**Tier:** <letter> — <what architectural pattern this demonstrates>

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|

## How it works

<2-4 sentences describing the execution. Name Unity APIs used. No abstraction.>

## Idempotency

<Yes or No. If Yes, explain why running it twice has no side effect.
If No, explain what happens on second run.>

## Requirements

<List only if there are real prerequisites, e.g. "Read/Write enabled on texture", 
"com.unity.addressables installed".>

## Edge cases

<Concrete edge cases the user will actually hit. Not theoretical.>

## Example

<Real production scenario: what asset, what rule, what the action does.>

## See also

<Links to related actions or docs if relevant.>
```

**Деталі по конкретних actions:**

#### `SetPivotAction.md`

Ключовий момент: action змінює `TextureImporterSettings.spritePivot`, потім запускає `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`. Це re-import. Тому action запускається ПІСЛЯ першого re-import'а (в постпроцесорі після move). Другий re-import — це нормально, але слід знати.

Edge case: якщо texture не є Sprite type — action сетить поле, але воно ні на що не впливає (no error, no warning).

#### `TrimAudioSilenceAction.md`

Найбільша сторінка. Деталі, які треба задокументувати:
- Тільки WAV, тільки 16-bit PCM. RIFX (big-endian), non-PCM, non-16bit — action повертає false, файл не чіпається.
- `silenceThreshold` (0..1, default 0.01): доля від максимальної амплітуди 16-bit (`short.MaxValue`) нижче якої семпл вважається тишею.
- Результат: файл замінюється (атомарно через `.tmp` + `File.Replace`). Undo через History tab поверне файл назад, але тільки move — не попередній вміст WAV.
- Re-entry guard: якщо trim запустив re-import і той re-import знову тригерить цей action — action виявляє re-entry і пропускає.

Edge cases:
- Весь файл — тиша: action повертає false, файл не чіпається.
- Немає тиші для trim: action повертає false, файл не чіпається.
- Stereo: обидва канали тестуються на тишу. Trim відбувається тільки якщо обидва канали тихі.

#### `AppendToCatalogAction.md`

Пояснити `AssetCatalog` SO (Create > Asset Router > Asset Catalog). Ідемпотентний через `List.Contains`. Caveat: `List.Contains` — O(N), на каталозі 10k+ entries буде помітне гальмо.

#### `RegisterAddressableAction.md`

Починається з: "This action is compiled only when `com.unity.addressables >= 1.19.0` is installed. Without that package, the action class does not exist in the build."

Пояснити `UNITY_ADDRESSABLES` define symbol і як він виставляється через `versionDefines` в .asmdef.

Field: `groupName` — якщо group з таким іменем не існує, `CreateOrMoveEntry` створить default group entry. Пояснити цю поведінку.

Caveat: `SaveAssets` НЕ викликається після кожного asset — тільки `SetDirty`. Save відбувається після завершення всього post-import batch. Якщо Unity крашнеться до save — group changes можуть не зберегтись.

#### `EmitUnityEventAction.md`

Пояснити що `UnityEvent<Object>` — це Inspector-configurable callbacks. Розробнику не треба писати код: просто тягти в `On Import` слот будь-який компонент/метод.

`CanRunOn` повертає false якщо `GetPersistentEventCount() == 0` — тобто action мовчки пропускається якщо ніхто не підписаний.

#### `CreatePrefabFromTemplateAction.md`

Це showpiece action — найдетальніша сторінка. Пояснити:

Flow: `InstantiatePrefab(template)` -> `GetComponentsInChildren<IAssetRouterPrefabSetup>()` -> `SetupAssetRouter(importedAsset, assetPath)` на кожному -> `SaveAsPrefabAsset(instance, outputPath)` -> `DestroyImmediate(instance)`.

`namePattern`: підтримує `{assetName}` токен. Наприклад pattern `{assetName}_Prefab` для asset `Character.fbx` дасть `Character_Prefab.prefab`.

`overwriteExisting`: false за замовчуванням — якщо prefab вже існує, action пропускається без помилки.

`IAssetRouterPrefabSetup`: інтерфейс з Runtime assembly, тому можна implement в будь-якому MonoBehaviour без Editor залежностей. Це розрив Editor/Runtime межі навмисно — callback дозволяє налаштувати prefab з дефолтними значеннями.

#### `CreateScriptableObjectFromTemplateAction.md`

Аналогічно до Prefab action але для SO. `IAssetRouterDataSetup`. `Instantiate(template)` копіює всі серіалізовані поля — тобто template може мати preloaded дефолти.

#### `CreateMaterialFromTextureAction.md`

`textureProperty`: shader property name (наприклад `_MainTex` для Standard shader, `_BaseMap` для URP Lit). Треба знати shader. Якщо property name неправильний — material створюється але текстура не присвоюється (no error).

#### `GenerateSpritePhysicsShapeAction.md`

Requires Read/Write enabled — без цього `GetPixels()` кидає exception і action fails. Це must-have requirement.

Pixel scan: знаходить bounding box всіх пікселів де alpha > threshold. Результат — прямокутник, не точний контур спрайта. Для точного контуру треба Physics Shape generation в Unity importer settings.

#### `GenerateNineSliceBordersAction.md`

Requires Read/Write. Сканує з кожного краю текстури поки знаходить перший non-transparent піксель. Результат: `TextureImporter.spriteBorder = Vector4(left, bottom, right, top)`.

Edge case: якщо вся текстура opaque (немає transparent країв) — всі 4 border = 0. Тобто 9-slice не ламається, просто немає рамки.

#### `CreateTilePaletteEntryAction.md`

Шукає перший Sprite sub-asset в імпортованому файлі. Якщо sub-assets немає (файл не Sprite) — action fails. `CanRunOn` перевіряє лише тип (Texture2D), не наявність sub-assets — тому fail може статись в `Execute`.

---

### 3.5 `Documentation~/api/extension-points.md`

**Об'єм:** ~200 рядків.

**Зміст:**

1. Короткий вступ: два public types що треба знати — `IAssetImportAction` і `AssetImportActionAsset`.

2. Повний контракт з кодом:
```csharp
public interface IAssetImportAction
{
    bool CanRunOn(Object importedAsset, AssetImportContext ctx);
    void Execute(Object importedAsset, AssetImportContext ctx);
}

public abstract class AssetImportActionAsset : ScriptableObject, IAssetImportAction { ... }
```
Пояснити чому ScriptableObject: дозволяє шарити action між rules, кожен action має власний Inspector.

3. `AssetImportContext` struct — всі поля:
   - `string AssetPath` — asset path з `/` (Unity convention)
   - `BaseImportRule Rule` — rule що triggered цей action
   - `ImporterSettingsDatabase Database` — вся база правил (рідко потрібна, але є)
   - `ILogger Logger` — для logging без прямого `Debug.Log`, testable

4. `CanRunOn` — не просто type check. Пояснити що це gate: якщо returns false, Execute не викликається. Використовувати для: type guard, field validity check, guard проти re-entry.

5. `Execute` — de facto порядок викликів: action запускається ПІСЛЯ `MoveAsset`, тобто `ctx.AssetPath` — вже новий шлях.

6. Приклад мінімального action:
```csharp
[CreateAssetMenu(menuName = "Asset Router/Actions/Log Asset Name")]
public class LogAssetNameAction : AssetImportActionAsset
{
    public override bool CanRunOn(Object asset, AssetImportContext ctx)
        => asset != null;

    public override void Execute(Object asset, AssetImportContext ctx)
        => ctx.Logger.Log($"Imported: {ctx.AssetPath}");
}
```

7. Де розмістити `.asset` файл action: як sub-asset всередині database .asset. Пояснити workflow через UI (кнопка `+` у rule detail).

8. Error isolation: якщо `Execute` кидає exception — `ActionPipeline` ловить його, логує через `Debug.LogException` і продовжує до наступного action. Ланцюг не переривається.

9. Посилання на `testing-your-actions.md`.

---

### 3.6 `Documentation~/migrations/v1-to-v2-schema.md`

**Об'єм:** ~60 рядків.

Тільки факти:
- v1 schema: три поля `prefix`, `suffix`, `extensionFilter`.
- v2 schema: одне поле `pattern` (glob або regex) + `patternMode`.
- Міграція відбувається автоматично при першому завантаженні Database в Unity Editor. Не потрібно жодних дій.
- Результат: `prefix="T_"`, `suffix=""`, `extensionFilter=".png"` → `pattern="T_*.png"`, `patternMode=Glob`.
- Незворотня: після migrate SO зберігається з новою схемою. Downgrade неможливий.
- `schemaVersion` на SO: до migrate = 0 або 1, після = 2. Не редагувати вручну.

---

### 3.7 `Documentation~/use-cases/mobile-team.md`

**Об'єм:** ~120 рядків.

Scenario: 4-person mobile team, Unity 2D project. Тех-арт + 2 арт + програміст.

Конкретні налаштування:
- Rules для UI sprites (`UI_*` → `Assets/Art/UI/` + TextureImporter_Sprite preset + SetPivot action center)
- Rules для normal maps (`NM_*` → `Assets/Art/NormalMaps/` + TextureImporter_NormalMap preset)
- scopeFolder: Artists кидають файли в `Assets/Import/Artist1/` і `Assets/Import/Artist2/` — одне ім'я `T_*` маршрутується по-різному залежно від scopeFolder.
- JSON export в git: команда зберігає `database.json` в репо, кожен дев Import → SO.
- Addressables: `UI_*` rule має `RegisterAddressableAction` з group "UI".

---

### 3.8 `Documentation~/use-cases/legacy-cleanup.md`

**Об'єм:** ~100 рядків.

Scenario: проєкт 2 роки в роботі, 3000 assets в `Assets/`, нема структури.

Workflow:
1. Встановити AssetRouter.
2. Відкрити Settings, налаштувати rules під наявні naming patterns.
3. Dry Run tab -> Scan project -> переглянути таблицю.
4. Відмітити тільки ті записи що виглядають правильно -> Apply Selected.
5. Якщо щось пішло не так -> History tab -> Undo.
6. Повторювати малими batch'ами поки порядок.

Важливий момент: Dry Run показує тільки monitored extensions. Файли з розширеннями поза списком (`monitoredExtensions`) — не показуються і не рухаються.

---

### 3.9 `Documentation~/use-cases/solo-developer.md`

**Об'єм:** ~80 рядків.

Scenario: соло, 2D мобільна гра, ~500 assets.

Мінімальний setup: змінити тільки targetFolder у default rules під свій проєкт. Більше нічого.

Чому без JSON export: для одного розробника SO в git достатньо.

Коли додавати actions: не з першого дня. Тільки коли той самий ручний крок (наприклад завжди встановлюєш pivot = center для UI спрайтів) виконуєш кілька разів в тиждень.

---

### 3.10 `Documentation~/testing-your-actions.md`

**Об'єм:** ~150 рядків.

Tutoral-сторінка. Три секції:

**Секція 1: Налаштування тестового проєкту**

```
Tests/
  YourPackage.Tests.asmdef    (references: AssetRouter.Editor, nunit.framework.dll)
  YourActionTests.cs
```

Показати мінімальний `.asmdef` JSON з потрібними references.

**Секція 2: Базовий тест action**

Повний приклад для `CanRunOn` тесту і `Execute` тесту. Показати як створити `AssetImportContext` вручну:

```csharp
var ctx = new AssetImportContext(
    assetPath: "Assets/Art/T_Rock_D.png",
    rule: new ImportRule { ruleName = "Test Rule" },
    database: ScriptableObject.CreateInstance<ImporterSettingsDatabase>(),
    logger: Debug.unityLogger
);
```

Показати як інстанціювати action через `ScriptableObject.CreateInstance<T>()`.

**Секція 3: Тест idempotency і error isolation**

Idempotency: викликати `Execute` двічі на одному asset, перевірити що стан однаковий.

Error isolation: тест pipeline: додати action що кидає exception і дочірній action що має виконатись попри помилку. Посилання на `_ExampleActionTest.cs`.

---

### 3.11 `Tests/Actions/_ExampleActionTest.cs`

**Об'єм:** ~80 рядків.

Це не production test — це template для extension authors. Навмисно прокоментований (виняток з правила "без коментарів").

Тестує `CreateScriptableObjectFromTemplateActionTests` або простіший `AppendToCatalogAction` — щоб приклад не вимагав фізичних файлів на диску.

Структура файлу:
- `[TestFixture]` з поясненням в коментарі "this is an example test structure for your own actions"
- `[SetUp]` — показати створення SO і context
- `[TearDown]` — cleanup через `Object.DestroyImmediate`
- 3 тести: CanRunOn false case, CanRunOn true case, Execute basic case

---

### 3.12 `README.md` (оновлення)

Поточний README відстав від реальності. Він описує prefix/suffix/extension (v0.0.1 schema) замість glob patterns. Треба оновити.

**Зміни:**
- Default rules таблиця: колонки Pattern (Glob), Target, Preset (видалити Prefix/Suffix колонки)
- "How it works" секція: замість prefix/suffix — "Each rule defines a glob or regex pattern"
- Structure блок: додати `Actions/` підпапку в `Editor/`, `Runtime/` папку
- Посилання на `Documentation~/index.md`

**Не чіпати:** загальна структура файлу, Installation секція, Quick Start кроки — вони правильні.

---

### 3.13 `CONTRIBUTING.md`

**Об'єм:** ~60 рядків.

**Зміст:**
- Як запустити тести локально (Unity → Window → General → Test Runner → Run All).
- Правила для PR:
  - Кожен PR що додає action — мусить додати `Documentation~/actions/<Name>.md`.
  - Кожен PR що змінює public API — мусить оновити XMLDoc.
  - Кожен PR — мусить мати запис у CHANGELOG.md.
  - Тести мусять бути зеленими.
- Стиль коду: internal sealed де можливо, без коментарів крім genuinely non-obvious, `.editorconfig` якщо є.

---

### 3.14 `RELEASE_CHECKLIST.md`

**Об'єм:** ~40 рядків.

Чекліст у форматі markdown checkbox list:

```markdown
## Before tagging a release

- [ ] All tests green in CI
- [ ] TEST.md manual checklist completed, no blockers
- [ ] package.json version matches the intended tag
- [ ] CHANGELOG.md has an entry for this version
- [ ] Every new public class and method has XMLDoc
- [ ] Every new built-in action has a page in Documentation~/actions/
- [ ] README.md is accurate for this version
- [ ] Samples~/QuickStart/README.md is accurate
- [ ] Documentation~/index.md links are valid
```

---

### 3.15 `Samples~/QuickStart/README.md` (оновлення)

Поточний README вже англійською і непоганий. Треба додати три секції use-case прикладів в кінці:

- "For a solo developer" — посилання на `Documentation~/use-cases/solo-developer.md` + 3 речення summary.
- "For a small team" — посилання на mobile-team.md + 3 речення.
- "For a legacy project cleanup" — посилання на legacy-cleanup.md + 3 речення.

---

## 4. Порядок виконання

### Фаза 1 — Blockers для релізу (робити першими)

1. XMLDoc на всіх публічних типах (in-code, не окремий файл)
2. `DOCUMENTATION_EN.md` — головна EN документація
3. `actions/README.md` — індекс actions
4. Всі 10 action pages у `Documentation~/actions/`
5. `api/extension-points.md`
6. `index.md`
7. Оновити `README.md`

### Фаза 2 — Important, не day-1 blockers

8. `migrations/v1-to-v2-schema.md`
9. `use-cases/mobile-team.md`
10. `use-cases/legacy-cleanup.md`
11. `use-cases/solo-developer.md`
12. `testing-your-actions.md`
13. `_ExampleActionTest.cs`

### Фаза 3 — Process docs

14. `CONTRIBUTING.md`
15. `RELEASE_CHECKLIST.md`
16. Оновити `Samples~/QuickStart/README.md`
17. Rename `DOCUMENTATION.md` → `DOCUMENTATION_UA.md`

---

## 5. Acceptance критерії

- Англомовний Unity розробник відкриває GitHub repo, читає README, встановлює пакет, відкриває Settings вікно і розуміє що робити — без пошуку у зовнішніх джерелах. Максимум 5 хвилин.
- Для кожного з 10 built-in actions є окрема сторінка з прикладом і edge cases.
- Розробник що хоче написати власний action знаходить `api/extension-points.md` і може написати робочий action без допомоги автора пакета.
- Жоден публічний клас/метод без XMLDoc.
- `RELEASE_CHECKLIST.md` існує і блокує release без документаційного gate.
- В жодному файлі документації немає em-dash (—) і заборонених фраз з розділу 0.
