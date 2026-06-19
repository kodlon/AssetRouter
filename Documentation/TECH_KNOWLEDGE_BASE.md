# Tech Knowledge Base — Asset Router

---

## AssetPostprocessor

**Що це:**
Вбудований Unity клас, який дозволяє перехопити момент імпорту ассету і змінити його налаштування до того, як Unity збереже результат. Не треба нічого підключати — Unity сам знаходить всі класи що успадковують `AssetPostprocessor` і викликає їх методи автоматично.

**Ключові методи:**
| Метод | Коли викликається |
|-------|-------------------|
| `OnPreprocessAsset()` | Для будь-якого ассету, до імпорту |
| `OnPreprocessTexture()` | Тільки для текстур, до імпорту |
| `OnPreprocessModel()` | Тільки для моделей (.fbx, .obj), до імпорту |
| `OnPostprocessAllAssets()` | Після імпорту всіх ассетів (static метод) |

**В проекті:**
`AssetRouterPostprocessor.cs` — реалізує `OnPreprocessAsset()` (знаходить правило, застосовує preset) і `OnPostprocessAllAssets()` (переміщує ассет у target folder).

---

## Unity Preset

**Що це:**
Asset (`.preset` файл) що зберігає знімок всіх налаштувань певного Unity об'єкта. Застосовується одним викликом:

```csharp
preset.ApplyTo(targetObject); // true — застосовано, false — тип не збігається
```

**Чому краще за ручне присвоєння полів:**
- Unity додає нові поля до імпортера у новій версії — preset підхоплює автоматично
- Налаштування редагує TA або артист через Inspector, не через код
- Кілька варіантів конфігурації = кілька `.preset` файлів

---

## ScriptableObject

**Що це:**
Спеціальний Unity клас для зберігання даних як окремого `.asset` файлу. Не прив'язаний до сцени чи GameObject.

**Чому ScriptableObject, а не JSON/PlayerPrefs:**
- JSON треба парсити вручну, нема Inspector UI
- PlayerPrefs — для runtime налаштувань гравця, не для Editor конфігу
- ScriptableObject — серіалізується Unity, є Inspector, переноситься між проектами як .asset файл

**В проекті:**
`ImporterSettingsDatabase` зберігає всі правила імпорту, список розширень, список ігнорованих папок.

---

## [SerializeReference]

**Що це:**
Атрибут Unity (з 2019.3) для серіалізації поліморфних об'єктів. Зберігає реальний тип елементів у `List<BaseClass>` після перезапуску редактора.

**В проекті:**
```csharp
[SerializeReference]
public List<BaseImportRule> rules = new List<BaseImportRule>();
```
Дозволяє додати новий підтип правил без змін в `ImporterSettingsDatabase`.

**Обмеження:** клас має бути `[Serializable]`. При перейменуванні класу збережені дані ламаються — потрібен `[MovedFrom]`.

---

## [InitializeOnLoad]

**Що це:**
Атрибут що змушує Unity викликати static constructor при кожному старті Editor і після рекомпіляції скриптів.

**В проекті:**
`AssetRouterInitializer` при старті перевіряє чи існує база даних, і якщо ні — створює дефолтну з 4 правилами.

```csharp
[InitializeOnLoad]
public static class AssetRouterInitializer
{
    static AssetRouterInitializer() => EditorApplication.delayCall += CreateDefaultDatabaseIfMissing;
}
```

**Чому `delayCall`:** Static constructor може викликатись до того як `AssetDatabase` повністю готовий. `delayCall` відкладає виконання до наступного кадру.

---

## ReorderableList

**Що це:**
Клас з `UnityEditorInternal` для списків в EditorWindow з drag-and-drop переставленням.

**Чому не просто `PropertyField` для масиву:**
Стандартний `PropertyField` не дає drag-and-drop переставлення — а це критично для пріоритетів правил (перше правило що матчиться виграє).

---

## AssetDatabase

**В проекті використовується для:**
- `FindAssets("t:ImporterSettingsDatabase")` — знайти базу даних
- `LoadAssetAtPath<Preset>(path)` — завантажити preset
- `MoveAsset(from, to)` — перемістити ассет (зберігає GUID і .meta)
- `CreateAsset(obj, path)` — зберегти ScriptableObject
- `IsValidFolder(path)` / `CreateFolder(parent, name)` — робота з папками

**Чому `AssetDatabase.MoveAsset`, а не `System.IO.File.Move`:**
`File.Move` переміщує файл на диску але Unity не знає — .meta залишається на місці, GUID ламається, всі references руйнуються. `AssetDatabase.MoveAsset` переміщує файл і .meta разом, GUID зберігається.

---

## HashSet (захист від infinite loop)

**В проекті:**
`static HashSet<string> AssetsBeingMoved` в `AssetRouterPostprocessor`. Перед переміщенням додаємо target path — при повторному імпорті `Contains()` повертає true і обробка пропускається.

**Чому static:** `AssetPostprocessor` створюється як новий instance для кожного імпорту. Instance-поле скидалось би кожного разу.

**Чому HashSet, а не List:** `Contains()` — O(1) проти O(n). При bulk import різниця відчутна.

---

## Abstract клас vs Interface (BaseImportRule)

**Чому abstract клас:**
Interface не може мати поля з даними. Спільні поля (`ruleName`, `prefix`, `suffix`, `extensionFilter`, `targetFolder`) жили б тільки в abstract класі. Плюс `[Serializable]` на abstract класі дозволяє Unity серіалізувати всі нащадки через `[SerializeReference]`.
