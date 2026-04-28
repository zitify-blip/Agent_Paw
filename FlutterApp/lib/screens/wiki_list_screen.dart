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
  bool _consolidating = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final wikis = await ApiClient.getWikiList(widget.projectId);
      if (mounted) setState(() { _wikis = wikis; _error = null; });
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  Future<void> _consolidate() async {
    setState(() => _consolidating = true);
    try {
      await ApiClient.consolidateWiki(widget.projectId);
      if (mounted) await _load();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('정리 실패: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _consolidating = false);
    }
  }

  Future<void> _delete(WikiDocument w) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('위키 삭제'),
        content: Text('"${w.title}"을(를) 삭제하시겠습니까?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('취소')),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: TextButton.styleFrom(foregroundColor: Colors.red),
            child: const Text('삭제'),
          ),
        ],
      ),
    );
    if (confirm != true || !mounted) return;
    try {
      await ApiClient.deleteWiki(widget.projectId, w.wikiId);
      await _load();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('삭제 실패: $e')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('위키'),
        actions: [
          if (_consolidating)
            const Padding(
              padding: EdgeInsets.all(14),
              child: SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            )
          else
            TextButton.icon(
              icon: const Icon(Icons.auto_fix_high, size: 18),
              label: const Text('정리하기'),
              onPressed: _consolidate,
            ),
        ],
      ),
      body: _error != null
          ? Center(child: Text(_error!, style: const TextStyle(color: Colors.red)))
          : _wikis == null
              ? const Center(child: CircularProgressIndicator())
              : _wikis!.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text('위키 문서가 없습니다.'),
                          const SizedBox(height: 16),
                          FilledButton.icon(
                            icon: const Icon(Icons.auto_fix_high),
                            label: const Text('대화 내용으로 위키 만들기'),
                            onPressed: _consolidating ? null : _consolidate,
                          ),
                        ],
                      ),
                    )
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: _wikis!.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (ctx, i) {
                          final w = _wikis![i];
                          return Dismissible(
                            key: ValueKey(w.wikiId),
                            direction: DismissDirection.endToStart,
                            background: Container(
                              alignment: Alignment.centerRight,
                              padding: const EdgeInsets.only(right: 20),
                              decoration: BoxDecoration(
                                color: Colors.red,
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: const Icon(Icons.delete, color: Colors.white),
                            ),
                            confirmDismiss: (_) async {
                              final confirm = await showDialog<bool>(
                                context: context,
                                builder: (dCtx) => AlertDialog(
                                  title: const Text('위키 삭제'),
                                  content: Text('"${w.title}"을(를) 삭제하시겠습니까?'),
                                  actions: [
                                    TextButton(onPressed: () => Navigator.pop(dCtx, false), child: const Text('취소')),
                                    TextButton(
                                      onPressed: () => Navigator.pop(dCtx, true),
                                      style: TextButton.styleFrom(foregroundColor: Colors.red),
                                      child: const Text('삭제'),
                                    ),
                                  ],
                                ),
                              );
                              return confirm == true;
                            },
                            onDismissed: (_) async {
                              try {
                                await ApiClient.deleteWiki(widget.projectId, w.wikiId);
                                await _load();
                              } catch (e) {
                                if (mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    SnackBar(content: Text('삭제 실패: $e')),
                                  );
                                }
                              }
                            },
                            child: Card(
                              child: ListTile(
                                leading: const Icon(Icons.article_outlined),
                                title: Text(w.title),
                                subtitle: w.category != null ? Text(w.category!) : null,
                                trailing: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  crossAxisAlignment: CrossAxisAlignment.end,
                                  children: [
                                    Text('v${w.version}', style: const TextStyle(fontSize: 12)),
                                    const Icon(Icons.chevron_right, size: 16),
                                  ],
                                ),
                                onTap: () async {
                                  final updated = await Navigator.push<bool>(
                                    ctx,
                                    MaterialPageRoute(
                                      builder: (_) => WikiDetailScreen(
                                        projectId: widget.projectId,
                                        wiki: w,
                                      ),
                                    ),
                                  );
                                  if (updated == true) _load();
                                },
                              ),
                            ),
                          );
                        },
                      ),
                    ),
    );
  }
}
