import 'package:shared_preferences/shared_preferences.dart';

class Config {
  static const _keyServerUrl    = 'server_url';
  static const _keyDevToken     = 'dev_token';
  static const _keyHasConnected = 'has_connected';

  static String _serverUrl    = 'http://14.32.52.29:47893';
  static String _devToken     = 'paw-dev-2025';
  static bool   _hasConnected = true;

  static String get serverUrl => _serverUrl;
  static String get devToken  => _devToken;

  // 로그인 화면에 보여줄 마지막 입력값 (저장되지 않은 기본값)
  static String get serverUrlHint => _serverUrl.isNotEmpty ? _serverUrl : 'http://192.168.0.2:47893';
  static String get devTokenHint  => _devToken.isNotEmpty  ? _devToken  : '';

  // 한 번이라도 성공적으로 연결된 적 있으면 true → 로그인 화면 스킵
  static bool get isConfigured => _hasConnected && _serverUrl.isNotEmpty && _devToken.isNotEmpty;

  static Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    _serverUrl    = prefs.getString(_keyServerUrl)    ?? _serverUrl;
    _devToken     = prefs.getString(_keyDevToken)     ?? _devToken;
    _hasConnected = prefs.getBool(_keyHasConnected)   ?? _hasConnected;
  }

  /// 헬스 체크 성공 후 호출 — 자격증명 저장 + 연결 성공 플래그 설정
  static Future<void> saveAndMarkConnected(String serverUrl, String devToken) async {
    final prefs = await SharedPreferences.getInstance();
    _serverUrl    = serverUrl;
    _devToken     = devToken;
    _hasConnected = true;
    await prefs.setString(_keyServerUrl, serverUrl);
    await prefs.setString(_keyDevToken, devToken);
    await prefs.setBool(_keyHasConnected, true);
  }

  /// 서버 변경 버튼 — 모든 정보 초기화
  static Future<void> clearConnection() async {
    final prefs = await SharedPreferences.getInstance();
    _serverUrl    = '';
    _devToken     = '';
    _hasConnected = false;
    await prefs.remove(_keyServerUrl);
    await prefs.remove(_keyDevToken);
    await prefs.remove(_keyHasConnected);
  }
}
