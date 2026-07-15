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

> ✅ **Виконано повністю — v0.6.0** (9.10 закритий 2026-06-23)

- [x] 9.1 Path normalization audit — `PathUtility` (NormalizeAssetPath, ToAbsolute, IsUnderFolder)
- [x] 9.2 `CreateNewDatabase()` bug — явний виклик `DefaultDatabaseFactory.PopulateDefaults(db)`
- [x] 9.3 Duplicate defaults — `DefaultDatabaseFactory`, обидва місця делегують до нього
- [x] 9.4 Window not reactive — `EditorApplication.projectChanged` підписка
- [x] 9.5 CHANGELOG.md — створено, Keep a Changelog формат
- [x] 9.6 CI — `.github/workflows/test.yml` (game-ci/unity-test-runner@v4)
- [x] 9.7 Runtime folder — не створювати (немає runtime features)
- [x] 9.8 SmartAssetImporter naming — залишаємо `Asset Router` (питання відкрите)
- [x] 9.9 Acceptance
- [x] 9.10 Unity license activation для CI — Personal license, GitHub Secrets, `permissions:` block

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

### 9.10 Unity license activation для CI ✅
CI workflow (`game-ci/unity-test-runner@v4`) потребує Unity ліцензії для запуску тестів. Без неї крок "Run edit-mode tests" падає миттєво.

**Актуальний flow (GameCI v4, 2026):** `.alf → .ulf` через activation workflow більше не потрібен. Hub генерує `.ulf` локально.

**Кроки (виконано 2026-06-23):**
1. Unity Hub → `Preferences` → `Licenses` → `Add` → **Get a free personal license**. Тицяти саме `Add` (навіть якщо ліцензія вже в списку) — інакше `.ulf` файл фізично не створиться.
2. Знайти `.ulf` файл:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. Додати у репо → `Settings` → `Secrets and variables` → `Actions` → `New repository secret`:
   - `UNITY_LICENSE` — вміст `.ulf` файлу (повний XML)
   - `UNITY_EMAIL` — email Unity-акаунту
   - `UNITY_PASSWORD` — пароль Unity-акаунту
4. Додати `permissions:` блок у `.github/workflows/test.yml` (інакше `Resource not accessible by integration` на пості результатів):
   ```yaml
   permissions:
     contents: read
     checks: write
     pull-requests: write
   ```

**Підводні камені:**
- **Google SSO у Unity** — game-ci не вміє OAuth-flow. Треба встановити окремий пароль через `id.unity.com` → Security → Set password (або через "Forgot password" якщо поле відсутнє). Це не ламає SSO — обидва способи входу живуть паралельно.
- **2FA на Unity-акаунті** залишається увімкненим; game-ci не запускає повний login, валідується через `.ulf`.
- **Personal license прив'язана до Unity-версії** — при апгрейді workflow на нову мінорну версію треба реактивувати ліцензію локально і оновити `UNITY_LICENSE` secret.

**Вартість:** безкоштовно — Unity Personal + GitHub Actions для public repo (unlimited standard runner minutes, не торкає новий $0.002/min charge з 2026-01-01).

---

## 10. Мої додаткові ідеї (поверх нотаток)

### ✅ 10.1 Per-folder rule scope
> ✅ **Виконано — v0.8.0**

Правило має опціональне поле `Apply only in folder` (наприклад `Assets/External/`). Тоді одне імʼя `T_Rock.png` може мати різну долю залежно від куди його кинули. Корисно для проєктів з кількома "вхідними" папками від різних артистів.

- [x] `scopeFolder = ""` поле у `BaseImportRule`
- [x] Перевірка у `RuleValidator.FindMatchingRule` через `PathUtility.IsUnderFolder`
- [x] UI: "Scope Folder" в секції Pattern деталей правила
- [x] 3 нові тести у `RuleValidatorTests`

### ✅ 10.2 Diagnostic mode
> ✅ **Виконано — v0.8.0**

`Tools > Asset Router > Diagnostic Window` — лог моніторованих ассетів у postprocessor: filename, matched rule, action (no match / in place / moved). Кожен рядок з timestamp. Окрема панель, не Console. Допомагає коли "файл не рухається і незрозуміло чому".

- [x] `DiagnosticLog.cs` — in-memory ring buffer 500 записів, `IsEnabled` toggle, `beforeAssemblyReload` очищення
- [x] `DiagnosticWindow.cs` — EditorWindow, таблиця з автоскролом, Clear кнопка
- [x] Emit у `OnPostprocessAllAssets` коли `DiagnosticLog.IsEnabled`

**Примітка:** тільки моніторовані ассети (що пройшли `ShouldProcess`) потрапляють у лог. Ігноровані/невідомі розширення не показуються — достатньо для use case "чому мій файл не рухається".

### ✅ 10.3 Per-rule statistics
> ✅ **Виконано — v0.8.0**

SO зберігає лічильник `timesMatched` на правило (incremented у postprocessor). У вікні правила показано `(N✓)`. Видно мертві/гарячі правила.

- [x] `RuleStatsStore.cs` — JSON-персистенція в `Library/AssetRouter/stats.json`, `IncrementBatch` (один read+write на batch), `ReadAll`, `Clear`
- [x] `BaseImportRule._sessionMatchCount` — `[NonSerialized]` in-memory per-session counter
- [x] Increment + display у `AssetRouterWindow` (завантажується при `LoadDatabase`)
- [x] 4 нові тести у `RuleStatsStoreTests.cs`

### ✅ 10.4 Naming convention validator (read-only)
> ✅ **Виконано — v0.8.0**

Вкладка "Validate" у Asset Router window: проходить усі ассети через `DryRunPlanner.Scan`, для кожного без правила — додає у список "файли поза конвенцією". Це **не** виконує дій, тільки звіт. Кнопка "Copy to Clipboard".

- [x] `NamingValidatorView.cs` — scan, table, clipboard export
- [x] 4й таб "Validate" у `AssetRouterWindow`

### ✅ 10.5 Rule templates / sharing
> ✅ **Виконано — v0.9.2**

Кнопка `Export rule as gist` — копіює YAML/JSON одного правила в clipboard. Кнопка `Import rule from clipboard` — додає його в поточний SO. Дозволяє ділитися правилами між проєктами без копіювання всього SO.

- [x] `JsonExporter.ExportRule(ImportRule)` — JSON з `{guid, path}` для preset і object-ref полів дій (через reflection)
- [x] `JsonImporter.TryImportRuleFromJson` — резолв preset GUID → fallback path → warning; object-ref поля actions відновлюються через reflection + `{guid, path}`; warning якщо N refs не резолвились
- [x] UI: "Copy Rule to Clipboard" у деталях правила; "Paste from Clipboard" у хедері списку правил (без потреби виділяти правило)
- [x] 6 нових тестів у `JsonRoundTripTests.cs`

### ✅ 10.6 OnPreprocessAsset double-apply protection
> ✅ **Виконано — v0.8.0**

Зараз preset застосовується двічі: один раз перед оригінальним імпортом, другий — після `MoveAsset` (Unity ре-імпортує файл на новому шляху). Idempotent, але це log spam + зайва робота. Розширити `AssetsBeingMoved` HashSet або додати `HashSet<string> AssetsBeingProcessed` для defense в `OnPreprocessAsset`. Перевірити чи preset.ApplyTo на однакових даних викликає `OnPostprocessAsset` chain — якщо так, можна викликати reentrancy.

- [x] `if (AssetsBeingMoved.Contains(assetPath)) return;` у `OnPreprocessAsset` перед `ShouldProcess`

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

## ✅ Epic 11 — Critical bugs & production hardening (pre-v1.0.0)

Знайдено під час повного code review v0.5.0. Усе нижче — реальні баги, не nitpicks. Жоден не може зайти в публічний реліз.

> ✅ **Виконано — v0.6.0**

### 11.1 Reentrancy & domain reload
- [x] `AssetRouterPostprocessor` — `AssemblyReloadEvents.beforeAssemblyReload += лямбда` через `[InitializeOnLoadMethod]` накопичує підписки на кожному reload при disabled Domain Reload. Замінити на named static method + unsubscribe-first.
- [x] `AssetRouterPostprocessor.OnPostprocessAllAssets` — цикл moves без `AssetDatabase.StartAssetEditing()/StopAssetEditing()`. На 10k+ файлах це O(N²) реімпортів. Загорнути.
- [x] Reentrancy hole у guard-сетах (`AssetsBeingMoved`, `_pendingActions`): джерельний шлях ассета не захищений, тільки таргет. Додавати обидва шляхи в HashSet до `MoveAsset`.
- [x] `AssetRouterInitializer` — `delayCall` не реfireиться при play→edit з disabled Domain Reload; мігратор може не запуститись для свіжо-створеного DB. Додатково підписатися на `EditorApplication.projectChanged`.

### 11.2 Atomic file writes — три місця з window'ом втрати даних
Поточний патерн `File.Delete(path); File.Move(tmp, path);` має вікно де файлу немає взагалі. Crash → втрата.

- [x] `OperationLog.WriteAll` — замінити на `File.Replace(tmp, path, backupFileName: path + ".bak")` (Windows) або `File.Move(tmp, path, overwrite: true)` (.NET Standard 2.1+).
- [x] `JsonExporter.ExportToFile` — той самий патерн.
- [x] `TrimAudioSilenceAction.TryTrim` — переписування WAV без atomic guarantee.

### 11.3 Sub-asset lifecycle (orphans + double-references)
- [x] `AssetRouterWindow.onRemoveCallback` (для правил) — видалення правила залишає сабассети `postImportActions` сиротами в `.asset` файлі бази. Перед `DeleteArrayElementAtIndex` пройти `rule.postImportActions` і прибрати сабассети.
- [x] `AssetRouterWindow.onRemoveCallback` (для actions) — `DestroyImmediate` сабассета не перевіряє, чи інше правило в БД на нього посилається. Виходить silent null. Скан перед destroy.

### 11.4 Unknown-asset dialog UX bug
- [x] `AssetRouterPostprocessor` — при множинному імпорті (drag-drop 100 unknown файлів) попап показується **окремо для кожного файлу**. Об'єднати в одну модалку зі списком + per-row дії, або агрегований "Import all / Delete all".
- [x] `delayCall`-обгортка попапа: при domain reload до виконання — попап не з'явиться, файл сирота без логу. Логувати warning перед `delayCall`.

### 11.5 TrimAudioSilenceAction — integer overflow в WAV-парсері
- [x] Лінія з `(short)(threshold * short.MaxValue)` + `Math.Abs(sample)` для `short.MinValue` дає overflow. Привести до `int thresholdSample` і порівнювати `Math.Abs((int)sample) > thresholdSample`.
- [x] RIFF-парсер — нема bounds-checks: `size >= 0`, `pos + 8 + size <= data.Length`. Малформований WAV → IndexOutOfRange.
- [x] Action працює зсередини post-import chain і робить `ImportAsset(ForceUpdate)` — теоретичний нескінченний цикл, якщо файл матчить власне правило знову. Захист через depth counter або `_pendingActions` extension.

### 11.6 PatternMatcher edge case
- [x] `Assets/**` НЕ матчить `Assets/x.png` через те, як `**` обробляє trailing slash. Задокументовано в UI-тултипах (Pattern field + Match Full Path) + додано тест `Glob_DoubleStar_Alone_MatchesDirectChildToo` що фіксує поведінку.

### 11.7 UI perf на великих проєктах
- [x] `AssetRouterWindow.BuildPatternPreview` — `FindAssets("", {"Assets"})` синхронно на кожне натискання клавіши в pattern field. На 50k проєкті фриз на секунди. Debounce 300 ms + `delayCall`.
- [x] `DryRunPlanner.Scan` — той самий `FindAssets` без progress bar; UI заморожується. Додати `EditorUtility.DisplayCancelableProgressBar` + батчі.
- [x] `AssetRouterWindow` — `AssetDatabase.SaveAssets()` після кожного додавання action. Відкладати save до Save/Apply.

### 11.8 BatchMover — кілька дрібних
- [x] `EnsureFolderExists` всередині `StartAssetEditing` — наступний `MoveAsset` падає з "folder does not exist". Створити всі потрібні папки **до** `StartAssetEditing`.
- [x] Лічильник `moved` для force-reimport in-place вводить в оману. Розділити: `Moved`, `Reimported`, `Skipped`, `Errored`.
- [x] Логіка `if (cancelled)` має inconsistent `current++` між гілками — рефакторити в одну гілку.

### 11.9 UndoEngine
- [x] Замінити `DisplayProgressBar` на `DisplayCancelableProgressBar` — на 10k undo нема як вийти.
- [x] Silent skip коли ассет переміщено далі — показати summary dialog: `Reverted: 142, Skipped: 8 (no longer at target)`.

### 11.10 JSON Importer/Exporter
- [x] Після `JsonImporter.ImportFromFile` не запускається `RuleMigrator` — імпорт legacy-схеми зламається на v1.x. Викликати мігратор у кінці.
- [x] `extensions.Add(ext.Value<string>())` додає `null` рядки якщо JSON malformed → NRE далі. Filter null/empty.
- [x] Sub-asset actions експортуються як `{guid, fileId}` — cross-machine sharing не працює (різні fileId). Явно задокументовано: "JSON export працює тільки в рамках одного проєкту" (portability note у XMLDoc).

### 11.11 OperationLog robustness
- [x] При corrupted `log.json` `JsonUtility.FromJson` глитає виняток і повертає пустоту — історія "зникає". Зберегти corrupted файл як `log.json.corrupt` + warning.
- [x] Лог росте без обмежень. Додати size cap (наприклад, останні 500 сесій) + кнопка `Clear History` в `HistoryView`.

### ✅ 11.12 ConflictDetector false negatives
- [x] Overlap-евристика на 14 хардкодних шляхах: правила типу `Char_*` vs `Char_Hero_*` не позначаться, бо `Char_Hero_*` не матчить жодний sample. Додати в UI banner caveat "евристика, можливі пропуски".
- [x] Розширити sample set реальними ассетами проєкту (top-100 за `AssetDatabase.FindAssets`) — гібридна стратегія.
- [x] **Perf (Medium):** `ConflictDetector` — `[InitializeOnLoad]`, `_cachedSamplePaths` static field, `GetOrBuildSamplePaths()`. Інвалідація через `EditorApplication.projectChanged` і `AssetDatabase.importPackageCompleted`. `InvalidateSampleCache()` internal для тестів. 2 нові тести у `ConflictDetectorTests`.

### ✅ 11.15 AssetRouterInitializer — subscription accumulation
- [x] **Low:** unsubscribe-first (`-= / +=`) для `delayCall` і `projectChanged` у статичному конструкторі `AssetRouterInitializer`.

### 11.13 Built-in actions — perf
- [x] `AppendToCatalogAction` — `List.Contains` O(N). На каталозі 10k+ помітно. Залишено як є + задокументовано ліміт у коментарі коду.
- [x] `RegisterAddressableAction` — `AssetDatabase.SaveAssets()` на кожен ассет → батч-катастрофа. Тільки `SetDirty(settings)`, save в кінці post-import.

### 11.14 Acceptance
- [ ] Прогнати весь postprocessor flow на тестовому проєкті з 10k ассетів — час виконання < 30 с, нема race conditions у Profiler.
- [ ] Crash-test: kill Unity процес посередині `JsonExporter` — після рестарту або оригінал, або новий файл, ніколи "ні те, ні те".
- [ ] `OnPostprocessAllAssets` нуль алокацій після прогріву (Profiler Allocator markers).

---

## 📦 Epic 12 — Asset Store / OpenUPM release readiness

Технічні вимоги Unity Asset Store + OpenUPM curation. Усе нижче — обов'язкове для submission.

> ⏳ **Blocker для v1.0.0**

### 12.1 Unity license для CI ✅ (закрито у 9.10 — 2026-06-23)
- [x] Activate Personal license через Unity Hub (`.ulf` згенеровано локально).
- [x] Додано secrets: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
- [x] Додано `permissions: checks/pull-requests: write` у `test.yml`.
- [x] CI зеленіє end-to-end (65/65 тестів пройдено, Check Run публікується).

### ✅ 12.2 package.json — поля
- [x] Прибрати `unityRelease: "0f1"` (вимагає рівно 2022.3.0f1) — лишити лише `unity: "2022.3"`.
- [x] Додати `"category": "Tools/Utilities"`.
- [ ] ~~Додати `"icon"` посилання на icon-файл (UPM 2022+).~~ **Не імплементовано — іконки немає, пропущено.**
- [x] Перевірити що `documentationUrl` веде на готову сторінку — виправлено на `DOCUMENTATION_EN.md` (битий лінк на неіснуючий `DOCUMENTATION.md` знайдено в code review, v0.9.2).
- [x] Перевірити коректність `samples[0].path` — `Samples~/QuickStart` і `Samples~/LegacyActions` існують, шляхи коректні.

### 12.3 Іконка + бренд-ассети
- [ ] ~~Створити `Editor/AssetRouterIcon.png` 64×64 (можна +128 retina) — використати в `EditorWindow.titleContent.image`.~~ **Не імплементовано — іконки немає, пропущено.**
- [ ] Asset Store банер 1920×1080 (cover image) — окрема папка `BrandAssets/` поза пакетом.
- [ ] Card-зображення 860×389 (для thumbnail).
- [ ] 4-секундний GIF drag-and-drop в `README.md` — Asset Store reviewer любить візуал.
- [ ] Screenshot вікна з 3 правилами і dry-run preview.

### ✅ 12.4 XMLDoc на весь public API — закрито в Epic 14
Asset Store reviewer часто блокує за відсутність документації. Все нижче покрито XMLDoc-секцією Epic 14 (див. "XMLDoc (додатково до плану)"):
- [x] `BaseImportRule` — всі поля окрім `pattern`.
- [x] `ImportRule.preset`, `ImportRule.postImportActions`.
- [x] `AssetCatalog.entries`.
- [x] `ImporterSettingsDatabase` — всі поля.
- [x] `PatternMode` enum + значення.
- [x] Публічні методи `AssetImportContext` ctor (вже задокументовано, перевірено).
- [x] `Samples~/LegacyActions/RunMenuItemAction` — клас і поле `menuItem` (v0.9.2, раніше пропущено).
- [x] `Samples~/LegacyActions/GenerateMeshColliderAction` — клас (v0.9.2, раніше пропущено).

### ✅ 12.5 Документація — закрито в Epic 14
- [x] `Documentation~/index.md` створено (Epic 14.1).
- [x] Переклад на англійську — `DOCUMENTATION_EN.md` primary (Epic 14.1).
- [x] Migration guide — `Documentation~/migrations/v1-to-v2-schema.md` (Epic 14.4).
- [x] `Samples~/QuickStart/README.md` розширено 3-ма use case секціями (Epic 14.1).

### ✅ 12.6 Menu structure
- [x] `[MenuItem("Tools/Asset Router Settings")]` → `[MenuItem("Tools/Asset Router/Settings")]` — тепер консистентно з `DiagnosticWindow`.
- [x] Додано `Tools/Asset Router/Documentation` → відкриває `DOCUMENTATION_EN.md` на GitHub.
- [x] Додано `Tools/Asset Router/Report Issue` → відкриває GitHub Issues.

### 12.7 Sample valid
- [ ] Перевірити що preset GUID-и в `Samples~/QuickStart/` не stale між версіями.
- [ ] Sample має включати `Raw/qwerty.png` (unknown file) для демонстрації popup.
- [ ] Sample database має включати приклад з `postImportActions` (наприклад, AppendToCatalog).

### 12.8 GitHub repo polish
- [ ] Topics: `unity`, `unity-package`, `upm`, `unity3d`, `unity-editor`, `unity-asset`, `openupm` (для discoverability).
- [ ] README з badges: build status, OpenUPM version, license, Unity version.
- [ ] GitHub Pages для documentation (опціонально, але професійніше).
- [ ] Release Notes у GitHub Releases на кожен тег (паралельно з CHANGELOG).

### 12.9 OpenUPM registration
- [ ] Перевірити що пакет ще не зайнятий на Unity registry (`com.kodlon.assetrouter` — мабуть унікально).
- [ ] Опублікувати на OpenUPM через web form або PR в `openupm/openupm`.
- [ ] Перевірити що `package.json` у корені репозиторію або вказати `subFolder` у OpenUPM YAML.

### ✅ 12.11 Welcome window on first launch
- [x] `WelcomeWindow.cs` — `[InitializeOnLoad]`, `CheckAndShow()` через `EditorApplication.delayCall`. Перевіряє `Application.isBatchMode` (guard для CI), наявність БД, `SessionState` (показує раз за сесію). Кнопка `Create` → `DefaultDatabaseFactory` → `AssetRouterWindow.OpenWindow()`. Кнопка `Not now` → просто закриває.
- [x] `AssetRouterInitializer.cs` — прибрано `CreateDefaultDatabaseIfMissing()` і константи. `Initialize()` тепер лише мігрує наявну БД.

### 12.10 Asset Store extension license decision
- [ ] Asset Store вимагає **Extension Asset** license (per-seat) для editor extensions. Це не код — це pricing tier при submission. Дослідити чи OK з безкоштовною ліцензією Asset Store (MIT може йти).
- [ ] Або: лишити безкоштовним лише на OpenUPM, на Asset Store не публікувати → менше бюрократії.

### ✅ 12.12 Beginner guide + UI reference (додано 2026-07, виконано 2026-07-05 — лишились скріншоти)

Поточна документація написана для розробників. Для Asset Store аудиторії (артисти, тех-арти, новачки) потрібен рівень "нуб відкрив і за 10 хвилин все зрозумів":

- [x] `Documentation~/getting-started.md` — покрокова інструкція з нуля: встановлення → що створилось автоматично → перший drag-and-drop → створення власного першого правила → де дивитись, чому файл не поїхав (Diagnostic Window). Проста мова, без жаргону. 8 кроків, кожен з конкретним тестовим файлом (`T_Rock_D.png`, `UI_Button.png`, `photo.png`, `Voice_Intro.wav`). Включає крок 5 з демо Set Pivot action на UI Textures rule (закриває залишок 12.13).
- [ ] Скріншоти: в `getting-started.md` на кожному кроці стоять `<!-- SCREENSHOT: ... -->` плейсхолдери — зробити скріншоти вручну в Unity і замінити.
- [x] `Documentation~/ui-reference.md` — повний UI-довідник, **структурований по вікнах, не суцільним списком**. Ієрархія документа:
  - [x] Розділ на кожне вікно: головне вікно Asset Router (і в ньому підрозділ на кожну з 4 вкладок — Settings / Dry Run / History / Validate), Diagnostic Window, Welcome Window, меню `Tools/Asset Router/...`, контекстне меню `Assets/Create/Asset Router/...` (wizard), плюс секція Dialogs (unknown files, clear history confirm, undo summary, export/import failed).
  - [x] Кожен розділ починається з 2-3 речень: **що це вікно/вкладка робить і навіщо існує**.
  - [x] Елементи **в порядку, як вони йдуть на екрані** (toolbar зліва направо → зверху вниз): таблиця "Element → What it does → When you need it", 1-2 речення на елемент.
  - [x] Неочевидне покрито: toolbar головного вікна (Database picker, Create New, Export/Import JSON), Copy/Paste Rule, live preview (debounce, токени, error state), списки actions (sub-asset cleanup), banner конфліктів, multi-database warning, no-database hint, розшифровка колонки Action у Diagnostic Window.
- [x] Лінки на обидва файли з `index.md`, `README.md` і `DOCUMENTATION_EN.md` (getting-started — першим пунктом). Заодно виправлено застарілі згадки: menu path `Tools > Asset Router Settings` → `Tools > Asset Router > Settings` (README, DOCUMENTATION_EN), кнопка "Re-import All Matched" → "Force Re-import In-Place" з коректним описом (нічого не рухає), "Select None" → "None".
- [x] `Documentation~/patterns.md` (додано 2026-07-05) — pattern cookbook для новачків: Glob і Regex поруч, рецепти під типові задачі (префікс/суфікс, альтернативні розширення, capture + templating, folder patterns), таблиця типових помилок (unanchored regex, `.` без escape, Match Full Path), як тестувати (live preview, Dry Run, Validate, regex101 з .NET flavor). Лінки з `getting-started.md` (крок 3), `index.md`, `README.md`, секції Pattern syntax у `DOCUMENTATION_EN.md`.

### ✅ 12.14 Feature catalog — повний перелік фіч з прикладами (додано 2026-07)

- [x] `Documentation~/features.md` — 27 фіч, кожна з параграфом опису + прикладом «було → стало».
- [x] Покрито: auto-routing, glob/regex, path templating, preset auto-apply, scope folder, monitored extensions, ignored folders, per-rule enable/disable, dry run, batch re-import (force in-place), history + undo, conflict detection, unknown files dialog, multi-database warning, diagnostic window, per-rule statistics, naming validator, post-import actions pipeline, 10 built-in actions (таблиця + лінк на per-action docs), custom action authoring, action scaffolding wizard (4 templates), JSON export/import, rule clipboard sharing, multi-database picker, welcome window.
- [x] Згруповано: Routing / Safety / Diagnostics & Monitoring / Extensibility / Team Workflow.
- [x] Лінк з `README.md` і `index.md` (першим пунктом у списку документації).

### ✅ 12.13 Default ruleset: продемонструвати post-import actions out of the box (додано 2026-07)

Зараз жодне дефолтне правило не має `postImportActions` — головна фіча пакета (Epic 2/15) невидима, поки користувач сам не докопається. Новий користувач бачить лише "move + preset".

- [x] Додати в `DefaultDatabaseFactory` хоча б одне правило з демо-action. Вибір: `SetPivotAction` (0.5, 0.5) на UI Textures — safe-by-default, без зовнішніх залежностей. `GenerateNineSliceBordersAction` відхилено: вимагає `isReadable`, яке TextureImporter не вмикає за замовчуванням.
- [x] Action-ассет створюється фабрикою як sub-asset нової бази через двофазний підхід: `PopulateDefaults` (in-memory) → `CreateAsset` → `EmbedSubAssets` (sub-asset реєстрація). `AssetDatabase.IsSubAsset` guard проти double-embed. Всі 3 сайти оновлено: `WelcomeWindow`, `AssetRouterWindow`, `ImporterSettingsDatabase.Reset`.
- [x] `DefaultDatabaseFactoryTests` оновлено: новий тест `CreateDefaultRules_UITexturesRule_HasSetPivotAction` + `try/finally` cleanup у всіх існуючих тестах.
- [x] Згадати в `getting-started.md` (12.12): "подивись на правило X — так підключаються actions". **Виконано 2026-07-05 — крок 5 у `getting-started.md`: UI Textures rule + Set Pivot, з тестом через `UI_Button.png`.**

### 12.11 Acceptance
- `unity package validator` (UPM CLI) повертає 0 warnings.
- Імпорт через `Add package from git URL` працює на свіжому 2022.3 проєкті без помилок.
- OpenUPM CI зелений.
- Asset Store submission принятий з першої спроби.

---

## ✅ Epic 13 — Test coverage closure

Виявлені gap'и в покритті критичних модулів. Без них v1.0.0 — гра в рулетку при рефакторингах.

> ✅ **Частково виконано** — unit-тестовані модулі закриті. Інтеграційні тести відкладені (причина нижче).

### 13.1 Покриття за модулями
- [ ] `BatchMover` — cancel path, force-reimport path, folder creation order, mixed move+reimport, помилки при missing folders. **Відкладено — інтеграційний (див. примітку).**
- [ ] `UndoEngine` — повний revert, partial revert (deleted target), revert конкретної сесії з кількома записами, прогрес-бар cancel. **Відкладено — інтеграційний (див. примітку).**
- [x] `RuleValidator.ShouldProcess` — null db, null/empty path, no extension, ignored folder prefix collision. Додано 7 нових тестів до `RuleValidatorTests.cs`.
- [x] `TrimAudioSilenceAction.TryTrim` — leading silence, trailing silence, both, all-silence, no-silence, malformed RIFF, RIFX big-endian, short.MinValue overflow, output RIFF integrity. 14 тестів у `TrimAudioSilenceActionTests.cs`.
- [x] `PathUtility` — `NormalizeAssetPath` (null, backslashes, trailing slash), `IsUnderFolder` (prefix collision, PluginsCustom regression, case-insensitive), `ToAbsolute` (подвійне "Assets" regression). 9 тестів у `PathUtilityTests.cs`.
- [ ] `AssetRouterPostprocessor` — інтеграційні тести через `Tests/Fixtures~/` з тимчасовими ассетами. **Відкладено — інтеграційний (див. примітку).**
- [ ] `JsonExporter/JsonImporter` — round-trip з усіма 6 типами built-in actions. **Відкладено — інтеграційний (див. примітку).**

### 13.2 Stress tests
- [ ] `DryRunPlanner` на згенерованому проєкті з 10k файлів — час < 5 с. **Відкладено.**
- [ ] `BatchMover` на 1000 файлів — час < 10 с. **Відкладено.**

### 13.3 Acceptance
- Покриття нової логіки ≥ 80%, критичних модулів (postprocessor, undo, trim audio) — 100%.
- CI зелений з усім test set.

### Примітка: чому інтеграційні тести не реалізовані

`BatchMover`, `UndoEngine`, `AssetRouterPostprocessor` і round-trip з action sub-assets залежать від `AssetDatabase.MoveAsset` та фізичних файлів на диску. Їх неможливо замокати — потрібно створювати тимчасові ассети в `Tests/Fixtures~/`, запускати реальний importer і прибирати за собою у `[TearDown]`. Це можливо, але ненадійно: cleanup failure залишає сміття в проєкті, race conditions між паралельними тестами в CI ламають результати. Вирішено не писати ці тести зараз, бо ризик flaky CI перевищує цінність покриття на цьому етапі. Повернутись до них варто коли буде виділений тестовий Unity проєкт або sandbox environment.

---

## ✅ Epic 14 — Documentation overhaul (English + per-action docs)

> ✅ **Виконано — v0.9.0**

### 14.1 English baseline
- [x] `DOCUMENTATION.md` перейменовано в `DOCUMENTATION_UA.md` (secondary). Нова `DOCUMENTATION_EN.md` — primary EN docs (~500 рядків): flow пояснення, Settings window, усі 4 вкладки, pattern syntax (glob/regex з таблицею), JSON export/import, troubleshooting.
- [x] `Documentation~/index.md` — UPM entry point, лінки на всі розділи.
- [x] `README.md` переписаний: виправлено застаріле prefix/suffix на glob patterns, актуальна структура пакета з Actions/Runtime/Wizard, таблиця 10 actions, лінки на документацію.
- [x] `Samples~/QuickStart/README.md` оновлено: актуальна таблиця всіх 10 actions, додано три use-case секції в кінці з лінками.
- [x] `CHANGELOG.md` — вже англійською, перевірено.

### 14.2 Per-action documentation
- [x] `Documentation~/actions/README.md` — індекс з таблицею (Action / Applies to / What it does / Tier) та поясненням tier system.
- [x] `Documentation~/actions/SetPivotAction.md`
- [x] `Documentation~/actions/TrimAudioSilenceAction.md` — найдетальніша: RIFF формат, stereo trim, re-entry guard, edge cases.
- [x] `Documentation~/actions/AppendToCatalogAction.md` — O(N) caveat задокументований.
- [x] `Documentation~/actions/RegisterAddressableAction.md` — UNITY_ADDRESSABLES define, deferred save caveat.
- [x] `Documentation~/actions/EmitUnityEventAction.md`
- [x] `Documentation~/actions/CreatePrefabFromTemplateAction.md` — showpiece: flow, IAssetRouterPrefabSetup callback, namePattern токен, overwriteExisting.
- [x] `Documentation~/actions/CreateScriptableObjectFromTemplateAction.md`
- [x] `Documentation~/actions/CreateMaterialFromTextureAction.md` — таблиця shader property names по шейдерах.
- [x] `Documentation~/actions/GenerateSpritePhysicsShapeAction.md` — Read/Write requirement, bounding box (не tight outline).
- [x] `Documentation~/actions/GenerateNineSliceBordersAction.md` — повністю opaque edge case задокументований.
- [x] `Documentation~/actions/CreateTilePaletteEntryAction.md` — sprite sheet edge case (тільки перший sub-asset).
- [x] `Documentation~/actions/LegacySamples.md` — пояснення чому GenerateMeshCollider і RunMenuItem перенесені в Samples~.

Формат кожної сторінки: Applies to / Tier / Configuration table / How it works / Idempotency / Requirements / Edge cases / Example.

### 14.3 API reference
- [x] `Documentation~/api/extension-points.md` — повний гайд: контракт IAssetImportAction, чому ScriptableObject а не plain class, AssetImportContext поля, CanRunOn vs Execute семантика, re-entry guard pattern, мінімальний приклад, assembly setup, scaffolding wizard, error isolation.
- [ ] DocFX генерація — не реалізовано. XMLDoc достатній; окремий markdown API generation відкладено до post-v1.0.0.

### 14.4 Migration guides
- [x] `Documentation~/migrations/v1-to-v2-schema.md` — таблиця до/після, формула комбінування трьох полів, автоматичність, незворотність, як перевірити.
- [ ] `migrations/v0.4-to-v0.5.md` — Newtonsoft.Json dependency guide — не реалізовано. Вирішено: зміна не breaking для більшості проєктів, CHANGELOG entry достатньо.

### 14.5 Use case docs
- [x] `Documentation~/use-cases/mobile-team.md` — 4-person team, URP, scopeFolder для двох артистів, JSON в git, Addressables group.
- [x] `Documentation~/use-cases/legacy-cleanup.md` — workflow: Validate tab -> правила -> Dry Run малими batch -> History undo. MoveAsset зберігає references — задокументовано.
- [x] `Documentation~/use-cases/solo-developer.md` — мінімальний setup: тільки змінити targetFolder. Коли додавати actions (не з першого дня).

### 14.6 Release blocker rule
- [x] `CONTRIBUTING.md` — PR вимоги: action без docs не мерджиться, API change без XMLDoc не мерджиться, CHANGELOG обов'язковий. Також: serialization rules ([MovedFrom], [FormerlySerializedAs]), atomic write rule.
- [x] `RELEASE_CHECKLIST.md` — checklist з трьох секцій: Code/tests, Documentation, Version/release. 12 пунктів.

### 14.7 Testing guide для extension authors
- [x] `Documentation~/testing-your-actions.md` — asmdef setup, CreateInstance pattern, побудова AssetImportContext для тесту, тести CanRunOn/Execute/idempotency/error isolation, helper method шаблон, таблиця мінімального покриття.
- [x] `Tests/Actions/_ExampleActionTest.cs` — exemplar тест на `AppendToCatalogAction` (6 тестів), навмисно прокоментований як template для extension authors. SetUp/TearDown, три CanRunOn тести, два Execute тести, MakeContext helper.
- [ ] Testing example секція в кожній action page — не додано в усі сторінки, є лінк в `testing-your-actions.md` на `_ExampleActionTest.cs`.

### XMLDoc (додатково до плану)
Додано до всіх публічних типів: `IAssetImportAction`, `AssetImportActionAsset`, `AssetImportContext` (всі поля + constructor), `PatternMode` (enum + обидва значення), `BaseImportRule` (всі публічні поля), `ImportRule`, `ImporterSettingsDatabase` (всі поля), `AssetCatalog`, `IAssetRouterPrefabSetup`, `IAssetRouterDataSetup`, всі 10 built-in action класів і їх поля.

### 14.8 Acceptance
- [x] Англомовний користувач відкриває GitHub repo і за 5 хв розуміє що це + як поставити + як працює перший use case.
- [x] Для кожного з 10 built-in actions є окрема markdown сторінка з прикладом.
- [x] Не лишилось публічного класу/методу без XMLDoc.
- [x] Release process включає документаційний gate (CONTRIBUTING.md + RELEASE_CHECKLIST.md).

---

## ✅ Epic 15 — Action library: showcase spectrum (top 10)

> ✅ **Виконано — v0.7.0** (Runtime asmdef, 7 нових actions, scaffolding wizard, 2 actions → LegacyActions sample)

> **Філософія:** Solo dev не може покрити ВСІ потреби — це безнадійно. Натомість мета v1.0.0 — **продемонструвати спектр**: 10 actions різних архітектурних патернів, від найпростішого до складного. Програміст дивиться, розуміє, копіпейстить найближчий і пише власний. v1.0.0 ≠ "тут є все" → v1.0.0 = "ось як це робиться, далі сам".

### 15.0 Spectrum coverage — що кожен action демонструє

| # | Action | Tier | Що демонструє |
|---|---|---|---|
| 1 | **SetPivotAction** | A | Найпростіший — tweak importer поля. "Hello world" actions. |
| 2 | **AppendToCatalogAction** | B | Cross-asset registry (SO як база). |
| 3 | **RegisterAddressableAction** | C | Optional package dependency через `versionDefines`. |
| 4 | **EmitUnityEventAction** | D | Inspector escape hatch — no-code customization. |
| 5 | **CreatePrefabFromTemplateAction** ⭐ | E | Factory pattern + user callback interface. |
| 6 | **CreateScriptableObjectFromTemplateAction** | E | Той самий factory pattern для SO (showcase reusability). |
| 7 | **CreateMaterialFromTextureAction** | E | Третій factory приклад — material. Робить паттерн "видимим". |
| 8 | **GenerateSpritePhysicsShapeAction** | F | Smart inference з контенту (polygon outline). |
| 9 | **GenerateNineSliceBordersAction** | F | Інший стиль inference (transparent edges → 9-slice). |
| 10 | **CreateTilePaletteEntryAction** | G | Інтеграція з Unity sub-feature (Tilemap). |

**Архітектурні tiers:**
- **A** — Trivial importer field setter (5 рядків коду)
- **B** — Aggregation / registry into SO
- **C** — Optional external package (compile-time gate)
- **D** — No-code Inspector hook (UnityEvent)
- **E** — Factory + user-defined callback interface ← **3 приклади тому що це найсильніший паттерн**
- **F** — Content inference (читай асет → виведи налаштування)
- **G** — Unity-specific feature integration

Дивлячись на 10, розробник бачить: "Моя задача — між F і E, беру приклади 5 + 8".

---

### 15.1 Deprecation: existing actions що НЕ потрапили у showcase

Поточні actions які або повторюють tier іншого actions, або поза 2D-focus. Не видаляємо — переносимо у `Samples~/LegacyActions/`. Користувач може import-нути sample якщо потрібно. Це не breaking change, бо v0.5.0 на public registries ще не публікувався.

- [x] **GenerateMeshColliderAction** — 3D + дублює Tier A. Перенесено → `Samples~/LegacyActions/GenerateMeshColliderAction.cs`.
- [x] **TrimAudioSilenceAction** — **рішення: залишено в core пакеті.** Баги виправлені в Epic 11 (overflow, atomic write), повне unit-покриття (14 тестів). Перенесення в Samples~ скасовано — hardening вже зроблено.
- [x] **RunMenuItemAction** — замінений `EmitUnityEventAction`. Перенесено → `Samples~/LegacyActions/RunMenuItemAction.cs`.

**Тести існуючих legacy actions** (`ActionPipelineTests` тощо) — теж переносяться у sample або залишаються як edge-case проби.

---

### 15.2 New actions — v0.8.0 implementation

#### 15.2.1 ⭐⭐⭐ CreatePrefabFromTemplateAction (showpiece)

**Demonstrates: Tier E (factory + user interface callback)**

- [x] Створено **Runtime asmdef** (`Runtime/AssetRouter.Runtime.asmdef`).
- [x] Інтерфейс у Runtime: `IAssetRouterPrefabSetup { void SetupAssetRouter(Object, string assetPath); }` — спрощена сигнатура без `AssetImportContext` (Editor-тип не може бути в Runtime assembly).
- [x] Action поля: `templatePrefab`, `outputFolder`, `namePattern`, `overwriteExisting`.
- [x] Execute flow реалізовано (InstantiatePrefab → setup callback → SaveAsPrefabAsset → DestroyImmediate).
- [ ] Sample у `Samples~/PrefabTemplateExample/` — відкладено до Epic 14.

#### 15.2.2 ⭐⭐⭐ CreateScriptableObjectFromTemplateAction (reuses pattern)

**Demonstrates: Tier E — той самий patterning для SO. Підкреслює, що patterns extensible.**

- [x] Інтерфейс у Runtime: `IAssetRouterDataSetup { void SetupAssetRouter(Object, string assetPath); }`.
- [x] Поля: `template` (ScriptableObject), `outputFolder`, `namePattern`, `overwriteExisting`.
- [x] Execute: `Instantiate(template)` → setup callback → `AssetDatabase.CreateAsset`.
- [ ] Sample: `ItemData.cs` — відкладено до Epic 14.

#### 15.2.3 ⭐⭐ CreateMaterialFromTextureAction (reuses pattern)

**Demonstrates: Tier E ще раз — щоб патерн "factory + callback" вкорінився як ідея.**

- [x] Поля: `baseMaterial`, `textureProperty`, `outputFolder`, `namePattern`, `overwriteExisting`.
- [x] Execute: `new Material(baseMaterial)` → `SetTexture` → `AssetDatabase.CreateAsset`.

#### 15.2.4 ⭐⭐ EmitUnityEventAction (replaces RunMenuItem)

**Demonstrates: Tier D — як зробити no-code extension через Inspector. Найкращий escape hatch.**

- [x] Поле: `[SerializeField] ImportedAssetEvent _onImport` (`UnityEvent<Object>` — Inspector-friendly, без Editor-типів у параметрі).
- [x] Execute: `_onImport?.Invoke(importedAsset)`. CanRunOn перевіряє `GetPersistentEventCount() > 0`.

#### 15.2.5 ⭐⭐ GenerateSpritePhysicsShapeAction

**Demonstrates: Tier F — smart inference (читай pixel data → згенеруй outline).**

- [x] Налаштування: `float alphaThreshold = 0.1f` (0..1).
- [x] Читає `Texture2D.GetPixels` → знаходить bounding box опакових пікселів → `Sprite.OverridePhysicsShape`. Потребує Read/Write на текстурі.

#### 15.2.6 ⭐⭐ GenerateNineSliceBordersAction

**Demonstrates: Tier F — інший стиль content inference (find transparent borders → 9-slice).**

- [x] Scan pixels → `TextureImporter.spriteBorder = new Vector4(left, bottom, right, top)`. Потребує Read/Write.
- [x] `float alphaThreshold = 0.1f` (0..1) як налаштування.

#### 15.2.7 ⭐⭐ CreateTilePaletteEntryAction

**Demonstrates: Tier G — Unity sub-feature integration (Tilemap).**

- [x] Поля: `outputFolder`, `namePattern`, `overwriteExisting`, `tileColor`.
- [x] Execute: `LoadAssetAtPath<Sprite>` → `CreateInstance<Tile>` → `tile.sprite` → `AssetDatabase.CreateAsset`. Tile asset зберігається в outputFolder.

---

### 15.3 Final lineup для v0.8.0 (10 actions)

| Tier | Action | Effort | Showcase value |
|---|---|---|---|
| A | SetPivot ✅ (kept) | 0 (shipped) | "Найпростіше виглядає так" |
| B | AppendToCatalog ✅ (kept) | 0 (shipped) | "Cross-asset registry виглядає так" |
| C | RegisterAddressable ✅ (kept) | 0 (shipped) | "Optional dep виглядає так" |
| D | **EmitUnityEvent** (new) | S | "No-code extension виглядає так" |
| E | **CreatePrefabFromTemplate** (new) ⭐ | L | "Factory pattern + interface" |
| E | **CreateScriptableObjectFromTemplate** (new) | M | "Той самий патерн для SO" |
| E | **CreateMaterialFromTexture** (new) | S | "Той самий патерн для material" |
| F | **GenerateSpritePhysicsShape** (new) | M | "Content inference: pixels → outline" |
| F | **GenerateNineSliceBorders** (new) | S | "Content inference: transparent edges → border" |
| G | **CreateTilePaletteEntry** (new) | M | "Unity feature integration" |

**Net change:** −3 (deprecated), +7 (new) = 10 в core пакеті + 3 у sample LegacyActions.

### 15.4 Action Scaffolding — "Create New Action" wizard

**Мета:** zero-ceremony створення user action. Замість "почитай docs → створи .cs файл → пам'ятай наслідувати → пам'ятай `[CreateAssetMenu]` → пам'ятай namespace" — натиснув "Create" → отримав готовий шаблон з TODO коментарями.

Реалізація через `[MenuItem("Assets/Create/Asset Router/New Action.../...")]`. У підменю — 4 варіанти що мапляться на архітектурні tier'и з 15.0:

- [x] **`New Action.../Basic Action`** — stub з `CanRunOn = true` + порожній Execute.
- [x] **`New Action.../Texture Filter Action`** — перевіряє `TextureImporter` + `ImportAsset(ForceUpdate)`.
- [x] **`New Action.../Sprite Factory Action`** — factory: `LoadAssetAtPath<Sprite>` + `CreateAsset`.
- [x] **`New Action.../Prefab Factory Action`** — factory: `InstantiatePrefab` → callback → `SaveAsPrefabAsset`.

**Технічна реалізація:**
- Шаблони embedded як `const string` у `Editor/Wizard/ActionScaffoldingWizard.cs` (надійніше ніж зовнішні .txt файли — без проблем з path resolution при встановленні через UPM).
- `[MenuItem]` → `EditorUtility.SaveFilePanelInProject` → замінює `{{ACTION_NAME}}` / `{{NAMESPACE}}` → `File.WriteAllText` → `AssetDatabase.Refresh()`.

**Чому це варто:** головний бар'єр для extension — не "не вмію писати C#", а "не знаю які поля/атрибути/наслідування потрібні". Шаблон знімає цей бар'єр повністю. Це не overengineering — це **30 рядків коду + 4 шаблонні файли** для зняття 90% friction.

**Acceptance:**
- 4 варіанти у Create menu.
- Згенерований Factory шаблон компілюється з коробки і його action одразу видно в `+` dropdown списку actions.

### 15.5 Acceptance (overall)
- [x] 10 actions у `Editor/Actions/BuiltIn/` — всі 7 нових + 3 існуючих (SetPivot, AppendToCatalog, RegisterAddressable).
- [x] Unit/edit-mode тести — `NewActionsTests.cs` (15 тестів): CanRunOn null-guards + pixel analysis (ComputeBorder).
- [ ] ~~XMLDoc на public API~~ — відкладено до Epic 14.
- [ ] ~~`Documentation~/actions/<Name>.md`~~ — відкладено до Epic 14.
- [ ] ~~`Samples~/PrefabTemplateExample/`~~ — відкладено до Epic 14.
- [x] `Samples~/LegacyActions/` з README — GenerateMeshCollider + RunMenuItem перенесено, README пояснює причини.
- [x] Scaffolding wizard — 4 шаблони у `Editor/Wizard/ActionScaffoldingWizard.cs`, генерують компільований код.

---

## ✅ Epic 16 — Path Templating (capture group tokens у targetFolder)

> ✅ **Виконано — v0.9.1** (реліз) + **v0.9.2** (bugfixes: `**/` segment boundary, non-participating group → empty замість literal `{n}`, Windows-санітизація captured значень у `TargetResolver.Sanitize`)

Одне правило покриває multi-dimensional naming: `Tex_Location_3_Bus_7.png` сам резолвиться у `Assets/Locations/3/Bus_7/` без потреби писати правило на кожну комбінацію.

### 16.1 Чому

Модель "literal pattern → literal target" ламається на multi-dimensional naming. N незалежних осей в імені файлу = добуток правил (50 локацій × 10 типів = 500 правил). Плагін втрачає сенс на такому масштабі.

Industry-стандарт: webpack (`[name]`, `[path]`), Azure DevOps (`$(var)`), daihenka/asset-pipeline (`{varname}`) — captures у pattern + tokens у target. Перевірений підхід.

### 16.2 Що

**Pattern** — синтаксис не змінюється для користувача, внутрішнє представлення додає captures:
- **Glob**: кожен `*` і `**` стає capture group (`T_Loc_*_Bus_*` → внутрішньо `^T_Loc_([^/]*)_Bus_([^/]*)$`). `?` залишається без захоплення.
- **Regex**: користувач сам пише `(?<loc>\d+)` або `(\d+)`. Стандартний синтаксис .NET.

**Target Folder** отримує підтримку токенів:
- `{1}`, `{2}`, … — позиційні (1-based), посилаються на n-ий capture group.
- `{name}` — іменовані (Regex mode з `(?<name>…)`).
- `{{`, `}}` — escape для буквальних фігурних дужок (як у .NET `string.Format`).

| Pattern (mode) | Target | Файл | Резолвиться у |
|---|---|---|---|
| `Tex_Loc_*_*` (Glob) | `Assets/Locations/{1}/{2}/` | `Tex_Loc_Forest_Tree.png` | `Assets/Locations/Forest/Tree/` |
| `^T_(?<loc>\w+)_.*` (Regex) | `Assets/Art/{loc}/` | `T_Forest_Tree.png` | `Assets/Art/Forest/` |
| `Tex_*_Bus_5.png` (Glob) | `Assets/Shared/Bus_5/` | `Tex_Location_2_Bus_5.png` | `Assets/Shared/Bus_5/` (без токенів — буквально) |

**Backward-compatible:** target без `{...}` поводиться 1:1 з v0.9.0. Існуючі бази працюють без міграції, `schemaVersion` не бампається.

### 16.3 Як (технічно)

Три touch-points у Logic layer:

**(1) `PatternMatcher`** — новий метод `Match(rule, path) → System.Text.RegularExpressions.Match`. Старий `Matches(rule, path) → bool` залишається як thin wrapper (`Match(...) != null && .Success`) — щоб ConflictDetector overlap-евристика і існуючі сайти не зачіпались. `GlobToRegex` міняється:

```
[^/]*  → ([^/]*)    // *
.*     → (.*)       // **
[^/]   → [^/]       // ? — без захоплення
```

Кеш `_compiledPattern` / `_compiledFor` працює як був.

**(2) `RuleValidator.FindMatchingRule`** — повертає нову internal struct `RuleMatch { BaseImportRule Rule; Match Match; }` замість `BaseImportRule`. Null check на `RuleMatch?`. Sites: `AssetRouterPostprocessor.OnPreprocessAsset` / `OnPostprocessAllAssets`, `DryRunPlanner.Scan`.

**(3) `TargetResolver`** — **новий** `Editor/Logic/TargetResolver.cs`:
- `internal static string Resolve(string template, Match match)` — один прохід:
  - `{{` / `}}` → escape, виводить літеральний `{` / `}`.
  - `{n}` (число) → `match.Groups[n].Value`; якщо групи нема або capture порожній — warning один раз на правило, в target залишається `{n}` буквально (видимий фейл у Diagnostic Window).
  - `{name}` → `match.Groups[name].Value` за тим самим принципом.
- Санітизація підставленого значення: заборона `..`, backslash, leading `/`. На сумнівних значеннях — warning і fallback до буквального template.

**Move-сайти:**
- `AssetRouterPostprocessor.MoveToTargetFolder` приймає Match, кличе `TargetResolver.Resolve` перед обчисленням `targetPath`.
- `DryRunPlanner.Scan` резолвить target одразу при побудові `DryRunEntry` (preview бачить реальний шлях).
- `BatchMover.Move` нічого не змінює — отримує вже-резолвлений `DryRunEntry.TargetPath`.
- `OperationLog` пише резолвлений path як `to`; `UndoEngine` працює без змін (буквальний reverse-move).

### 16.4 Backward compatibility & edge cases

- **Target без токенів** — fast path, парсер не запускається. Жодного оверхеду для існуючих правил.
- **Token без відповідного capture** — log warning один раз, target резолвиться буквально. Фігурні дужки в назві папки → видимий фейл, дев'юзер одразу побачить у Diagnostic Window.
- **Empty capture** (optional regex group) — підставляє empty string; `Assets//file.png` нормалізатор схлопне до `Assets/file.png`.
- **Capture містить `/`** (від `**` у Glob або `.*` у Regex) — допустимо: artist може свідомо мати nested naming. Санітизація прибирає тільки `..` і leading `/`, backslash.
- **JSON export/import** — target залишається рядком, токени серіалізуються as-is. Жодних змін у `JsonExporter`/`JsonImporter`.
- **ConflictDetector** — overlap heuristic не міняється, бо викликає `PatternMatcher.Matches` (legacy wrapper).
- **scopeFolder + templating** — незалежні: scopeFolder фільтрує до match, templating резолвить target з match.
- **`{0}`** — за конвенцією .NET це whole match. Документуємо, але не рекомендуємо у користувацьких правилах.

### 16.5 Default rules — додаємо 2 демонстраційних

Оновлюємо `DefaultDatabaseFactory.CreateDefaultRules()`. Нові правила вставляються **перед** generic `T_*` (порядок виграє, more-specific first):

| # | Rule Name | Mode | Pattern | Target | Preset |
|---|---|---|---|---|---|
| 1 | UI Textures | Glob | `UI_*` | `Assets/Art/UI/` | TextureImporter_UI |
| 2 | **Character Textures** (NEW) | Glob | `T_Char_*_*` | `Assets/Art/Characters/{1}/` | TextureImporter |
| 3 | **Location Textures** (NEW) | Regex | `^T_Loc_(?<loc>\w+)_.*` | `Assets/Art/Locations/{loc}/` | TextureImporter |
| 4 | General Textures | Glob | `T_*` | `Assets/Art/Textures/` | TextureImporter |
| 5 | Sound Effects | Glob | `SFX_*` | `Assets/Audio/SFX/` | AudioImporter |
| 6 | Music | Glob | `Mus_*` | `Assets/Audio/Music/` | AudioImporter_Music |

**Що демонструють:**
- **Character Textures** — позиційний glob capture (`{1}`). `T_Char_Hero_Diffuse.png` → `Assets/Art/Characters/Hero/`. Нову папку під персонажа створювати не треба.
- **Location Textures** — regex named capture (`{loc}`). `T_Loc_Forest_Tree.png` → `Assets/Art/Locations/Forest/`. Показує regex-режим для тих, хто не хоче Glob.

**Важливо:** існуючі бази v0.9.0 **не апгрейдяться автоматично** — це порушує user data. Нові правила з'являються тільки при `Create New Database` або при Inspector `Reset()` на SO. Документація v0.9.1 згадує "if you want the new demo rules, create a fresh database via the toolbar".

### 16.6 UI зміни

- **Tooltip на полі Target Folder** — пояснення синтаксису `{1}`, `{name}`, `{{`/`}}`, посилання на documentation.
- **Live Preview оновлюється** — окрім "✓ e.g. matches" показує **резолвлені targets**:
  ```
  ✓ T_Char_Hero_Diffuse.png    → Assets/Art/Characters/Hero/
    T_Char_Boss_Diffuse.png    → Assets/Art/Characters/Boss/
  ```
- **Error у preview** — якщо target має невідомий token, рядок підсвічений червоним: `⚠ token '{loc}' not found in pattern captures`. Те ж саме повертає `TargetResolver` валідатор.
- Жодних нових полів у Rule Detail panel — все живе в існуючих `pattern` і `targetFolder`.

### 16.7 Документація

- `Documentation~/DOCUMENTATION_EN.md` — нова секція "Path Templating" після "Pattern syntax". Grammar table, приклади, edge cases.
- `Documentation~/api/path-templating.md` — **новий** reference: full grammar, escape rules, token resolution flow, troubleshooting (warning messages explained).
- `Documentation~/index.md` — посилання на нову сторінку у списку.
- `Documentation~/use-cases/mobile-team.md` — оновити секцію "Scope folder for two artists": templating як альтернатива scopeFolder для destination branching. Залишити scopeFolder приклад для source filtering.
- `Documentation~/use-cases/legacy-cleanup.md` — додати секцію "Use Path Templating when файли мають structured naming".
- `Documentation~/use-cases/solo-developer.md` — згадка про templating як "next step after basic rules".
- `Samples~/QuickStart/README.md` — додати приклади `T_Char_Hero_D.png` і `T_Loc_Forest_Rock.png` що демонструють новий рулсет.
- `README.md` — у "How it works" коротко згадати templating з посиланням на documentation.
- `CHANGELOG.md` — повний entry для v0.9.1.

### 16.8 Тести

- **`PatternMatcherTests`** (+5 тестів):
  - `Match_GlobWithStar_CapturesPositionalValue`
  - `Match_GlobWithDoubleStar_CapturesPath`
  - `Match_RegexNamedGroup_CapturesByName`
  - `Match_NoMatch_ReturnsNull`
  - `Matches_LegacyWrapper_StillReturnsBool` (regression — щоб не зламати ConflictDetector)
- **`TargetResolverTests`** (**новий** файл, ~10 тестів):
  - `Resolve_NoTokens_ReturnsLiteral`
  - `Resolve_PositionalToken_SubstitutesCapture`
  - `Resolve_NamedToken_SubstitutesCapture`
  - `Resolve_MissingToken_LogsWarningReturnsLiteral`
  - `Resolve_EscapedDoubleBraces_RendersLiterally`
  - `Resolve_EmptyCapture_RendersEmptyString`
  - `Resolve_TokenContainsDotDot_Sanitized`
  - `Resolve_TokenContainsBackslash_Sanitized`
  - `Resolve_MultipleTokensInTemplate_AllSubstituted`
  - `Resolve_NullMatch_ReturnsTemplateLiterally`
- **`RuleValidatorTests`** — оновити виклики під нову `RuleMatch` struct (всі ~19 існуючих тестів продовжують працювати з мінімальною правкою).
- **`DefaultDatabaseFactoryTests`** (**новий** файл, ~3 тести):
  - `CreateDefaultRules_ContainsCharacterTexturesRule`
  - `CreateDefaultRules_ContainsLocationTexturesRule`
  - `CreateDefaultRules_SpecificRulesBeforeGenericTRule` (order regression — critical!)

### 16.9 Файли

- `Editor/Logic/PatternMatcher.cs` — `Match` метод, capturing `GlobToRegex`.
- `Editor/Logic/RuleValidator.cs` — `RuleMatch` struct, оновити `FindMatchingRule`.
- `Editor/Logic/TargetResolver.cs` — **новий**.
- `Editor/Logic/AssetRouterPostprocessor.cs` — `MoveToTargetFolder` приймає Match.
- `Editor/Logic/DryRunPlanner.cs` — резолвить target у DryRunEntry.
- `Editor/Data/DefaultDatabaseFactory.cs` — +2 нові правила у правильному порядку.
- `Editor/View/AssetRouterWindow.RuleDetail.cs` — preview резолвить target, tooltip на target field.
- `Tests/PatternMatcherTests.cs` — +5 тестів.
- `Tests/TargetResolverTests.cs` — **новий**.
- `Tests/RuleValidatorTests.cs` — оновити сигнатури викликів.
- `Tests/DefaultDatabaseFactoryTests.cs` — **новий**.
- `Documentation~/DOCUMENTATION_EN.md` — оновити.
- `Documentation~/api/path-templating.md` — **новий**.
- `Documentation~/index.md` — оновити список.
- `Documentation~/use-cases/mobile-team.md`, `Documentation~/use-cases/legacy-cleanup.md`, `Documentation~/use-cases/solo-developer.md` — оновити.
- `Samples~/QuickStart/README.md` — додати демо-файли.
- `README.md` — згадка про templating.
- `CHANGELOG.md` — entry для v0.9.1.
- `package.json` — bump version на `0.9.1`.

### 16.10 Acceptance

- Існуюча база v0.9.0 запускається без міграції; правила без `{` у target поводяться 1:1.
- Drop `T_Char_Hero_D.png` у новий проєкт → файл їде у `Assets/Art/Characters/Hero/` з застосованим TextureImporter preset.
- Drop `T_Loc_Forest_Rock.png` → `Assets/Art/Locations/Forest/`.
- Drop `T_Random.png` → `Assets/Art/Textures/` (catch-all).
- Live Preview у Rule Detail показує резолвлені шляхи для перших ~3 matching файлів.
- Помилковий target (наприклад `{nonexistent}`) — warning у Console + папка з фігурними дужками у файловій системі (видимий фейл, легко діагностувати).
- Dry Run показує резолвлені targets; Apply Selected рухає файли коректно.
- History session містить резолвлений path; Undo повертає файл назад.
- JSON export / import — round-trip preservation токенів (вони серіалізуються як буквальний рядок).
- Diagnostic Window показує rule name + резолвлений target.
- CI: всі ~150 тестів зелені (129 існуючих + ~20 нових).
- Документація: новий користувач за 2 хв розуміє `{name}` синтаксис з прикладами у `DOCUMENTATION_EN.md`.

---

## Suggested release order

- [x] **v0.1.0** — Epic 9 (cleanup + bugfixes + CI). Стабільний фундамент.
- [x] **v0.2.0** — Epic 1 (pattern matching) + Epic 5 (conflict detection).
- [x] **v0.3.0** — Epic 2 (import actions). Перший великий стрибок гнучкості.
- [x] **v0.4.0** — Epic 3 (dry-run) + Epic 4 (batch re-import) + Epic 6 (undo) — пак "team-safety".
- [x] **v0.5.0** — Epic 7 (JSON export) + Epic 8 (bundled content + sample). Готовий до публікації.
- [x] **v0.6.0** — Epic 11 (critical bugs) + Epic 13 (test coverage, unit-only).
- [x] **v0.7.0** — Epic 15 (showcase spectrum: 7 нових actions + 2 deprecated → LegacyActions + scaffolding wizard + Runtime asmdef).
- [x] **v0.8.0** — Epic 10 (per-folder scope, diagnostic window, statistics, naming validator, double-apply protection).
- [x] **v0.9.0** — Epic 14 (documentation overhaul: EN translation + per-action docs + use case guides).
- [x] **v0.9.1** — Epic 16 (Path Templating: capture group tokens у targetFolder + 2 нові default rules + документація). **v0.9.2** — bugfix-реліз за результатами повного code review (див. `Documentation~/bugfix.md`).
- [ ] **v1.0.0** — Epic 12 (Asset Store / OpenUPM release readiness) + стабілізація + публікація.
- [ ] **v1.1.0+** — нові actions за feedback + Epic 10 ідеї що не потрапили у v0.8.0.

**Release gate (no exceptions):**
1. Усі тести зелені на CI.
2. `TEST.md` прогнано вручну, без блокерів.
3. Документація актуальна для всіх змін у релізі.
4. `CHANGELOG.md` оновлений з повним release note.
5. `package.json` version узгоджена з git tag.

Кожен реліз — тег у git, оновлення `package.json` version, оновлення `CHANGELOG.md`, GitHub Release notes.

---

## Open questions перед стартом

1. **Назва**: лишаємо `Asset Router` чи перейменовуємо на `SmartAssetImporter` як у нотатках?
2. **Newtonsoft.Json**: додаємо як обовʼязкову залежність (Epic 7), чи робимо JSON export опціональним без зовнішніх deps (власний writer)?
3. **Runtime folder**: створюємо одразу зі скелетом (під майбутній `AppendToCatalogAction`) чи додаємо тоді коли реально потрібен?
4. **CI provider**: GitHub Actions (free для public репо) ок?
5. **Target Unity**: підтримуємо тільки 2022.3 LTS, чи додаємо 2021.3 LTS і 6.0+?
