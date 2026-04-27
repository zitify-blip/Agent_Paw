import 'package:flutter/material.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import '../api/client.dart';
import '../api/models.dart';

class WikiDetailScreen extends StatefulWidget {
  final String projectId;
  final WikiDocument wiki;

  const WikiDetailScreen(
      {super.key, required this.projectId, required this.wiki});

  @override
  State<WikiDetailScreen> createState() => _WikiDetailScreenState();
}

class _WikiDetailScreenState extends State<WikiDetailScreen> {
  WikiDocument? _detail;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final detail =
          await ApiClient.getWikiDetail(widget.projectId, widget.wiki.wikiId);
      if (mounted) setState(() {
        _detail = detail;
        _loading = false;
      });
    } catch (e) {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final title = _detail?.title ?? widget.wiki.title;
    final content = _detail?.content ?? '';

    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        actions: [
          if (_detail != null)
            Padding(
              padding: const EdgeInsets.only(right: 12),
              child: Chip(label: Text('v${_detail!.version}')),
            ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : content.isEmpty
              ? const Center(child: Text('내용이 없습니다.'))
              : Markdown(
                  data: content,
                  padding: const EdgeInsets.all(16),
                ),
    );
  }
}
