# LLM Wiki — чеклист реализации

Источник: `llmwiki-spec (3).md`. Целевой стек: C# / .NET 10 / Avalonia 11.

Статусы: ⬜ pending · 🟡 in progress · ✅ done

---

## Фаза 1. Скелет

- 🟡 **#1 — Solution и структура проектов**
  - `LLMWiki.sln`
  - `src/LLMWiki.Core` — домен и сервисы (без UI)
  - `src/LLMWiki.App` — Avalonia приложение
  - `tests/LLMWiki.Tests` — xUnit
  - `src/external/claude-agent-sdk-dotnet` — submodule SDK
  - `Directory.Packages.props` — централизованное управление версиями NuGet
  - `Directory.Build.props` — Nullable, ImplicitUsings, LangVersion
  - ProjectReference: App→Core, App→SDK, Tests→Core

## Фаза 2. Домен и инфраструктура

- ⬜ **#2 — Domain models** (раздел спеки [01])
  - Сущности: `Vault`, `RawFile`, `WikiPage`, `GraphNode`, `GraphEdge`,
    `ChatMessage`, `AppSettings`, `GitSyncStatus`, `ConflictEntry`
  - Enums: `FileType`, `MessageRole`, `NodeType`, `GitSyncState`

- ⬜ **#3 — SettingsService** (разделы [05], [10])
  - JSON в `%APPDATA%/LLMWiki/settings.json` (Win) / `~/.config/LLMWiki/`
  - Атомарная запись: temp → fsync → rename
  - Defaults на отсутствующие поля; recovery при повреждённом JSON
  - Per-OS пути: `LLMWikiPaths.AppData`, `LLMWikiPaths.Logs`

- ⬜ **#4 — VaultService** (разделы [02], [05], [08], [10])
  - Создание структуры: `raw/`, `wiki/`, `CLAUDE.md`, `index.md`, `log.md`
  - `.llmwiki_write_check` для проверки прав записи
  - Path traversal guard: `vaultRoot` через `Path.GetFullPath` + `StartsWith`
  - Восстановление повреждённых служебных файлов
  - Обнаружение vault внутри vault (предупреждение)

- ⬜ **#5 — FileService**
  - Whitelist расширений (Text/Pdf/Image/Other)
  - Drag&drop файла или папки (рекурсивное добавление, игнор symlinks)
  - Case-insensitive обнаружение конфликта имён → диалог
  - Лимиты: 50 MB Ingest, 255 символов имя, 100 MB warn для git
  - Unicode/NFC нормализация

## Фаза 3. Парсинг и граф

- ⬜ **#6 — FrontmatterParser**
  - YAML frontmatter: `source`, `generated_at`, `orphaned`
  - H1 → `Title`, иначе имя файла
  - Tolerance к malformed YAML (warning state)

- ⬜ **#7 — WikiLinkParser + GraphBuilder**
  - Регэксп / Markdig extension для `[[Page]]`, `[[Page|Alias]]`, `[[folder/page]]`
  - Case-insensitive разрешение, self-links игнорируются, дубли = weight 1
  - Ghost nodes для битых ссылок; orphan-страницы через frontmatter

## Фаза 4. Claude SDK интеграция

- ⬜ **#8 — Claude SDK wrapper**
  - `IClaudeAgent` обёртка над claude-agent-sdk-dotnet
  - `CanUseTool` callback: блокировать `Write`/`Edit` вне `wiki/`, `Bash` всегда
  - MaxTurns: Ingest=200, Lint=100, Query=50
  - `IngestQueue` (`Channel<>` bounded 100), консьюмер-воркер
  - `AgentProgressParser` (`ToolUseBlock` → события прогресса)
  - Stalled stream detection (60s)
  - Rollback при ошибке (удаление wiki файлов с `source = current`)
  - `ingest_state.json` для инкрементального режима

- ⬜ **#9 — Авторизация Claude**
  - `CredentialsChecker` — проверка `.claude/.credentials`
  - `TerminalWidget` через `Pty.Net` для интерактивного `claude login`
  - Cancel через kill процесса

## Фаза 5. Git синхронизация

- ⬜ **#10 — GitSyncService** (раздел [08]/Git, [10])
  - `Process` через `ArgumentList` (защита от injection)
  - PAT через `GIT_ASKPASS` env (никогда в URL)
  - Setup: `git init`, `.gitignore`, `.gitattributes`, первый push/pull
  - AutoSync таймер
  - State machine: `Idle → Pulling → Pushing → Idle`
  - `git merge --abort` при конфликте; `git show MERGE_HEAD:path` для `RemoteContent`
  - Валидация URL: только `https://github.com`, без embedded creds, без metachars
  - Circuit breaker: 5 ошибок → 5 минут пауза
  - Retry: 3 попытки, exponential backoff (1s → 2s → 4s)

## Фаза 6. UI

- ⬜ **#11 — Главное окно + viewer**
  - `MainWindow` с TabControl (Файлы / Граф / Чат)
  - `TreeView` для vault
  - Markdown через `Markdown.Avalonia.Tight` (sanitize)
  - Изображения, plain text
  - PDF — fallback на системное приложение
  - Баннер "файл изменился" (только для viewer, не основная логика)
  - Лимит 10 MB для текста

- ⬜ **#12 — Чат, граф, конфликты, настройки**
  - Чат: WikiOnly/Extended toggle, стриминг, badge "из wiki",
    очередь Query, лимит 200 сообщений / 2 MB
  - Граф: Avalonia Canvas, force-directed,
    viewport culling >200 нод, simplified mode >10000 рёбер
  - Экран конфликтов: список + 2 панели + кнопки выбора +
    persistence через `conflict_resolution_state.json`
  - Настройки: vault path, GitHub URL+PAT, AutoSync, режим чата

## Фаза 7. Финал

- ⬜ **#13 — Lint, IngestScheduler, lock, logging**
  - Lint: битые ссылки, orphans, isolated nodes, дубли
  - `IngestScheduler` — единая точка дедупликации
  - `app.lock` + PID single-instance
  - Serilog → `%APPDATA%/LLMWiki/logs/`, ротация 7 дней,
    PAT/ApiKey не логируются
  - Path traversal post-cleanup после агентной операции

- ⬜ **#14 — Тесты**
  - `PathValidator` (traversal guards) — unit + property-based
  - `WikiLinkParser` — unit + fuzzing
  - `FrontmatterParser` — unit + fuzzing malformed YAML
  - `OrphanDetector`, `ConflictStateMachine`, `CanUseTool` enforcement
  - GitSync с mock git
  - Ingest rollback (crash simulation)

- ⬜ **#15 — Финальная проверка**
  - `dotnet build` без warnings
  - `dotnet test` зелёный
  - Прогон 9 сценариев из секции [06] спеки
  - README.md
