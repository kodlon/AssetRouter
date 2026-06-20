# Asset Router — Roadmap

> Робочий план розвитку плагіну на основі нотаток автора + senior-bar вимог.
> Документ живе в `Documentation~/` — Unity його не імпортує, але git тримає.
> Кожен Epic — окрема мерж-одиниця: feature branch, окремий PR, окремий тег.

---

## 0. Cross-cutting вимоги (senior bar)

Застосовується до **всіх** змін нижче. Без виконання — PR не мерджиться.

### 0.1 Архітектура й API
- Публічний API — мінімальний. Внутрішні класи `internal sealed`. Те, що зовні не потрібно, не виставляти.
- Залежність на абстракції: `BaseImportRule`, нові `IImportAction` — використовувати інтерфейси/абстрактні класи; не падати в типові касти без `is`/`as`-перевірки.
- DRY: дефолти правил живуть **в одному місці** (`DefaultDatabaseFactory`), а не дублюються в `ImporterSettingsDatabase.Reset()` + `AssetRouterInitializer` (зараз дублюються з розбіжностями).
- Жодних `static` mutable полів без обґрунтування. `AssetsBeingMoved` — обґрунтовано (захист від reentrancy), але має `[InitializeOnEnterPlayMode]`/`AssemblyReloadEvents.beforeAssemblyReload` хук для скидання.

### 0.2 Серіалізація
- На кожен `[Serializable]` клас, що потрапляє в `[SerializeReference]`, **обовʼязково** `[MovedFrom(true, "old.Namespace", null, "OldClassName")]` під час будь-якого rename/move — інакше user data втрачається без попередження (підтверджено Unity Issue Tracker, проблема не виправлена в 2025).
- `[FormerlySerializedAs]` на кожне поле, що перейменовується.
- Не покладатися на `JsonUtility` для поліморфних типів — він їх не підтримує. Якщо потрібен JSON-експорт (Epic 7) — або власний writer з дискримінатором `$type`, або Newtonsoft.Json через `com.unity.nuget.newtonsoft-json`.

### 0.3 Кросплатформенність шляхів
- Усе, що йде в `AssetDatabase.*` — завжди `/` (Unity вимагає forward slash на всіх ОС).
- Усе, що йде в `System.IO.*` для абсолютних шляхів файлової системи — `Path.Combine` + `Path.DirectorySeparatorChar`.
- Перед порівнянням шляхів — нормалізація: `path.Replace('\\', '/').TrimEnd('/')`.
- Винести в `PathUtility` static class: `NormalizeAssetPath`, `ToAbsolute`, `IsUnderFolder` (case-insensitive). Сьогодні `HandleUnknownAsset` робить `Application.dataPath.Replace("Assets", "")` — це баг (зніме слово "Assets" будь-де в шляху). Виправити через `Path.GetDirectoryName(Application.dataPath)`.

### 0.4 Performance
- **Будь-який** batch flow (re-import, dry-run apply, undo) — у `try { AssetDatabase.StartAssetEditing(); ... } finally { AssetDatabase.StopAssetEditing(); }`. Без цього на 1000+ файлах різниця 100×.
- Компілювати pattern один раз на правило (glob → `Regex` з `RegexOptions.Compiled | IgnoreCase | CultureInvariant`), кешувати в самому правилі через `[NonSerialized]` поле. Не парсити в гарячому шляху `OnPreprocessAsset`.
- `RuleValidator.FindMatchingRule` — нуль алокацій у hot path: уникати `Path.GetFileName` (повертає string, але кешувати на виклик), без LINQ, без foreach over IEnumerable, лише індексований `for`.

### 0.5 Тести
- Кожен Epic — окрема група тестів у `Tests/`. Покриття нової логіки ≥ 80%.
- Edit-mode тести через NUnit (як зараз).
- Інтеграційні тести для `AssetPostprocessor` пишуться через тимчасові ассети у `Tests/Fixtures~/` + cleanup у `[TearDown]`. `Fixtures~` теж під тильдою — щоб Unity не імпортував.
- CI: GitHub Actions з `game-ci/unity-test-runner@v4` — окремий Epic в підсумку.

### 0.6 Документація
- Кожен новий публічний клас/метод — XMLDoc `///` з прикладом.
- `Documentation~/DOCUMENTATION.md` оновлюється в тому ж PR. PR без оновлення доків — блокується.
- `CHANGELOG.md` в корені пакету (UPM-конвенція, зараз відсутній) — створити в Epic 9, оновлювати в кожному релізі за форматом [Keep a Changelog](https://keepachangelog.com/).

---

## ✅ Epic 1 — Pattern matching: glob/regex замість prefix/suffix

**Нотатка автора:** прибрати `prefix`/`suffix`, замінити на одне поле glob-стилю `UI_*_Button.png`; опціонально regex для просунутих.

> ✅ **Виконано — v0.2.0**

- [x] 1.1 Чому
- [x] 1.2 Що — нові поля `patternMode`, `pattern`, `matchAgainstFullPath`
- [x] 1.3 Як (технічно) — `PatternMatcher`: glob→regex, кеш, ReDoS timeout
- [x] 1.4 Міграція даних — `RuleMigrator` v1→v2, `schemaVersion`, `[FormerlySerializedAs]`
- [x] 1.5 UI — dropdown Mode, поле Pattern, live-preview + regex error highlight
- [x] 1.6 Файли — `PatternMode.cs`, `PatternMatcher.cs`, `RuleMigrator.cs`, оновлені `BaseImportRule`, `RuleValidator`, `AssetRouterWindow`
- [x] 1.7 Acceptance

### 1.1 Чому
- Три окремі поля (`prefix`, `suffix`, `extensionFilter`) — це жорсткий частковий випадок повного glob. Один patern читається інтуїтивніше: `T_*_D.png` замість трьох полів.
- Команда мислить шаблонами імен файлів цілком, а не "що на початку / що в кінці".

### 1.2 Що
Замінити в `BaseImportRule`:
```
public string prefix;
public string suffix;
public string extensionFilter;
```
на:
```
public PatternMode patternMode;        // Glob | Regex
public string pattern;                 // "UI_*_Button.png" або "^UI_.+_Button\.png$"
public bool matchAgainstFullPath;      // false = тільки filename (default), true = весь asset path
```

### 1.3 Як (технічно)
- `Glob` парситься у `Regex`: `*` → `[^/]*`, `?` → `[^/]`, `**` → `.*`, дотики як literal, екран спецсимволів. Кеш у `[NonSerialized] Regex _compiledPattern` плюс `[NonSerialized] string _compiledFor` (інвалідація якщо pattern змінили з вікна).
- Скористатися готовою бібліотекою **тільки** якщо потрібен `[abc]`, `{a,b}`, brace expansion. Для базового `*`/`?` — самописний парсер 30 рядків, без зовнішніх залежностей. (Розглянуто [DotNet.Glob](https://github.com/dazinator/DotNet.Glob), [Corvus.Globbing](https://endjin.com/blog/2022/12/an-overview-of-the-corvus-globbing-library) — для нашого діапазону overkill.)
- `RegexOptions.Compiled | IgnoreCase | CultureInvariant`. Timeout `TimeSpan.FromMilliseconds(50)` — захист від ReDoS, якщо команда напише патологічний regex.

### 1.4 Міграція даних
- Старі правила з `prefix="T_"`, `suffix="_D"`, `extensionFilter=".png"` мають автоматично транслюватись у `pattern="T_*_D.png"`, `patternMode=Glob`.
- Реалізувати через `OnAfterDeserialize` на `BaseImportRule` АБО окремий one-shot мігратор `RuleMigrator.MigrateIfNeeded(db)` що ставить `db.schemaVersion = 2` після прогону.
- Додати поле `int schemaVersion` на `ImporterSettingsDatabase`, default `0`. У `AssetRouterInitializer` після створення — ставити поточну версію. Мігратор виконується раз і запис у Console з префіксом `[AssetRouter][Migration]`.
- На `[MovedFrom]` НЕ розраховувати для перейменування полів — `FormerlySerializedAs` працює, AЛЕ воно бере значення один-в-один, а нам треба склеювати три поля в одне. Тому окремий мігратор.

### 1.5 UI
- В деталях правила: dropdown `Pattern Mode (Glob / Regex)`, текстове поле `Pattern`, чекбокс `Match full path`.
- Live-валідація: під полем — мітка `✓ matches: T_Rock_D.png, T_Wall_D.png` (показати 3 приклади з активного проєкту через `AssetDatabase.FindAssets`). Якщо regex не валідний — червона мітка з message з `RegexParseException`.

### 1.6 Файли
- `Editor/Data/BaseImportRule.cs` — нова схема полів.
- `Editor/Data/PatternMode.cs` — новий enum.
- `Editor/Logic/PatternMatcher.cs` — новий клас (glob→regex + кеш).
- `Editor/Logic/RuleValidator.cs` — переписаний `FindMatchingRule`.
- `Editor/Logic/RuleMigrator.cs` — новий, виконується з `AssetRouterInitializer`.
- `Editor/View/AssetRouterWindow.cs` — оновити details panel + live preview.
- `Tests/PatternMatcherTests.cs` — нові тести (glob: `*`, `?`, `**`, екранування; regex: валідний/невалідний; timeout).
- `Tests/RuleMigratorTests.cs` — нові.

### 1.7 Acceptance
- Існуючий SO зі схемою v1 автоматично мігрується при першому завантаженні.
- Тести `RuleValidatorTests` залишаються зеленими (адаптуються під нову схему).
- 0 алокацій в `OnPreprocessAsset` після прогріву (verified via Profiler Allocator markers).

---

## ✅ Epic 2 — Pluggable Import Actions

**Нотатка автора:** масив скриптів після пресету — пивот, колайдер, обрізання звуку, Addressables, додавання у SO. "Найсильніша ідея".

> ✅ **Виконано — v0.3.0**

- [x] 2.1 Чому
- [x] 2.2 Контракт — `IAssetImportAction`, `AssetImportActionAsset`, `AssetImportContext`
- [x] 2.3 На правилі — `List<AssetImportActionAsset> postImportActions`
- [x] 2.4 Built-in actions (6 шт.: SetPivot, MeshCollider, TrimAudio, Addressables, Catalog, MenuItem)
- [x] 2.5 UI — ReorderableList дій, кнопка `+` з TypeCache меню
- [x] 2.6 Файли
- [x] 2.7 Acceptance

### 2.1 Чому
Preset закриває лише `AssetImporter` settings. Все інше (генерація колайдера, пакування в адресабли, реєстрація в каталозі) — вимагає коду. Винесення в pluggable interface = ядро ніколи не торкається при додаванні нового сценарію.

### 2.2 Контракт
```
public interface IAssetImportAction
{
    bool CanRunOn(Object importedAsset, AssetImportContext ctx);
    void Execute(Object importedAsset, AssetImportContext ctx);
}

public abstract class AssetImportActionAsset : ScriptableObject, IAssetImportAction
{
    public abstract bool CanRunOn(Object importedAsset, AssetImportContext ctx);
    public abstract void Execute(Object importedAsset, AssetImportContext ctx);
}
```
`AssetImportContext` — невелика value-struct: `string assetPath`, `BaseImportRule rule`, `ImporterSettingsDatabase db`, `ILogger logger`.

Чому ScriptableObject, а не plain class з `[SerializeReference]`: actions можна **переюзати між правилами** (один SO в десяти правилах) + кожен action має власний Inspector "з коробки".

### 2.3 На правилі
```
public List<AssetImportActionAsset> postImportActions;  // інспектор-friendly список
```
Виконуються у `OnPostprocessAllAssets` після `MoveAsset` у вказаному порядку. Один `try/catch` навколо кожного — щоб один кривий action не зривав ланцюг (errored action → `Debug.LogException` + продовжуємо).

### 2.4 Built-in actions (входять у пакет)
Кожен — окремий файл у `Editor/Actions/`:

| Клас | Призначення |
|------|-------------|
| `SetPivotAction` | Центрувати pivot sprite/mesh (`SpriteImporter.spritePivot`, або `Mesh.RecalculateBounds`) |
| `GenerateMeshColliderAction` | На FBX додати `MeshCollider` як sub-asset |
| `TrimAudioSilenceAction` | Обрізати тишу на початку/кінці WAV (тільки .wav, через свій парсер RIFF) |
| `RegisterAddressableAction` | Додати в Addressables group через `AddressableAssetSettings.CreateOrMoveEntry` (`AddressableAssetSettings.CreateAssetReference(guid)`). Захист: `#if UNITY_ADDRESSABLES` версійний дефайн |
| `AppendToCatalogAction` | Додати посилання на ассет у вказаний SO-каталог (наприклад `SpriteCatalog.asset`) |
| `RunMenuItemAction` | Загальний escape hatch — викликати `EditorApplication.ExecuteMenuItem("...")` |

Addressables — опціональна залежність. Виявлення: у `Editor/AssetRouter.Editor.asmdef` додати `versionDefines: [{ name: "com.unity.addressables", expression: "1.19.0", define: "UNITY_ADDRESSABLES" }]`. Це стандарт UPM для optional deps.

### 2.5 UI
- У details panel правила — `ReorderableList` дій (drag-drop порядку виконання).
- На кожному елементі — посилання на ScriptableObject + кнопка "Edit" що фокусує його в Project window.
- Кнопка `+` показує контекстне меню зі всіма наявними `AssetImportActionAsset`-типами в проєкті (через `TypeCache.GetTypesDerivedFrom<AssetImportActionAsset>()`).

### 2.6 Файли
- `Editor/Actions/IAssetImportAction.cs`
- `Editor/Actions/AssetImportActionAsset.cs`
- `Editor/Actions/AssetImportContext.cs`
- `Editor/Actions/Built-in/*.cs` (6 файлів)
- `Editor/Logic/AssetRouterPostprocessor.cs` — додати виконання дій після move.
- `Editor/Data/ImportRule.cs` — додати `postImportActions`.
- `Tests/ActionPipelineTests.cs` — фейковий action перевіряє виклик.

### 2.7 Acceptance
- Можна перетягнути будь-який `AssetImportActionAsset` у список на правилі без коду.
- Помилка в одній action не блокує наступні в ланцюгу.
- Addressables-action компілюється тільки якщо пакет встановлений, без помилок без пакета.

---

## ✅ Epic 3 — Preview / Dry-run mode

**Нотатка автора:** "ось що станеться з цими 47 файлами якщо натиснеш імпорт".

> ✅ **Виконано — v0.4.0**

- [x] 3.1 Чому
- [x] 3.2 Як — вкладка `Dry Run`, таблиця, кнопка `Apply selected`, Export CSV
- [x] 3.3 Технічно — фоновий скан, CancellationToken, `StartAssetEditing`
- [x] 3.4 Файли — `DryRunView.cs`, `DryRunPlanner.cs`, `BatchMover.cs`
- [x] 3.5 Acceptance

### 3.1 Чому
Коли правил багато і regex складний — без dry-run легко перенести 200 файлів не туди. Це team-safety feature.

### 3.2 Як
- У вікні нова вкладка `Dry Run`.
- Кнопка `Scan project` — проходить по `AssetDatabase.FindAssets("t:Object")` (з фільтрами `monitoredExtensions`/`ignoredFolders`) і для кожного шляху рахує `RuleValidator.FindMatchingRule` + цільову папку.
- Таблиця (через `MultiColumnHeader` або просто `IMGUI` grid):
  ```
  [ ] File             Current folder         → Target folder         Rule            Actions
      T_Rock.png       Assets/RawImport/      Assets/Art/Textures/    Textures        SetPivot, ...
      UI_Btn.png       Assets/RawImport/      Assets/Art/UI/          UI Textures     SetPivot, ...
      qwerty.png       Assets/RawImport/      —                       (no match)      —
  ```
- Кнопка `Apply selected` — виконати move + actions тільки для відмічених рядків.
- Експорт CSV — кнопка `Export to CSV` для review поза Unity.

### 3.3 Технічно
- Все читання — у фоні через `EditorApplication.update` корутину (інакше блокує UI на великих проєктах). Прогрес-бар.
- Cancellation: `CancellationTokenSource`, перевірка у внутрішньому циклі.
- Apply: всередині `try { StartAssetEditing(); ... } finally { StopAssetEditing(); }`.

### 3.4 Файли
- `Editor/View/DryRunView.cs` — нове.
- `Editor/Logic/DryRunPlanner.cs` — будує `List<AssetMoveCandidate>` без виконання.
- `Editor/Logic/BatchMover.cs` — застосовує план; шарингується з Epic 4.
- `Tests/DryRunPlannerTests.cs`.

### 3.5 Acceptance
- Скан 10 000 ассетів — не блокує UI більше ніж на 16 ms на кадр.
- Apply 47 файлів виконується в одному `Start/StopAssetEditing` блоці.
- Користувач може скасувати скан у будь-який момент.

---

## ✅ Epic 4 — Batch Re-import existing assets

**Нотатка автора:** кнопка щоб пройтись по існуючих ассетах і застосувати правила.

> ✅ **Виконано — v0.4.0**

- [x] 4.1 Чому
- [x] 4.2 Що — кнопка `Re-import All Matched`, опція `Force preset re-apply`
- [x] 4.3 Технічно — `DryRunPlanner` + `BatchMover`, progress bar, `StartAssetEditing`
- [x] 4.4 Acceptance

### 4.1 Чому
Сьогодні плагін реагує тільки на нові імпорти. Усе що вже в проєкті — поза дією правил. Без цього плагін не вирішує проблему "наводимо порядок у легасі-проєкті".

### 4.2 Що
- У вікні кнопка `Re-import All Matched`.
- Не плутати з Dry Run з Epic 3: Re-import — це "виконати негайно", Dry Run — "показати спочатку, потім вибірково виконати".
- Опція `Force preset re-apply` — навіть якщо файл уже в потрібній папці, перезапустити імпорт через `AssetDatabase.ImportAsset(path, ForceUpdate)` щоб ApplyTo пресету спрацював.

### 4.3 Технічно
- Перевикористати `DryRunPlanner` + `BatchMover` з Epic 3.
- Прогрес-бар через `EditorUtility.DisplayCancelableProgressBar`.
- Усе в `try/finally` зі `StopAssetEditing` (підтверджено: різниця у тестових проєктах "3 години → 3 хвилини" коли batch правильний).

### 4.4 Acceptance
- Один клік пере-розкладає весь проєкт за наявними правилами.
- Cancellable.
- Логи з підсумком `Moved: 142, Skipped: 30, Errored: 0`.

---

## ✅ Epic 5 — Conflict Detection

**Нотатка автора:** якщо два правила можуть матчити один ассет — підсвічувати у вікні.

> ✅ **Виконано — v0.2.0**

- [x] 5.1 Типи конфліктів (Duplicate, Overlap)
- [x] 5.2 Як виявити — string-equality для дублікатів, sample-path heuristic для overlap
- [x] 5.3 UI — banner у вікні, `⚠` у ReorderableList
- [x] 5.4 Файли — `ConflictDetector.cs`, `ConflictDetectorTests.cs`
- [x] 5.5 Acceptance

### 5.1 Типи конфліктів
1. **Strict duplicate** — два правила з ідентичним `pattern` + `patternMode` + `matchAgainstFullPath` → дублікат, треба видалити одне.
2. **Shadowing** — правило B повністю перекриває правило A (B матчиться завжди коли A — ні), і B стоїть пізніше → A мертве.
3. **Overlap** — є хоча б один теоретичний рядок що матчиться обома → warning, перше виграє.

### 5.2 Як виявити
- **Strict duplicate** — string-equality по нормалізованому pattern.
- **Overlap** для glob — обернути обидва glob у regex і перевірити перетин: для двох регексів немає простого "перетин не порожній", але можна перевірити на ~50 згенерованих кандидатах (`T_Foo.png`, `UI_Btn.png` тощо з ассетів проєкту) + перевірити "rule A матчиться, rule B теж". Це евристика, не формальний доказор. Для regex повноцінне рішення = `RegExp intersection` через переведення в DFA — це overkill.
- Альтернатива: брати імена реальних ассетів проєкту, прогнати кожне через всі правила, якщо ≥2 матчиться — підсвітити правила. Це pragmatic.

### 5.3 UI
- У ReorderableList біля проблемного правила — іконка `⚠`. Tooltip пояснює: "Shadows rule #3 'Sound Effects' — обидва матчать `SFX_*.wav`".
- На вершині вікна — banner `2 conflicts detected. Show details ▾` що розкриває таблицю.

### 5.4 Файли
- `Editor/Logic/ConflictDetector.cs` — нове.
- `Tests/ConflictDetectorTests.cs`.

### 5.5 Acceptance
- Strict duplicate визначається 100% точно.
- Overlap по реальних ассетах проєкту — false positive rate допустимий, false negative rate близький до нуля (краще перепопередити).

---

## ✅ Epic 6 — Operation Log + Undo Last Batch

**Нотатка автора:** один кривий regex → 200 файлів не там. Треба undo.

> ✅ **Виконано — v0.4.0**

- [x] 6.1 Чому
- [x] 6.2 Що — `OperationLogEntry`, вкладка `History`, кнопка `Undo last session`
- [x] 6.3 Технічно — атомарний запис, `StartAssetEditing`, best-effort revert
- [x] 6.4 Файли — `OperationLog.cs`, `UndoEngine.cs`, `HistoryView.cs`
- [x] 6.5 Acceptance

### 6.1 Чому
`AssetDatabase.MoveAsset` **не реєструється** у вбудованому Unity Undo Stack. `Undo.RegisterImporterUndo` існує, але це для змін у налаштуваннях importer, не для move. Треба свій undo-механізм.

### 6.2 Що
- Кожен batch (Postprocessor session, Dry-Run apply, Batch Re-import) пише `OperationLogEntry` з `timestamp`, `ruleName`, `from`, `to`, `actionsRun[]`. Stored в `Library/AssetRouter/log.json` (Library — гра-локальне, в git не йде, ок).
- Window: вкладка `History` — таблиця останніх N сесій (кожна — групa записів з timestamp). Кнопка `Undo last session` робить зворотній `MoveAsset(to, from)` для кожного запису у зворотному порядку.
- Хедер `Library/AssetRouter/log.json` — версійний (`{"v":1,"sessions":[...]}`) щоб майбутні зміни формату не ламали старі логи.

### 6.3 Технічно
- Запис у лог — атомарний: `File.WriteAllText(tmp); File.Move(tmp, final)`. Без цього при крашу Unity лог пошкоджується.
- Undo операції — теж `try/finally StartAssetEditing`.
- Деякі move-операції незворотні (якщо файл потім видалили, ассет перевизначили) — реалізувати best-effort з підсумком `Reverted: 142, Failed: 8 (files no longer at target)`.

### 6.4 Файли
- `Editor/Logic/OperationLog.cs`
- `Editor/Logic/UndoEngine.cs`
- `Editor/View/HistoryView.cs`
- `Tests/OperationLogTests.cs`

### 6.5 Acceptance
- Undo після Batch Re-import повертає ассети на оригінальні шляхи у 100% випадків, де ассети не видалені.
- Лог JSON читабельний, версіонований.

---

## ✅ Epic 7 — Git-friendly persistence

**Нотатка автора:** ScriptableObject у бінарі → merge conflicts.

> ✅ **Виконано — v0.5.0**

- [x] 7.1 Реальність
- [x] 7.2 Що робити — варіант A (JSON export/import) рекомендовано
- [x] 7.3 Формат JSON — `$type` discriminator, GUID refs, Newtonsoft.Json
- [x] 7.4 Файли — `JsonExporter.cs`, `JsonImporter.cs`, `JsonRoundTripTests.cs`
- [x] 7.5 Acceptance

### 7.1 Реальність
Unity з 2017+ за замовчуванням Force Text mode (підтверджено: у проєкті `m_SerializationMode: 2`). SO **уже** зберігається як YAML, читабельно. Реальна біль — `[SerializeReference]`-айдішники (`fileID: -8345671234`) зсуваються при reorder, тому diff виглядає більшим ніж насправді. Це знайдено в [Unity docs про SerializeReference](https://docs.unity3d.com/ScriptReference/SerializeReference.html).

### 7.2 Що робити
Дві опції, обидві варіант:

**(A) Залишити SO як source of truth, додати JSON export/import** — кнопки `Export to JSON` / `Import from JSON` у вікні. JSON для code review/PR-friendly diff, SO для runtime/UI. Команда може зберігати лише JSON у git, кожен дев робить Import → отримує SO. Це pragmatic.

**(B) Перевести source of truth у JSON, SO стає transient cache** — drastically: повністю переписати layer. SO будується з JSON на старті, всі зміни через JSON. Це cleaner, але міграція складніша.

**Рекомендація — варіант A** для першої версії. Простіше, не ламає поточних користувачів.

### 7.3 Формат JSON
- Стабільний schema. `$type` discriminator для поліморфних `BaseImportRule` (зараз тільки `ImportRule`, але майбутні підкласи мають продовжити працювати).
- Власний writer (не `JsonUtility` — він не вміє поліморфізм). Розглянути [`Newtonsoft.Json` через офіційний пакет](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json) — це не optional dep, додати в `package.json dependencies`. Версія `3.2.1+`.
- Поля `Preset preset` і `List<AssetImportActionAsset> postImportActions` серіалізуються як `{"guid":"abc..."}` — посилання, не вміст. При імпорті GUID знаходимо через `AssetDatabase.GUIDToAssetPath`.

### 7.4 Файли
- `Editor/Logic/JsonExporter.cs`
- `Editor/Logic/JsonImporter.cs`
- `package.json` — додати `com.unity.nuget.newtonsoft-json` у `dependencies`.
- `Tests/JsonRoundTripTests.cs` — гарантує `SO → JSON → SO` без втрати даних.

### 7.5 Acceptance
- Експорт 50 правил → human-readable JSON ~10 KB, стабільний при reorder.
- Roundtrip тести зелені.

---

## ✅ Epic 8 — Bundled content (presets + actions + sample scene)

**Нотатка автора:** готовий набір пресетів + готовий набір простих скриптів, разом.

> ✅ **Виконано — v0.5.0**

- [x] 8.1 Presets — 6 нових (Sprite, Lightmap, NormalMap, ModelStatic, ModelCharacter, Voice)
- [x] 8.2 Actions — built-in actions з Epic 2
- [x] 8.3 Sample scene — `Samples~/QuickStart/` з Raw-файлами і README
- [x] 8.4 Файли
- [x] 8.5 Acceptance

### 8.1 Presets (уже частково є)
Доповнити поточні 4 пресети:

| Preset | Призначення |
|--------|-------------|
| `TextureImporter_Sprite` | UI sprite з sprite mode = Single, mesh type = Tight |
| `TextureImporter_Lightmap` | EXR/HDR з HDR enabled, no compression |
| `TextureImporter_NormalMap` | NormalMap type, BC5 compression, no sRGB |
| `ModelImporter_Static` | для props: no rig, no animations, generate colliders off |
| `ModelImporter_Character` | rig humanoid, animations import, optimize game objects |
| `AudioImporter_Voice` | Compressed In Memory, mono, 22050 Hz |

### 8.2 Actions (з Epic 2)
Той же набір 6 actions з Epic 2 — це і є "готовий набір простих скриптів".

### 8.3 Sample scene
Заповнити `Samples~/QuickStart/` фактичним контентом:
- `SampleScene.unity`
- `Raw/T_Rock_D.png`, `Raw/UI_Button.png`, `Raw/SFX_Click.wav`, `Raw/Mus_Loop.ogg` — приклади що матчаться
- `Raw/qwerty.png` — приклад unknown file для демонстрації попапа
- `README.md` — кроки: "імпортуй sample → подивись як файли розлітаються".
- Окрема демо-сцена з runtime SO-каталогом якщо Epic 2 `AppendToCatalogAction` готовий.

### 8.4 Файли
- `Presets/*.preset` (нові 6) + `.meta`
- `Samples~/QuickStart/*`
- `package.json` — `samples` array уже є, перевірити що path правильний

### 8.5 Acceptance
- "Import sample" в Package Manager → демо-сцена відкривається, drag-and-drop файлу з `Raw/` показує всю магію за 5 секунд.

---

## ✅ Epic 9 — Cross-platform & infrastructure cleanup

Чисто інженерні борги, не feature-roadmap, але без них senior-bar не пройти.

> ⚠️ **Майже виконано — v0.1.0** (залишився пункт 9.10)

- [x] 9.1 Path normalization audit — `PathUtility` (NormalizeAssetPath, ToAbsolute, IsUnderFolder)
- [x] 9.2 `CreateNewDatabase()` bug — явний виклик `DefaultDatabaseFactory.PopulateDefaults(db)`
- [x] 9.3 Duplicate defaults — `DefaultDatabaseFactory`, обидва місця делегують до нього
- [x] 9.4 Window not reactive — `EditorApplication.projectChanged` підписка
- [x] 9.5 CHANGELOG.md — створено, Keep a Changelog формат
- [x] 9.6 CI — `.github/workflows/test.yml` (game-ci/unity-test-runner@v4)
- [x] 9.7 Runtime folder — не створювати (немає runtime features)
- [x] 9.8 SmartAssetImporter naming — залишаємо `Asset Router` (питання відкрите)
- [x] 9.9 Acceptance
- [ ] 9.10 Unity license activation для CI — **обов'язково перед релізом**

### 9.1 Path normalization audit
Пройти grep по всьому коду:
- `Application.dataPath.Replace("Assets", "")` → fix (баг описаний у 0.3).
- `path.Replace('\\', '/')` — лише в одному centralized `PathUtility`, інлайн не дозволяється.
- Усе порівняння шляхів — `StringComparison.OrdinalIgnoreCase` (Windows і macOS — case-insensitive FS; Linux — sensitive, але AssetDatabase нормалізує). Цей вибір документуємо в коментарі коду.

### 9.2 `CreateNewDatabase()` bug
Зараз у `AssetRouterWindow.CreateNewDatabase()` створюється SO через `CreateInstance<>`, Unity не викликає `Reset()`, поля `monitoredExtensions`/`ignoredFolders`/`rules` залишаються порожні. → Викликати `DefaultDatabaseFactory.PopulateDefaults(db)` явно.

### 9.3 Duplicate defaults
`ImporterSettingsDatabase.Reset()` (Reset для inspector "Reset" дії) і `AssetRouterInitializer.CreateDefaultDatabaseIfMissing()` дублюють список правил з **різними** `monitoredExtensions`. Винести в `DefaultDatabaseFactory.cs`, обидва місця кличуть фабрику.

### 9.4 Window not reactive to external changes
Якщо SO створено програмно після відкриття вікна — воно не оновиться. Додати:
```
private void OnEnable() { EditorApplication.projectChanged += OnProjectChanged; ... }
private void OnDisable() { EditorApplication.projectChanged -= OnProjectChanged; }
```

### 9.5 CHANGELOG.md
Створити в корені пакета. Заповнити поточну версію `0.0.1`.

### 9.6 CI
Додати `.github/workflows/test.yml` (у корені репозиторію, не в пакеті):
- Unity 2022.3.62f3
- `game-ci/unity-test-runner@v4`, `testMode: EditMode`
- Trigger: push + PR.
- Free runner ok (відкритий MIT-репозиторій).

### 9.7 Runtime folder?
Нотатка автора згадує `Runtime/` папку. Сьогодні плагін на 100% editor-only. Runtime код потрібен **тільки** якщо ми додаємо runtime SO-каталог з `AppendToCatalogAction` (Epic 2). Тоді створити:
```
Runtime/
  AssetRouter.Runtime.asmdef     ← без editor-only прапора
  Catalogs/SpriteCatalog.cs      ← ScriptableObject що ассети додаються в нього
Editor/
  AssetRouter.Editor.asmdef      ← references: AssetRouter.Runtime
```
Без runtime feature — Runtime/ створювати **не треба**, це cargo cult.

### 9.8 SmartAssetImporter — це AssetRouter?
У нотатках згадка `Assets/SmartAssetImporter/`. Поточна назва пакета — `com.kodlon.assetrouter`, namespace `Kodlon.AssetRouter`. Якщо це старе ім'я з нотаток до rename — нічого не робимо. Якщо це нове ім'я, на яке треба перейти — це окремий жорсткий рефактор (rename namespace + `[MovedFrom]` на кожен серіалізуємий клас + redirect для package manifest + git tag old version). Уточнити перед стартом.

### 9.9 Acceptance
- Усі баги вище пофіксані.
- CI зелений на main.
- CHANGELOG актуальний.

### 9.10 Unity license activation для CI
CI workflow (`game-ci/unity-test-runner@v4`) потребує Unity ліцензії для запуску тестів. Без неї крок "Run edit-mode tests" падає миттєво. Потрібно зробити один раз перед релізом на OpenUPM.

**Кроки:**
1. Додати у репо activation workflow з [game-ci docs](https://game.ci/docs/github/activation) — він згенерує `.alf` файл (license request).
2. Завантажити `.alf`, зайти на `license.unity3d.com`, активувати **Unity Personal** (безкоштовно).
3. Завантажити `.ulf` файл (активована ліцензія).
4. Додати в репо → Settings → Secrets → Actions:
   - `UNITY_LICENSE` — вміст `.ulf` файлу
   - `UNITY_EMAIL` — email Unity акаунту
   - `UNITY_PASSWORD` — пароль Unity акаунту
5. Видалити тимчасовий activation workflow.

**Вартість:** безкоштовно (Unity Personal + GitHub Actions для public repo).

---

## 10. Мої додаткові ідеї (поверх нотаток)

### 10.1 Per-folder rule scope
Правило має опціональне поле `Apply only in folder` (наприклад `Assets/External/`). Тоді одне імʼя `T_Rock.png` може мати різну долю залежно від куди його кинули. Корисно для проєктів з кількома "вхідними" папками від різних артистів.

### 10.2 Diagnostic mode
`Tools > Asset Router > Diagnostic Window` — суцільний лог усього що приходить у postprocessor: `path, monitored?, ignored?, matched rule, preset applied?, moved?`. Кожен рядок з timestamp. Окрема панель, не Console. Допомагає коли "файл не рухається і незрозуміло чому".

### 10.3 Per-rule statistics
SO зберігає лічильник `timesMatched` на правило (incremented атомарно у postprocessor). У вікні правила показано `Matched 1247 files since 2026-06-10`. Видно мертві/гарячі правила.

### 10.4 Naming convention validator (read-only)
Окремий "Validate Project Names" window: проходить усі ассети, для кожного питає "чи матчишся ти хоч одним правилом?" — якщо ні, додає у список "файли поза конвенцією". Це **не** виконує дій, тільки звіт. Дозволяє увести naming convention в команді поступово.

### 10.5 Rule templates / sharing
Кнопка `Export rule as gist` — копіює YAML/JSON одного правила в clipboard. Кнопка `Import rule from clipboard` — додає його в поточний SO. Дозволяє ділитися правилами між проєктами без копіювання всього SO.

### 10.6 OnPreprocessAsset double-apply protection
Зараз preset застосовується двічі: один раз перед оригінальним імпортом, другий — після `MoveAsset` (Unity ре-імпортує файл на новому шляху). Idempotent, але це log spam + зайва робота. Розширити `AssetsBeingMoved` HashSet або додати `HashSet<string> AssetsBeingProcessed` для defense в `OnPreprocessAsset`. Перевірити чи preset.ApplyTo на однакових даних викликає `OnPostprocessAsset` chain — якщо так, можна викликати reentrancy.

### 10.7 Sponsor / public release polish
Якщо ціль — публікація на OpenUPM/Asset Store:
- Screenshot/GIF на 4 секунди в README — драг-енд-дроп файлу, він летить у папку.
- `.openupmrc` для OpenUPM реєстрації.
- Promotional banner 1920×1080.
- Сторінка з 5 use-cases ("я тех-арт у мобайл-команді...", "я соло-розробник з 2000 ассетів...").

### 10.8 Telemetry (opt-in!)
Аноним лічильник скільки правил у user-баз і скільки разів спрацьовує. Дозволяє знати які features реально живі. Тільки після явного opt-in checkbox при першому запуску. Без opt-in — нуль трафіку.

### 10.9 Asset Database v2 compatibility
Перевірити що плагін працює як з Asset Database v1, так і з v2 (Unity 2019.3+). У 2022.3 default — v2. Особливо `HashSet<string>` re-entry захист — поведінка може відрізнятися. Тест: на проєкті з v2 запустити повний flow.

### 10.10 Назва "Asset Router" — питання SEO
Asset Store пошук по "asset router" / "asset organizer" / "auto importer". Швидкий гуглінг показує що Asset Store вже має кілька конкурентів з близькими іменами. Можливо варто перейменувати на щось унікальніше (`Asset Conductor`, `Asset Postman`, `Importmancer`...). Це не код, це маркетинг — але впливає на discoverability.

---

## Suggested release order

- [x] **v0.1.0** — Epic 9 (cleanup + bugfixes + CI). Стабільний фундамент.
- [x] **v0.2.0** — Epic 1 (pattern matching) + Epic 5 (conflict detection).
- [x] **v0.3.0** — Epic 2 (import actions). Перший великий стрибок гнучкості.
- [x] **v0.4.0** — Epic 3 (dry-run) + Epic 4 (batch re-import) + Epic 6 (undo) — пак "team-safety".
- [x] **v0.5.0** — Epic 7 (JSON export) + Epic 8 (bundled content + sample). Готовий до публікації.
- [ ] **v1.0.0** — стабілізація, реліз на OpenUPM.

Кожен реліз — тег у git, оновлення `package.json` version, оновлення `CHANGELOG.md`, GitHub Release notes.

---

## Open questions перед стартом

1. **Назва**: лишаємо `Asset Router` чи перейменовуємо на `SmartAssetImporter` як у нотатках?
2. **Newtonsoft.Json**: додаємо як обовʼязкову залежність (Epic 7), чи робимо JSON export опціональним без зовнішніх deps (власний writer)?
3. **Runtime folder**: створюємо одразу зі скелетом (під майбутній `AppendToCatalogAction`) чи додаємо тоді коли реально потрібен?
4. **CI provider**: GitHub Actions (free для public репо) ок?
5. **Target Unity**: підтримуємо тільки 2022.3 LTS, чи додаємо 2021.3 LTS і 6.0+?
