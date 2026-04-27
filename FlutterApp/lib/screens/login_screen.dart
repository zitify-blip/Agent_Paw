import 'package:flutter/material.dart';
import '../api/client.dart';
import '../config.dart';
import 'projects_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  late final TextEditingController _urlCtrl;
  late final TextEditingController _tokenCtrl;
  bool _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _urlCtrl   = TextEditingController(text: Config.serverUrlHint);
    _tokenCtrl = TextEditingController(text: Config.devTokenHint);
  }

  @override
  void dispose() {
    _urlCtrl.dispose();
    _tokenCtrl.dispose();
    super.dispose();
  }

  Future<void> _connect() async {
    final url   = _urlCtrl.text.trim();
    final token = _tokenCtrl.text.trim();
    if (url.isEmpty || token.isEmpty) {
      setState(() => _error = '서버 주소와 토큰을 입력하세요.');
      return;
    }

    setState(() { _loading = true; _error = null; });

    // 헬스 체크를 먼저 수행
    final ok = await ApiClient.checkHealthWith(url, token);
    if (!mounted) return;

    if (!ok) {
      setState(() {
        _loading = false;
        _error   = '서버에 연결할 수 없습니다. 주소와 토큰을 확인하세요.';
      });
      return;
    }

    // 연결 성공 후에만 저장
    await Config.saveAndMarkConnected(url, token);

    if (!mounted) return;
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(builder: (_) => const ProjectsScreen()),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cs    = theme.colorScheme;

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 48),
              Text(
                'Agent Paw',
                style: theme.textTheme.headlineLarge,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 8),
              Text(
                '서버 연결 설정',
                style: theme.textTheme.bodyLarge?.copyWith(color: cs.onSurfaceVariant),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 48),
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
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(
                  _error!,
                  style: theme.textTheme.bodySmall?.copyWith(color: cs.error),
                  textAlign: TextAlign.center,
                ),
              ],
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _loading ? null : _connect,
                child: _loading
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
}
