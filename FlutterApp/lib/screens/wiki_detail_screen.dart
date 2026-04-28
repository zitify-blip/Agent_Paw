import 'package:flutter/material.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import '../api/client.dart';
import '../api/models.dart';

const _kCategories = ['결정사항', '기술명세', '문제해결', '프로세스', '일반'];

class WikiDetailScreen extends StatefulWidget {
  final String projectId;
  final WikiDocument wiki;

  const WikiDetailScreen({super.key, required this.projectId, required this.wiki});

  @override
  State<WikiDetailScreen> createState() => _WikiDetailScreenState();
}

class _WikiDetailScreenState extends State<WikiDetailScreen> {
  WikiDocument? _detail;
  bool _loading = true;
  bool _editing = false;
  bool _saving = false;

  late TextEditingController _titleCtrl;
  late TextEditingController _contentCtrl;
  String? _editCategory;

  @override
  void initState() {
    super.initState();
    _titleCtrl = TextEditingController();
    _contentCtrl = TextEditingController();
    _load();
  }

  @override
  void dispose() {
    _titleCtrl.dispose();
    _contentCtrl.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final detail = await ApiClient.getWikiDetail(widget.projectId, widget.wiki.wikiId);
      if (mounted) {
        setState(() {
          _detail = detail;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _startEdit() {
    _titleCtrl.text = _detail?.title ?? widget.wiki.title;
    _contentCtrl.text = _detail?.content ?? '';
    _editCategory = _detail?.category ?? widget.wiki.category;
    setState(() => _editing = true);
  }

  void _cancelEdit() => setState(() => _editing = false);

  Future<void> _save() async {
    if (_saving) return;
    setState(() => _saving = true);
    try {
      final updated = await ApiClient.updateWiki(
        widget.projectId,
        widget.wiki.wikiId,
        title: _titleCtrl.text.trim(),
        content: _contentCtrl.text.trim(),
        category: _editCategory,
      );
      if (mounted) {
        setState(() {
          _detail = updated;
          _editing = false;
          _saving = false;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('저장되었습니다.')),
        );
        Navigator.pop(context, true);
      }
    } catch (e) {
      if (mounted) {
        setState(() => _saving = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('저장 실패: $e')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final title = _detail?.title ?? widget.wiki.title;
    final content = _detail?.content ?? '';

    return Scaffold(
      appBar: AppBar(
        title: _editing ? const Text('위키 편집') : Text(title),
        actions: [
          if (_loading)
            const SizedBox.shrink()
          else if (_editing) ...[
            if (_saving)
              const Padding(
                padding: EdgeInsets.all(14),
                child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)),
              )
            else ...[
              TextButton(onPressed: _cancelEdit, child: const Text('취소')),
              TextButton(onPressed: _save, child: const Text('저장')),
            ],
          ] else ...[
            if (_detail != null)
              Padding(
                padding: const EdgeInsets.only(right: 4),
                child: Chip(label: Text('v${_detail!.version}')),
              ),
            IconButton(
              icon: const Icon(Icons.edit_outlined),
              tooltip: '편집',
              onPressed: _startEdit,
            ),
          ],
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _editing
              ? _buildEditForm(theme)
              : content.isEmpty
                  ? const Center(child: Text('내용이 없습니다.'))
                  : Markdown(data: content, padding: const EdgeInsets.all(16)),
    );
  }

  Widget _buildEditForm(ThemeData theme) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('카테고리', style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary)),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          value: _kCategories.contains(_editCategory) ? _editCategory : _kCategories.last,
          decoration: const InputDecoration(border: OutlineInputBorder(), isDense: true),
          items: _kCategories
              .map((c) => DropdownMenuItem(value: c, child: Text(c)))
              .toList(),
          onChanged: (v) => setState(() => _editCategory = v),
        ),
        const SizedBox(height: 16),
        Text('제목', style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary)),
        const SizedBox(height: 8),
        TextFormField(
          controller: _titleCtrl,
          decoration: const InputDecoration(border: OutlineInputBorder()),
        ),
        const SizedBox(height: 16),
        Text('내용 (Markdown)', style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary)),
        const SizedBox(height: 8),
        TextFormField(
          controller: _contentCtrl,
          maxLines: 20,
          decoration: const InputDecoration(
            border: OutlineInputBorder(),
            alignLabelWithHint: true,
          ),
          style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
        ),
        const SizedBox(height: 32),
      ],
    );
  }
}
