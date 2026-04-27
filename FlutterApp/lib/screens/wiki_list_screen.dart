import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/models.dart';
import 'wiki_detail_screen.dart';

class WikiListScreen extends StatefulWidget {
  final String projectId;
  const WikiListScreen({super.key, required this.projectId});

  @override
  State<WikiListScreen> createState() => _WikiListScreenState();
}

class _WikiListScreenState extends State<WikiListScreen> {
  List<WikiDocument>? _wikis;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final wikis = await ApiClient.getWikiList(widget.projectId);
      if (mounted) setState(() => _wikis = wikis);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('위키')),
      body: _error != null
          ? Center(
              child: Text(_error!, style: const TextStyle(color: Colors.red)))
          : _wikis == null
              ? const Center(child: CircularProgressIndicator())
              : _wikis!.isEmpty
                  ? const Center(child: Text('위키 문서가 없습니다.'))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: _wikis!.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (ctx, i) {
                          final w = _wikis![i];
                          return Card(
                            child: ListTile(
                              leading: const Icon(Icons.article_outlined),
                              title: Text(w.title),
                              subtitle: w.category != null
                                  ? Text(w.category!)
                                  : null,
                              trailing: Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                crossAxisAlignment: CrossAxisAlignment.end,
                                children: [
                                  Text('v${w.version}',
                                      style: const TextStyle(fontSize: 12)),
                                  const Icon(Icons.chevron_right, size: 16),
                                ],
                              ),
                              onTap: () => Navigator.push(
                                ctx,
                                MaterialPageRoute(
                                  builder: (_) => WikiDetailScreen(
                                    projectId: widget.projectId,
                                    wiki: w,
                                  ),
                                ),
                              ),
                            ),
                          );
                        },
                      ),
                    ),
    );
  }
}
