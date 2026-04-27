import 'dart:convert';
import 'package:http/http.dart' as http;
import '../config.dart';
import 'models.dart';

class ApiException implements Exception {
  final int statusCode;
  final String message;
  ApiException(this.statusCode, this.message);

  @override
  String toString() => 'ApiException($statusCode): $message';
}

class ApiClient {
  static Map<String, String> get _headers => {
        'Authorization': 'Bearer ${Config.devToken}',
        'Content-Type': 'application/json',
      };

  static String _url(String path) => '${Config.serverUrl}$path';

  static Future<dynamic> _get(String path) async {
    final res = await http.get(Uri.parse(_url(path)), headers: _headers);
    final body = jsonDecode(utf8.decode(res.bodyBytes));
    if (res.statusCode >= 400) {
      throw ApiException(res.statusCode, body['error'] as String? ?? 'Error');
    }
    return body;
  }

  static Future<dynamic> _post(String path, Map<String, dynamic> data) async {
    final res = await http.post(
      Uri.parse(_url(path)),
      headers: _headers,
      body: jsonEncode(data),
    );
    final body = jsonDecode(utf8.decode(res.bodyBytes));
    if (res.statusCode >= 400) {
      throw ApiException(res.statusCode, body['error'] as String? ?? 'Error');
    }
    return body;
  }

  static Future<bool> checkHealth() async {
    try {
      final res = await http
          .get(Uri.parse(_url('/m/health')))
          .timeout(const Duration(seconds: 5));
      return res.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  /// 저장하기 전 로그인 화면에서 특정 URL/token으로 헬스 체크
  static Future<bool> checkHealthWith(String serverUrl, String devToken) async {
    try {
      final res = await http.get(
        Uri.parse('$serverUrl/m/health'),
        headers: {'Authorization': 'Bearer $devToken'},
      ).timeout(const Duration(seconds: 5));
      return res.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  static Future<UserInfo> getMe() async {
    final json = await _get('/m/me');
    return UserInfo.fromJson(json as Map<String, dynamic>);
  }

  static Future<List<Project>> getProjects() async {
    final json = await _get('/m/projects');
    return (json as List<dynamic>)
        .map((e) => Project.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  static Future<Project> createProject(String name, {String? description}) async {
    final json = await _post('/m/projects', {
      'name': name,
      if (description != null) 'description': description,
    });
    return Project.fromJson(json as Map<String, dynamic>);
  }

  static Future<List<ChatMessage>> getMessages(
    String projectId, {
    int limit = 50,
    String? before,
  }) async {
    var path = '/m/projects/$projectId/messages?limit=$limit';
    if (before != null) path += '&before=$before';
    final json = await _get(path);
    return (json as List<dynamic>)
        .map((e) => ChatMessage.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  static Future<ChatResponse> sendMessage(
    String projectId,
    String message, {
    String? forcePersonaId,
    List<String>? teamIds,
    String? teamMode,
  }) async {
    final json = await _post('/m/projects/$projectId/chat', {
      'message': message,
      if (forcePersonaId != null) 'forcePersonaId': forcePersonaId,
      if (teamIds != null) 'teamIds': teamIds,
      if (teamMode != null) 'teamMode': teamMode,
    });
    return ChatResponse.fromJson(json as Map<String, dynamic>);
  }

  static Future<List<Persona>> getPersonas(String projectId) async {
    final json = await _get('/m/projects/$projectId/personas');
    return (json as List<dynamic>)
        .map((e) => Persona.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  static Future<List<WikiDocument>> getWikiList(String projectId) async {
    final json = await _get('/m/projects/$projectId/wiki');
    return (json as List<dynamic>)
        .map((e) => WikiDocument.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  static Future<WikiDocument> getWikiDetail(
      String projectId, String wikiId) async {
    final json = await _get('/m/projects/$projectId/wiki/$wikiId');
    return WikiDocument.fromJson(json as Map<String, dynamic>);
  }

  static Future<List<TimelineEvent>> getTimeline(String projectId,
      {int limit = 30}) async {
    final json = await _get('/m/projects/$projectId/timeline?limit=$limit');
    return (json as List<dynamic>)
        .map((e) => TimelineEvent.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}
