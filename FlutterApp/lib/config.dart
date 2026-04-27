import 'package:shared_preferences/shared_preferences.dart';

class Config {
  static const String _keyServerUrl = 'server_url';
  static const String _keyDevToken = 'dev_token';

  static String _serverUrl = 'http://192.168.0.1:47893';
  static String _devToken = 'dev-token-change-me';

  static String get serverUrl => _serverUrl;
  static String get devToken => _devToken;

  static Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    _serverUrl = prefs.getString(_keyServerUrl) ?? _serverUrl;
    _devToken = prefs.getString(_keyDevToken) ?? _devToken;
  }

  static Future<void> save({String? serverUrl, String? devToken}) async {
    final prefs = await SharedPreferences.getInstance();
    if (serverUrl != null) {
      _serverUrl = serverUrl;
      await prefs.setString(_keyServerUrl, serverUrl);
    }
    if (devToken != null) {
      _devToken = devToken;
      await prefs.setString(_keyDevToken, devToken);
    }
  }

  static bool get isConfigured => _serverUrl.isNotEmpty && _devToken.isNotEmpty;
}
