import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/update_checker.dart';
import '../config.dart';
import '../theme.dart';
import 'projects_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  late final TextEditingController _urlCtrl;
  late final TextEditingController _tokenCtrl;

  // 연결
  bool _connecting = false;
  String? _connectError;

  // 업데이트
  UpdateInfo? _updateInfo;
  bool _checkingUpdate = true;
  bool _updateDismissed = false;
  bool _downloading = false;
  double _downloadProgress = 0;
  String? _downloadError;

  @override
  void initState() {
    super.initState();
    _urlCtrl   = TextEditingController(text: Config.serverUrlHint);
    _tokenCtrl = TextEditingController(text: Config.devTokenHint);
    _checkUpdate();
  }

  @override
  void dispose() {
    _urlCtrl.dispose();
    _tokenCtrl.dispose();
    super.dispose();
  }

  // ── 업데이트 체크 ────────────────────────────────────────────────────────

  Future<void> _checkUpdate() async {
    final info = await UpdateChecker.check();
    if (mounted) {
      setState(() {
        _updateInfo     = info;
        _checkingUpdate = false;
      });
    }
  }

  Future<void> _doUpdate() async {
    if (_updateInfo == null) return;
    setState(() {
      _downloading   = true;
      _downloadError = null;
      _downloadProgress = 0;
    });
    try {
      await UpdateChecker.downloadAndInstall(_updateInfo!, (p) {
        if (mounted) setState(() => _downloadProgress = p);
      });
      // 설치 인텐트가 열리면 다운로드 상태는 유지 (사용자가 돌아올 수 있음)
    } catch (e) {
      if (mounted) {
        setState(() {
          _downloading   = false;
          _downloadError = '다운로드 실패: $e';
        });
      }
    }
  }

  // ── 서버 연결 ────────────────────────────────────────────────────────────

  Future<void> _connect() async {
    final url   = _urlCtrl.text.trim();
    final token = _tokenCtrl.text.trim();
    if (url.isEmpty || token.isEmpty) {
      setState(() => _connectError = '서버 주소와 토큰을 입력하세요.');
      return;
    }

    setState(() { _connecting = true; _connectError = null; });

    final ok = await ApiClient.checkHealthWith(url, token);
    if (!mounted) return;

    if (!ok) {
      setState(() {
        _connecting   = false;
        _connectError = '서버에 연결할 수 없습니다. 주소와 토큰을 확인하세요.';
      });
      return;
    }

    await Config.saveAndMarkConnected(url, token);
    if (!mounted) return;

    Navigator.pushReplacement(
      context,
      MaterialPageRoute(builder: (_) => const ProjectsScreen()),
    );
  }

  // ── UI ───────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cs    = theme.colorScheme;
    final tt    = theme.textTheme;

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 32),

              // ── 업데이트 배너 ──────────────────────────────────────────
              if (!_updateDismissed) ...[
                if (_checkingUpdate)
                  _buildCheckingBanner(theme)
                else if (_updateInfo != null)
                  _buildUpdateBanner(theme, cs, tt),
                if (_checkingUpdate || _updateInfo != null)
                  const SizedBox(height: 24),
              ],

              // ── 앱 타이틀 ─────────────────────────────────────────────
              Text('Agent Paw', style: tt.headlineLarge, textAlign: TextAlign.center),
              const SizedBox(height: 8),
              Text(
                '서버 연결 설정',
                style: tt.bodyLarge?.copyWith(color: cs.onSurfaceVariant),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 40),

              // ── 서버 주소 / 토큰 ──────────────────────────────────────
              TextField(
                controller: _urlCtrl,
                decoration: const InputDecoration(
                  labelText: '서버 주소',
                  hintText: 'http://192.168.0.2:47893',
                ),
                keyboardType: TextInputType.url,
                textInputAction: TextInputAction.next,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _tokenCtrl,
                decoration: const InputDecoration(labelText: 'Dev 토큰'),
                obscureText: true,
                textInputAction: TextInputAction.done,
                onSubmitted: (_) => _connect(),
              ),

              if (_connectError != null) ...[
                const SizedBox(height: 12),
                Text(
                  _connectError!,
                  style: tt.bodySmall?.copyWith(color: cs.error),
                  textAlign: TextAlign.center,
                ),
              ],
              const SizedBox(height: 24),

              FilledButton(
                onPressed: _connecting ? null : _connect,
                child: _connecting
                    ? const SizedBox(
                        height: 20, width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('연결'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildCheckingBanner(ThemeData theme) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainer,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: theme.colorScheme.outlineVariant),
      ),
      child: Row(
        children: [
          SizedBox(
            width: 16, height: 16,
            child: CircularProgressIndicator(
              strokeWidth: 2,
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(width: 12),
          Text(
            '업데이트 확인 중…',
            style: theme.textTheme.bodySmall?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildUpdateBanner(ThemeData theme, ColorScheme cs, TextTheme tt) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.primary50,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.primary300),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // 헤더
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 8, 0),
            child: Row(
              children: [
                Icon(Icons.system_update_rounded,
                    size: 20, color: AppColors.primary700),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'v${_updateInfo!.version} 업데이트 이용 가능',
                    style: tt.titleSmall?.copyWith(
                      color: AppColors.primary700,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.close, size: 18),
                  color: AppColors.primary700,
                  padding: EdgeInsets.zero,
                  constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
                  onPressed: () => setState(() => _updateDismissed = true),
                ),
              ],
            ),
          ),

          // 릴리즈 노트 (있을 때만)
          if (_updateInfo!.releaseNotes.trim().isNotEmpty)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 6, 16, 0),
              child: Text(
                _updateInfo!.releaseNotes.trim(),
                style: tt.bodySmall?.copyWith(color: AppColors.primary700),
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
              ),
            ),

          // 다운로드 진행바
          if (_downloading) ...[
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: _downloadProgress,
                      backgroundColor: AppColors.primary100,
                      color: AppColors.primary500,
                      minHeight: 6,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${(_downloadProgress * 100).toInt()}% 다운로드 중…',
                    style: tt.labelSmall?.copyWith(color: AppColors.primary700),
                  ),
                ],
              ),
            ),
          ],

          // 오류
          if (_downloadError != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: Text(
                _downloadError!,
                style: tt.labelSmall?.copyWith(color: AppColors.error),
              ),
            ),

          // 버튼
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 10, 16, 14),
            child: FilledButton.icon(
              onPressed: _downloading ? null : _doUpdate,
              icon: _downloading
                  ? const SizedBox(
                      width: 16, height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                    )
                  : const Icon(Icons.download_rounded, size: 18),
              label: Text(_downloading ? '다운로드 중…' : '지금 업데이트'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary500,
                foregroundColor: Colors.white,
                minimumSize: const Size(double.infinity, 44),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
