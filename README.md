# LLM Wiki

Десктопное приложение для персональной wiki, которую индексирует Claude Code.
Стек: **C# / .NET 10 / Avalonia 12 / `claude-agent-sdk-dotnet`**.

Полная спецификация: [`llmwiki-spec (3).md`](./llmwiki-spec%20(3).md).
План реализации: [`CHECKLIST.md`](./CHECKLIST.md).

---

## Структура решения

```
LLMWiki.slnx
├── src/
│   ├── LLMWiki.Core/      net10 — домен, сервисы, парсеры (без UI и SDK)
│   ├── LLMWiki.App/       net10 — Avalonia UI, обёртка над SDK, git CLI
│   └── external/          (зарезервировано под submodule SDK)
├── claude sdk/             исходники claude-agent-sdk-dotnet (vendor)
└── tests/
    └── LLMWiki.Tests/      NUnit + FluentAssertions
```

**Vault на диске** (создаёт приложение при первом открытии):
```
vault/
├── raw/        # пользовательские файлы
├── wiki/       # md-страницы — пишет только Claude
├── index.md
├── log.md
└── CLAUDE.md   # инструкции для агента
```

---

## Слои

### LLMWiki.Core

| Папка | Назначение |
|---|---|
| `Domain/` | Сущности и enums (`Vault`, `RawFile`, `WikiPage`, `GraphNode/Edge`, `ChatMessage`, `AppSettings`, `GitSyncStatus`, `ConflictEntry`) |
| `Infrastructure/` | `LLMWikiPaths` (per-OS), `AtomicFile` (temp→fsync→rename), `PathValidator` (traversal-guard, NFC), `SingleInstanceLock`, `IClock`, `LoggingSetup` (Serilog) |
| `Settings/` | `SettingsService` — JSON, recovery, defaults |
| `Vault/` | `VaultService` — структура, write-check, восстановление служебных файлов, vault-in-vault детект; `VaultPostOpCleanup` — удаляет файлы вне whitelist после агентной операции |
| `Files/` | `FileTypeClassifier`, `FileLimits`, `FileService` — drag&drop, лимиты, конфликты имён |
| `Parsing/` | `FrontmatterParser` (YAML), `WikiLinkParser` (`[[Page\|Alias]]`), `ParsedWikiPage` |
| `Graph/` | `GraphBuilder` — ноды/рёбра, ghost для битых ссылок, orphan по frontmatter |
| `Lint/` | `LocalLintRunner` — битые ссылки, orphan, isolated, дубли заголовков |
| `Ingest/` | `IngestStateCache`, `IngestQueue` (Channel bounded 100), `IngestScheduler` (дедупликация), `IngestRollback`, `ClaudeToolGuard` (CanUseTool), `AgentProgressParser` |
| `Agent/` | `IClaudeAgent`, `SystemPrompts`, `AgentLimits`, `CredentialsChecker` |
| `Git/` | `GitRemoteUrlValidator`, `CircuitBreaker`, `GitFileTemplates`, `GitPorcelainParser`, `GitSyncStateMachine`, `IPatStorage` + `PlatformPatStorage` (Windows Credential Manager / file 0600 на Linux/Mac) |

### LLMWiki.App

| Папка | Назначение |
|---|---|
| `Agent/` | `SdkClaudeAgent` (поверх `ClaudeAgentSdk`), `ClaudeLoginRunner`, `ClaudeCliChecker` |
| `Git/` | `GitProcessRunner` (PAT через `GIT_ASKPASS` env), `GitSyncService` (Setup/Push/Pull, retry, conflict capture), `AutoSyncTimer` |
| `ViewModels/` | MVVM (CommunityToolkit) — `MainWindowViewModel`, `FilesViewModel`, `ChatViewModel`, `GraphViewModel`, `SettingsViewModel`, `ConflictResolutionViewModel` |
| `Views/` | `MarkdownView` (Markdig → Avalonia controls), `ConflictResolutionWindow` |
| `MainWindow.axaml` | TabControl: Файлы / Граф / Чат / Настройки + статус-бар |
| `AppServices.cs` | DI композиция (`Microsoft.Extensions.DependencyInjection`) |

---

## Сборка и тесты

```bash
dotnet restore LLMWiki.slnx
dotnet build LLMWiki.slnx           # 0 warnings, 0 errors
dotnet test  tests/LLMWiki.Tests    # 139 / 139 passed
```

Запуск приложения (десктоп):
```bash
dotnet run --project src/LLMWiki.App
```

---

## Тесты (139 прохождений)

| Модуль | Файл |
|---|---|
| Path traversal guards | `PathValidatorTests`, `PathValidatorPropertyTests` |
| Settings JSON / recovery | `SettingsServiceTests` |
| Vault init и восстановление | `VaultServiceTests` |
| FileService whitelist / конфликты / лимиты | `FileServiceTests` |
| Frontmatter parser | `FrontmatterParserTests`, `FrontmatterFuzzTests` |
| Wikilink parser | `WikiLinkParserTests`, `WikiLinkFuzzTests` |
| Graph builder (ghost / orphan / dedupe) | `GraphBuilderTests` |
| Ingest state cache | `IngestStateCacheTests` |
| Claude tool guard (Bash/Write/Edit) | `ClaudeToolGuardTests` |
| Agent progress parser | `AgentProgressParserTests` |
| Ingest queue + scheduler | `IngestQueueTests` |
| Ingest rollback (включая crash sim) | `IngestRollbackTests`, `IngestRollbackCrashSimulationTests` |
| Credentials checker | `CredentialsCheckerTests` |
| Git URL validator | `GitRemoteUrlValidatorTests` |
| Circuit breaker | `CircuitBreakerTests` |
| Porcelain parser | `GitPorcelainParserTests` |
| Git sync state machine | `GitSyncStateMachineTests` |
| InMemory PAT storage | `InMemoryPatStorageTests` |
| Vault post-op cleanup | `VaultPostOpCleanupTests` |
| Local lint | `LocalLintRunnerTests` |
| Single instance lock | `SingleInstanceLockTests` |
| Force-directed layout | `ForceDirectedLayoutTests` |
| ANSI escape stripping | `AnsiStripperTests` |

---

## Безопасность (раздел [10] спеки)

- **Atomic writes** — `AtomicFile`: temp файл + `fsync` + rename для `settings.json`, `ingest_state.json`, `index.md`/`log.md`.
- **Path traversal** — `PathValidator.EnsureWithin` обязателен на всех IO к vault; `VaultPostOpCleanup` удаляет файлы вне whitelist после каждой агентной операции.
- **Tool authorization** — `ClaudeToolGuard` подключается через `CanUseTool` callback SDK: `Bash` всегда `Deny`, `Write/Edit` только в `wiki/` (+ корневые `index.md`/`log.md`/`CLAUDE.md`).
- **PAT никогда в URL** — `GitProcessRunner` использует `GIT_ASKPASS` env-script + `LLMWIKI_PAT` env, токен не появляется в `ArgumentList`, `settings.json` или логах.
- **Validation границ** — Git remote URL только `https://github.com/...`, без embedded creds, без shell metacharacters; whitelist расширений; лимиты 50 MB ingest / 10 MB wiki / 100 MB GitHub / 255 chars filename.
- **Circuit breaker** — 5 fails → 5 мин cooldown, авто-закрытие.
- **Логирование** — Serilog в `%APPDATA%/LLMWiki/logs/`, дневная ротация, retention 7 дней; PAT и API key никогда не логируются.

---

## Статус по обязательным сценариям спеки [06]

| # | Сценарий | Статус |
|---|---|---|
| 1 | Первый запуск + установка/login Claude Code | `ClaudeCliChecker` + `CredentialsChecker`. Если Claude не найден — модальное окно. Если credentials отсутствуют — открывается окно `claude login` с PTY (Pty.Net ConPty на Windows / `script` wrapper на Unix). |
| 2 | Drag & drop PDF → Ingest → wiki-страница → toast | `MainWindow` слушает `DragDrop.DropEvent`, файлы копируются через `FileService` в `raw/`, ставятся в `IngestService` (Channel + dedupe), `SdkClaudeAgent` обрабатывает их с rollback и PostOpCleanup. |
| 3 | Чат WikiOnly | `ChatViewModel` ↔ `SdkClaudeAgent.QueryStreamAsync` через `ClaudeSdkClient` (multi-turn); live-стриминг блок-за-блоком. Бейдж «из wiki» виден когда галочка выключена. |
| 4 | Чат Wiki+AI | Тогл `WikiPlusAi` персистится в `settings.json`; system-prompt переключается между `WikiOnlyQueryPrompt` и `ExtendedQueryPrompt`. |
| 5 | Граф связей | `GraphCanvas` рисует force-directed layout (Fruchterman-Reingold), клик по ноде → открывает файл во viewer. Viewport culling и simplified mode для больших графов. |
| 6 | Файловое дерево + viewer | `FilesViewModel` + `MarkdownView` (Markdig→Avalonia controls), `PdfView` (PDFtoImage→SkiaSharp→Bitmap), `Image` для картинок, `SelectableTextBlock` для текста. Лимит 10 MB на текстовый viewer. |
| 7 | Переиндексация | Кнопка «Переиндексировать» → `IngestService.ScheduleFullReindex` дренирует очередь и ставит все файлы из `raw/` заново. |
| 8 | GitHub Setup | Кнопка «Setup GitHub» в настройках → `GitSyncCoordinator.SetupAsync`: `git init`, `.gitignore`, `.gitattributes`, remote, первый push. AutoSync таймер запускается автоматически. |
| 9 | Разрешение конфликтов | После `Pull` с конфликтами автоматически открывается `ConflictResolutionWindow` с локальной/удалённой версией, persistence в `conflict_resolution_state.json`, `git show :2:path` / `:3:path` для содержимого. |

---

## Лицензия

См. [LICENSE](./LICENSE).
