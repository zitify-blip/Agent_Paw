import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/models.dart';

class ProjectSettingsScreen extends StatefulWidget {
  final Project project;

  const ProjectSettingsScreen({super.key, required this.project});

  @override
  State<ProjectSettingsScreen> createState() => _ProjectSettingsScreenState();
}

class _ProjectSettingsScreenState extends State<ProjectSettingsScreen> {
  late bool _askUserEnabled;
  late double _maxRounds;
  late double _maxParticipants;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _askUserEnabled = widget.project.askUserEnabled;
    _maxRounds = widget.project.maxDiscussionRounds.toDouble();
    _maxParticipants = widget.project.maxDiscussionParticipants.toDouble();
  }

  bool get _dirty =>
      _askUserEnabled != widget.project.askUserEnabled ||
      _maxRounds.round() != widget.project.maxDiscussionRounds ||
      _maxParticipants.round() != widget.project.maxDiscussionParticipants;

  Future<void> _save() async {
    if (!_dirty || _saving) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ApiClient.updateProjectSettings(
        widget.project.projectId,
        askUserEnabled: _askUserEnabled,
        maxDiscussionRounds: _maxRounds.round(),
        maxDiscussionParticipants: _maxParticipants.round(),
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('설정이 저장되었습니다.')),
        );
        Navigator.pop(context, true);
      }
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(
        title: const Text('환경 설정'),
        actions: [
          if (_saving)
            const Padding(
              padding: EdgeInsets.all(14),
              child: SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            )
          else
            TextButton(
              onPressed: _dirty ? _save : null,
              child: const Text('저장'),
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: 8),
        children: [
          if (_error != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
              child: Text(_error!,
                  style: TextStyle(color: theme.colorScheme.error)),
            ),

          _SectionHeader('에이전트 동작'),
          SwitchListTile(
            title: const Text('사용자에게 질문 가능'),
            subtitle: const Text('에이전트가 판단이 필요할 때 사용자에게 되물을 수 있습니다.'),
            value: _askUserEnabled,
            onChanged: (v) => setState(() => _askUserEnabled = v),
          ),

          const Divider(height: 32),
          _SectionHeader('멀티에이전트 토론'),

          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('최대 토론 라운드'),
                Text(
                  '${_maxRounds.round()}',
                  style: theme.textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
              ],
            ),
          ),
          Slider(
            value: _maxRounds,
            min: 1,
            max: 50,
            divisions: 49,
            label: '${_maxRounds.round()}',
            onChanged: (v) => setState(() => _maxRounds = v),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Text(
              '합의(전원 agree) 시 조기 종료. 이 값은 무한 루프 방지 안전 캡입니다.',
              style: theme.textTheme.bodySmall
                  ?.copyWith(color: theme.colorScheme.outline),
            ),
          ),

          const SizedBox(height: 16),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 0),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('최대 토론 참여자 수'),
                Text(
                  '${_maxParticipants.round()}명',
                  style: theme.textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
              ],
            ),
          ),
          Slider(
            value: _maxParticipants,
            min: 2,
            max: 10,
            divisions: 8,
            label: '${_maxParticipants.round()}명',
            onChanged: (v) => setState(() => _maxParticipants = v),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Text(
              'PM을 제외한 비PM 에이전트가 대상입니다.',
              style: theme.textTheme.bodySmall
                  ?.copyWith(color: theme.colorScheme.outline),
            ),
          ),

          const SizedBox(height: 32),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  final String title;
  const _SectionHeader(this.title);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Text(
        title,
        style: Theme.of(context).textTheme.labelLarge?.copyWith(
              color: Theme.of(context).colorScheme.primary,
            ),
      ),
    );
  }
}
