import 'dart:convert';
import 'dart:io';
import 'package:http/http.dart' as http;
import 'package:open_file/open_file.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:path_provider/path_provider.dart';

class UpdateInfo {
  final String version;
  final String downloadUrl;
  final String fileName;
  final String releaseNotes;

  const UpdateInfo({
    required this.version,
    required this.downloadUrl,
    required this.fileName,
    required this.releaseNotes,
  });
}

class UpdateChecker {
  static const _apiUrl =
      'https://api.github.com/repos/zitify-blip/Agent_Paw/releases/latest';

  /// 최신 릴리즈를 확인해 현재 버전보다 신버전이면 UpdateInfo 반환, 없으면 null.
  static Future<UpdateInfo?> check() async {
    try {
      final info = await PackageInfo.fromPlatform();
      final current = info.version;

      final res = await http.get(
        Uri.parse(_apiUrl),
        headers: {'User-Agent': 'AgentPaw-Flutter/$current'},
      ).timeout(const Duration(seconds: 10));

      if (res.statusCode != 200) return null;

      final json = jsonDecode(res.body) as Map<String, dynamic>;
      final tagName = (json['tag_name'] as String? ?? '');
      final latest = tagName.replaceFirst(RegExp(r'^[vV]'), '');

      if (!_isNewer(latest, current)) return null;

      final assets = json['assets'] as List<dynamic>? ?? [];
      for (final asset in assets) {
        final name = asset['name'] as String? ?? '';
        if (name.toLowerCase().endsWith('.apk')) {
          final body = json['body'] as String? ?? '';
          return UpdateInfo(
            version: latest,
            downloadUrl: asset['browser_download_url'] as String,
            fileName: name,
            releaseNotes: body.length > 200 ? '${body.substring(0, 200)}…' : body,
          );
        }
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  /// APK 다운로드 후 설치 인텐트 실행.
  /// [onProgress] 0.0 ~ 1.0 진행률 콜백.
  static Future<void> downloadAndInstall(
    UpdateInfo info,
    void Function(double) onProgress,
  ) async {
    final dir = await getTemporaryDirectory();
    final file = File('${dir.path}/${info.fileName}');

    final client = http.Client();
    try {
      final request = http.Request('GET', Uri.parse(info.downloadUrl));
      final response = await client.send(request);
      final total = response.contentLength ?? 0;
      var received = 0;

      final sink = file.openWrite();
      await for (final chunk in response.stream) {
        sink.add(chunk);
        received += chunk.length;
        if (total > 0) onProgress(received / total);
      }
      await sink.close();

      await OpenFile.open(file.path);
    } finally {
      client.close();
    }
  }

  static bool _isNewer(String latest, String current) {
    List<int> parse(String v) =>
        v.split('.').map((s) => int.tryParse(s.trim()) ?? 0).toList();

    final l = parse(latest);
    final c = parse(current);
    for (var i = 0; i < 3; i++) {
      final li = i < l.length ? l[i] : 0;
      final ci = i < c.length ? c[i] : 0;
      if (li > ci) return true;
      if (li < ci) return false;
    }
    return false;
  }
}
