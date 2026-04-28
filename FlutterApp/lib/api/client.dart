import 'dart:async';
import 'dart:convert';
import 'dart:io';
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

  static Future<dynamic> _patch(String path, Map<String, dynamic> data) async {
    final res = await http.patch(
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

  /// 로그인 전 연결 검증. 성공 시 null, 실패 시 표시할 에러 메시지 반환.
  /// Step 1: /m/health (인증 없이) — 서버 도달 여부
  /// Step 2: /m/me (토큰 포함) — 토큰·UserId 유효성
  static Future<String?> connectCheck(String serverUrl, String devToken) async {
    final healthUri = Uri.tryParse('$serverUrl/m/health');
    if (healthUri == null ||
        (!healthUri.isScheme('http') && !healthUri.isScheme('https'))) {
      return '서버 주소 형식이 올바르지 않습니다.\n예) http://192.168.0.2:47893';
    }

    // ── Step 1: 서버 도달 가능 여부 ─────────────────────────────────
    try {
      final res = await http.get(healthUri).timeout(const Duration(seconds: 5));
      if (res.statusCode != 200) {
        return '서버 응답 오류 (HTTP ${res.statusCode})';
      }
    } on SocketException {
      return '서버에 연결할 수 없습니다.\nIP 주소와 포트(47893)를 확인하세요.';
    } on TimeoutException {
      return '연결 시간 초과.\n서버가 실행 중인지, IP가 올바른지 확인하세요.';
    } catch (_) {
      return '서버에 연결할 수 없습니다.\n주소를 확인하세요.';
    }

    // ── Step 2: 토큰 + UserId 검증 ──────────────────────────────────
    try {
      final res = await http.get(
        Uri.parse('$serverUrl/m/me'),
        headers: {'Authorization': 'Bearer $devToken'},
      ).timeout(const Duration(seconds: 5));
      switch (res.statusCode) {
        case 200:
          return null;
        case 401:
          return '토큰이 올바르지 않습니다.\nappsettings.json의 MobileApi:DevToken을 확인하세요.';
        case 404:
          return '등록된 사용자를 찾을 수 없습니다.\nappsettings.json의 MobileApi:DevUserId를 확인하세요.';
        default:
          return '인증 오류 (HTTP ${res.statusCode})';
      }
    } on TimeoutException {
      return '인증 요청 시간 초과.';
    } catch (_) {
      return '인증 확인 중 오류가 발생했습니다.';
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

  static Future<Project> getProject(String projectId) async {
    final json = await _get('/m/projects/$projectId');
    return Project.fromJson(json as Map<String, dynamic>);
  }

  static Future<Project> updateProjectSettings(
    String projectId, {
    bool? askUserEnabled,
    int? maxDiscussionRounds,
    int? maxDiscussionParticipants,
  }) async {
    final json = await _patch('/m/projects/$projectId/settings', {
      if (askUserEnabled != null) 'askUserEnabled': askUserEnabled,
      if (maxDiscussionRounds != null) 'maxDiscussionRounds': maxDiscussionRounds,
      if (maxDiscussionParticipants != null)
        'maxDiscussionParticipants': maxDiscussionParticipants,
    });
    // 서버는 변경된 필드만 반환하므로 기존 projectId 기준으로 재조회
    return getProject(projectId);
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

  static Future<void> updatePersonaModel(
    String projectId,
    String personaId, {
    String? primaryModel,
    String? fallbackModel,
    bool clearFallback = false,
    double? temperature,
    int? maxTokens,
  }) async {
    await _patch('/m/projects/$projectId/personas/$personaId/model', {
      if (primaryModel != null) 'primaryModel': primaryModel,
      if (clearFallback) 'fallbackModel': null
      else if (fallbackModel != null) 'fallbackModel': fallbackModel,
      if (temperature != null) 'temperature': temperature,
      if (maxTokens != null) 'maxTokens': maxTokens,
    });
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
