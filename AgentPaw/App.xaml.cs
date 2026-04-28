using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AgentPaw.Data;
using AgentPaw.Orchestrator;
using AgentPaw.Services;
using AgentPaw.ViewModels;
using AgentPaw.Views;

namespace AgentPaw;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true);
                // 설치본에서 시크릿을 사용자 프로파일에 격리 보관. 설치 디렉토리 값보다 우선한다.
                var userConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AgentPaw", "appsettings.json");
                config.AddJsonFile(userConfigPath, optional: true, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                // Database
                services.AddDbContextFactory<AgentPawDbContext>(options =>
                    options.UseNpgsql(context.Configuration.GetConnectionString("Default")));

                // HTTP
                services.AddHttpClient();

                // Services
                services.AddSingleton<EncryptionService>();
                services.AddSingleton<AuthService>();
                services.AddSingleton<GitService>();
                services.AddSingleton<ProjectService>();
                services.AddSingleton<MemberService>();
                services.AddSingleton<ApiKeyService>();
                services.AddSingleton<AiClientService>();
                services.AddSingleton<ConfigLoaderService>();
                services.AddSingleton<UpdateService>();

                // Orchestrator
                services.AddSingleton<ClassifierService>();
                services.AddSingleton<ContextInjectorService>();
                services.AddSingleton<SelfCriticService>();
                services.AddSingleton<ToolExecutorService>();
                services.AddSingleton<PmReportService>();
                services.AddSingleton<OrchestratorService>();

                // Snapshot / Wiki
                services.AddSingleton<SnapshotService>();
                services.AddSingleton<RollbackService>();
                services.AddSingleton<WikiService>();

                // Google Chat + WebSocket (Phase 5)
                services.AddSingleton<ChatBotConfigService>();
                services.AddSingleton<GoogleChatService>();
                services.AddSingleton<GoogleDocsService>();
                services.AddSingleton<ChatCommandService>();
                services.AddSingleton<ChatDispatcherService>();
                services.AddSingleton<PubSubPullService>();
                services.AddSingleton<WebSocketServerService>();
                services.AddSingleton<StatusHttpService>();

                // Mobile API
                services.AddSingleton<MobileApiService>();

                // Slack
                services.AddSingleton<SlackChatService>();
                services.AddSingleton<SlackSocketModeService>();

                // Telegram
                services.AddSingleton<TelegramChatService>();
                services.AddSingleton<TelegramPollingService>();

                // Phase 6: Instructions + Claude CLI
                services.AddSingleton<InstructionService>();
                services.AddSingleton<PersonaService>();
                services.AddSingleton<ClaudeCliService>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<WorkspaceViewModel>();
                services.AddTransient<TimelineViewModel>();
                services.AddTransient<WikiViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<InstructionsViewModel>();
                services.AddTransient<ProjectSettingsViewModel>();
                services.AddTransient<ApiSettingsViewModel>();

                // Views
                services.AddSingleton<MainWindow>();

                // Configuration (for AuthService to access Google settings)
                services.AddSingleton<IConfiguration>(context.Configuration);
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 설치 직후 최초 실행: 사용자 프로파일에 설정 파일이 없고 ConnectionString 도 비어 있으면
        // Example 을 복사한 뒤 notepad 로 열어 편집을 유도한다. 시크릿을 인스톨러에 포함하지 않기 위함.
        var configuration = _host.Services.GetRequiredService<IConfiguration>();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
        {
            var userDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AgentPaw");
            Directory.CreateDirectory(userDir);
            var userConfigPath = Path.Combine(userDir, "appsettings.json");
            if (!File.Exists(userConfigPath))
            {
                var examplePath = Path.Combine(AppContext.BaseDirectory, "appsettings.Example.json");
                if (File.Exists(examplePath))
                    File.Copy(examplePath, userConfigPath);
                else
                    File.WriteAllText(userConfigPath,
                        "{\n  \"ConnectionStrings\": {\n    \"Default\": \"Host=localhost;Port=5432;Database=agent_paw;Username=postgres;Password=\"\n  },\n  \"Google\": {\n    \"ClientId\": \"\",\n    \"ClientSecret\": \"\",\n    \"RedirectUri\": \"http://localhost:47891/auth/callback\"\n  }\n}\n");
            }
            System.Windows.MessageBox.Show(
                $"최초 실행 설정 파일이 생성되었다:\n{userConfigPath}\n\nPostgreSQL 접속 정보와 Google OAuth 클라이언트 정보를 입력한 뒤 앱을 재실행한다.",
                "Agent Paw — 최초 설정", MessageBoxButton.OK, MessageBoxImage.Information);
            try { System.Diagnostics.Process.Start("notepad.exe", userConfigPath); } catch { }
            Shutdown();
            return;
        }

        // 기존 Electron 앱이 테이블을 관리하므로, 연결만 확인한다.
        await using var db = _host.Services.GetRequiredService<IDbContextFactory<AgentPawDbContext>>()
            .CreateDbContext();
        if (!await db.Database.CanConnectAsync())
        {
            var userConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AgentPaw", "appsettings.json");
            System.Windows.MessageBox.Show(
                $"PostgreSQL 데이터베이스에 연결할 수 없습니다.\n\n설정 파일 위치:\n{userConfigPath}\n\nConnectionStrings.Default 값을 확인한 뒤 앱을 재실행한다.",
                "Agent Paw", MessageBoxButton.OK, MessageBoxImage.Error);
            try { System.Diagnostics.Process.Start("notepad.exe", userConfigPath); } catch { }
            Shutdown();
            return;
        }

        // persona_group 테이블 및 persona.group_id 컬럼 자동 생성 + project_id nullable 전환
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS persona_group (
                    group_id TEXT PRIMARY KEY NOT NULL,
                    project_id TEXT REFERENCES project(project_id),
                    name TEXT NOT NULL,
                    description TEXT NOT NULL DEFAULT '',
                    icon TEXT NOT NULL DEFAULT 'folder',
                    sort_order INTEGER NOT NULL DEFAULT 0,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
                );
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'persona' AND column_name = 'group_id'
                    ) THEN
                        ALTER TABLE persona ADD COLUMN group_id TEXT REFERENCES persona_group(group_id);
                    END IF;
                END $$;
                ALTER TABLE persona ALTER COLUMN project_id DROP NOT NULL;
                ALTER TABLE persona_group ALTER COLUMN project_id DROP NOT NULL;
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'persona' AND column_name = 'is_pm'
                    ) THEN
                        ALTER TABLE persona ADD COLUMN is_pm BOOLEAN NOT NULL DEFAULT FALSE;
                    END IF;
                END $$;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_persona_project_pm
                    ON persona(project_id) WHERE is_pm = TRUE;
                CREATE TABLE IF NOT EXISTS project_persona (
                    project_id TEXT NOT NULL REFERENCES project(project_id) ON DELETE CASCADE,
                    persona_id TEXT NOT NULL REFERENCES persona(persona_id) ON DELETE CASCADE,
                    PRIMARY KEY (project_id, persona_id)
                );
                CREATE INDEX IF NOT EXISTS idx_project_persona_project ON project_persona(project_id);
                CREATE INDEX IF NOT EXISTS idx_project_persona_persona ON project_persona(persona_id);
                -- 기존 프로젝트 소속 페르소나를 전역 + link 테이블 쌍으로 이관 (1회성, 멱등)
                INSERT INTO project_persona (project_id, persona_id)
                    SELECT project_id, persona_id FROM persona
                    WHERE project_id IS NOT NULL
                ON CONFLICT DO NOTHING;
                CREATE TABLE IF NOT EXISTS persona_instruction (
                    persona_id TEXT NOT NULL REFERENCES persona(persona_id) ON DELETE CASCADE,
                    file_id TEXT NOT NULL REFERENCES instruction_file(file_id) ON DELETE CASCADE,
                    PRIMARY KEY (persona_id, file_id)
                );
                CREATE INDEX IF NOT EXISTS idx_persona_instruction_persona ON persona_instruction(persona_id);
                CREATE INDEX IF NOT EXISTS idx_persona_instruction_file ON persona_instruction(file_id);
                ALTER TABLE project ADD COLUMN IF NOT EXISTS ask_user_enabled BOOLEAN NOT NULL DEFAULT TRUE;
                ALTER TABLE project ADD COLUMN IF NOT EXISTS max_discussion_rounds INTEGER NOT NULL DEFAULT 10;
                ALTER TABLE project ADD COLUMN IF NOT EXISTS max_discussion_participants INTEGER NOT NULL DEFAULT 4;
                ALTER TABLE project ADD COLUMN IF NOT EXISTS google_doc_id TEXT;
                CREATE TABLE IF NOT EXISTS app_meta (
                    key TEXT PRIMARY KEY NOT NULL,
                    value TEXT NOT NULL,
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
                );
                """);
        }
        catch { /* 이미 존재하면 무시 */ }

        // 전역 페르소나 빌트인 템플릿 시드 — 시드 버전이 바뀌면 재시드한다
        try
        {
            var personaService = _host.Services.GetRequiredService<PersonaService>();
            await personaService.EnsureSeedAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PersonaSeed] FAILED: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"[PersonaSeed]   inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            Console.Error.WriteLine($"[PersonaSeed] stack: {ex.StackTrace}");
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        MainWindow = mainWindow;

        // 외부 채팅/소켓 서비스는 네트워크 I/O를 수반해 첫 창 표시를 지연시킨다 —
        // 창을 먼저 띄우고 백그라운드에서 병렬 기동한다. 실패해도 UI는 계속 동작한다.
        _ = Task.Run(async () =>
        {
            var services = _host.Services;
            var startups = new[]
            {
                SafeStartAsync(() => services.GetRequiredService<PubSubPullService>().StartAsync()),
                SafeStartAsync(() => services.GetRequiredService<SlackSocketModeService>().StartAsync()),
                SafeStartAsync(() => services.GetRequiredService<TelegramPollingService>().StartAsync()),
                SafeStartAsync(() =>
                {
                    services.GetRequiredService<WebSocketServerService>().Start();
                    return Task.CompletedTask;
                }),
                SafeStartAsync(() =>
                {
                    services.GetRequiredService<StatusHttpService>().Start();
                    return Task.CompletedTask;
                }),
                SafeStartAsync(() => services.GetRequiredService<MobileApiService>().StartAsync())
            };
            await Task.WhenAll(startups);
        });
    }

    private static async Task SafeStartAsync(Func<Task> start)
    {
        try { await start().ConfigureAwait(false); }
        catch { /* 설정 미완료·포트 충돌 등 무시 */ }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // 서비스 정리
        try { _host.Services.GetRequiredService<ClaudeCliService>().KillAll(); } catch { }
        try { await _host.Services.GetRequiredService<PubSubPullService>().StopAsync(); } catch { }
        try { await _host.Services.GetRequiredService<SlackSocketModeService>().StopAsync(); } catch { }
        try { await _host.Services.GetRequiredService<TelegramPollingService>().StopAsync(); } catch { }
        try { _host.Services.GetRequiredService<WebSocketServerService>().Stop(); } catch { }
        try { _host.Services.GetRequiredService<StatusHttpService>().Stop(); } catch { }

        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
        Environment.Exit(0);
    }

    public static T GetService<T>() where T : notnull
        => ((App)Current)._host.Services.GetRequiredService<T>();
}
