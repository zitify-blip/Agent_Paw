import 'package:shared_preferences/shared_preferences.dart';

class Config {
  static const _keyServerUrl    = 'server_url';
  static const _keyDevToken     = 'dev_token';
  static const _keyHasConnected = 'has_connected';

  // 하드코딩 기본값. 비워두면 SharedPreferences에서 로드한다.
  static const _defaultUrl   = 'http://14.32.52.29:47893';
  static const _defaultToken = 'paw-dev-2025';

  static String _serverUrl    = _defaultUrl;
  static String _devToken     = _defaultToken;
  static bool   _hasConnected = true;

  static String get serverUrl => _serverUrl;
  static String get devToken  => _devToken;

  static String get serverUrlHint => _serverUrl.isNotEmpty ? _serverUrl : 'http://192.168.0.2:47893';
  static String get devTokenHint  => _devToken.isNotEmpty  ? _devToken  : '';

  static bool get isConfigured => _hasConnected && _serverUrl.isNotEmpty && _devToken.isNotEmpty;

  static Future<void> load() async {
    // 하드코딩 기본값이 있으면 SharedPreferences를 완전히 무시한다.
    // 이전 설치에서 저장된 stale 값이 덮어씌우는 것을 방지한다.
    if (_defaultUrl.isNotEmpty && _defaultToken.isNotEmpty) return;

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
    _serverUrl    = _defaultUrl;
    _devToken     = _defaultToken;
    _hasConnected = _defaultUrl.isNotEmpty && _defaultToken.isNotEmpty;
    await prefs.remove(_keyServerUrl);
    await prefs.remove(_keyDevToken);
    await prefs.remove(_keyHasConnected);
  }
}
