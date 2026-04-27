import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/models.dart';
import '../config.dart';
import 'chat_screen.dart';
import 'login_screen.dart';

class ProjectsScreen extends StatefulWidget {
  const ProjectsScreen({super.key});

  @override
  State<ProjectsScreen> createState() => _ProjectsScreenState();
}

class _ProjectsScreenState extends State<ProjectsScreen> {
  List<Project>? _projects;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final projects = await ApiClient.getProjects();
      if (mounted) setState(() => _projects = projects);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  Future<void> _createProject() async {
    final nameCtrl = TextEditingController();
    final descCtrl = TextEditingController();

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('새 프로젝트'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: nameCtrl,
              decoration: const InputDecoration(
                labelText: '프로젝트 이름',
                border: OutlineInputBorder(),
              ),
              autofocus: true,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: descCtrl,
              decoration: const InputDecoration(
                labelText: '설명 (선택)',
                border: OutlineInputBorder(),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('취소')),
          FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('생성')),
        ],
      ),
    );

    if (confirmed != true) return;
    final name = nameCtrl.text.trim();
    if (name.isEmpty) return;

    try {
      await ApiClient.createProject(name,
          description: descCtrl.text.trim().isEmpty ? null : descCtrl.text.trim());
      await _load();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('생성 실패: $e')),
        );
      }
    }
  }

  void _logout() async {
    await Config.clearConnection();
    if (!mounted) return;
    Navigator.pushReplacement(
        context, MaterialPageRoute(builder: (_) => const LoginScreen()));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('프로젝트'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: '서버 변경',
            onPressed: _logout,
          ),
        ],
      ),
      body: _error != null
          ? Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(_error!, style: const TextStyle(color: Colors.red)),
                  const SizedBox(height: 12),
                  FilledButton(onPressed: _load, child: const Text('재시도')),
                ],
              ),
            )
          : _projects == null
              ? const Center(child: CircularProgressIndicator())
              : _projects!.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text('프로젝트가 없습니다.'),
                          const SizedBox(height: 12),
                          FilledButton(
                            onPressed: _createProject,
                            child: const Text('첫 프로젝트 만들기'),
                          ),
                        ],
                      ),
                    )
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: _projects!.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (ctx, i) {
                          final p = _projects![i];
                          return Card(
                            child: ListTile(
                              title: Text(p.projectName),
                              subtitle: p.description != null
                                  ? Text(p.description!)
                                  : null,
                              trailing: const Icon(Icons.chevron_right),
                              onTap: () => Navigator.push(
                                ctx,
                                MaterialPageRoute(
                                    builder: (_) => ChatScreen(project: p)),
                              ),
                            ),
                          );
                        },
                      ),
                    ),
      floatingActionButton: _projects != null && _projects!.isNotEmpty
          ? FloatingActionButton(
              onPressed: _createProject,
              child: const Icon(Icons.add),
            )
          : null,
    );
  }
}
