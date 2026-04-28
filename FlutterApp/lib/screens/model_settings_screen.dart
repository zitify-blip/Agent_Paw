import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/models.dart';

const _kClaudeModels = [
  'claude-opus-4-7',
  'claude-sonnet-4-6',
  'claude-haiku-4-5-20251001',
];

const _kFallbackModels = [
  '',
  'claude-sonnet-4-6',
  'claude-haiku-4-5-20251001',
  'gemini-2.5-flash',
  'gemini-2.5-pro',
];

class ModelSettingsScreen extends StatefulWidget {
  final String projectId;

  const ModelSettingsScreen({super.key, required this.projectId});

  @override
  State<ModelSettingsScreen> createState() => _ModelSettingsScreenState();
}

class _ModelSettingsScreenState extends State<ModelSettingsScreen> {
  List<Persona> _personas = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final personas = await ApiClient.getPersonas(widget.projectId);
      if (mounted) setState(() { _personas = personas; _loading = false; });
    } catch (e) {
      if (mounted) setState(() { _error = e.toString(); _loading = false; });
    }
  }

  void _openEditor(Persona persona) async {
    final updated = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => _PersonaModelEditor(
          projectId: widget.projectId,
          persona: persona,
        ),
      ),
    );
    if (updated == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('모델 설정')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: Colors.red)))
              : _personas.isEmpty
                  ? const Center(child: Text('페르소나가 없습니다.'))
                  : ListView.separated(
                      padding: const EdgeInsets.symmetric(vertical: 8),
                      itemCount: _personas.length,
                      separatorBuilder: (_, __) => const Divider(height: 1),
                      itemBuilder: (_, i) {
                        final p = _personas[i];
                        final model = p.primaryModel ?? '(미설정)';
                        return ListTile(
                          leading: CircleAvatar(
                            backgroundColor: _parseColor(p.color),
                            child: Text(
                              (p.label ?? p.name).substring(0, 1).toUpperCase(),
                              style: const TextStyle(color: Colors.white),
                            ),
                          ),
                          title: Text(p.label ?? p.name),
                          subtitle: Text(
                            model,
                            style: Theme.of(context).textTheme.bodySmall,
                          ),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              if (p.isPm)
                                const Padding(
                                  padding: EdgeInsets.only(right: 8),
                                  child: Chip(
                                    label: Text('PM', style: TextStyle(fontSize: 11)),
                                    visualDensity: VisualDensity.compact,
                                    padding: EdgeInsets.zero,
                                  ),
                                ),
                              const Icon(Icons.chevron_right),
                            ],
                          ),
                          onTap: () => _openEditor(p),
                        );
                      },
                    ),
    );
  }

  Color _parseColor(String? name) {
    return switch (name) {
      'blue'   => Colors.blue,
      'green'  => Colors.green,
      'red'    => Colors.red,
      'purple' => Colors.purple,
      'orange' => Colors.orange,
      'teal'   => Colors.teal,
      _        => Colors.blueGrey,
    };
  }
}

// ── 개별 페르소나 모델 편집 화면 ──────────────────────────────────────────

class _PersonaModelEditor extends StatefulWidget {
  final String projectId;
  final Persona persona;

  const _PersonaModelEditor({required this.projectId, required this.persona});

  @override
  State<_PersonaModelEditor> createState() => _PersonaModelEditorState();
}

class _PersonaModelEditorState extends State<_PersonaModelEditor> {
  late String _primaryModel;
  late String _fallbackModel;
  late double _temperature;
  late double _maxTokens;
  bool _saving = false;

  Persona get p => widget.persona;

  @override
  void initState() {
    super.initState();
    _primaryModel = p.primaryModel ?? _kClaudeModels[1];
    _fallbackModel = p.fallbackModel ?? '';
    _temperature = p.temperature.clamp(0.0, 1.0);
    _maxTokens = p.maxTokens.toDouble().clamp(256, 32768);
  }

  bool get _dirty =>
      _primaryModel != (p.primaryModel ?? _kClaudeModels[1]) ||
      _fallbackModel != (p.fallbackModel ?? '') ||
      (_temperature - p.temperature).abs() > 0.001 ||
      _maxTokens.round() != p.maxTokens;

  Future<void> _save() async {
    if (!_dirty || _saving) return;
    setState(() => _saving = true);
    try {
      await ApiClient.updatePersonaModel(
        widget.projectId,
        p.personaId,
        primaryModel: _primaryModel,
        fallbackModel: _fallbackModel.isEmpty ? null : _fallbackModel,
        clearFallback: _fallbackModel.isEmpty,
        temperature: _temperature,
        maxTokens: _maxTokens.round(),
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('저장되었습니다.')),
        );
        Navigator.pop(context, true);
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('저장 실패: $e')),
        );
        setState(() => _saving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(p.label ?? p.name),
        actions: [
          if (_saving)
            const Padding(
              padding: EdgeInsets.all(14),
              child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)),
            )
          else
            TextButton(
              onPressed: _dirty ? _save : null,
              child: const Text('저장'),
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // Primary Model
          Text('기본 모델', style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary)),
          const SizedBox(height: 8),
          DropdownButtonFormField<String>(
            value: _kClaudeModels.contains(_primaryModel) ? _primaryModel : null,
            decoration: const InputDecoration(border: OutlineInputBorder(), isDense: true),
            hint: Text(_primaryModel),
            items: _kClaudeModels
                .map((m) => DropdownMenuItem(value: m, child: Text(m, style: const TextStyle(fontSize: 13))))
                .toList(),
            onChanged: (v) { if (v != null) setState(() => _primaryModel = v); },
          ),
          // 직접 입력
          const SizedBox(height: 4),
          TextFormField(
            initialValue: _kClaudeModels.contains(_primaryModel) ? '' : _primaryModel,
            decoration: const InputDecoration(
              hintText: '또는 직접 입력 (예: claude-opus-4-7)',
              border: OutlineInputBorder(),
              isDense: true,
              contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            ),
            style: const TextStyle(fontSize: 13),
            onChanged: (v) { if (v.trim().isNotEmpty) setState(() => _primaryModel = v.trim()); },
          ),

          const SizedBox(height: 24),

          // Fallback Model
          Text('폴백 모델', style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary)),
          const SizedBox(height: 4),
          Text('기본 모델 호출 실패 시 사용. 비워두면 폴백 없이 실패로 처리합니다.',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.outline)),
          const SizedBox(height: 8),
          DropdownButtonFormField<String>(
            value: _kFallbackModels.contains(_fallbackModel) ? _fallbackModel : '',
            decoration: const InputDecoration(border: OutlineInputBorder(), isDense: true),
            items: _kFallbackModels.map((m) => DropdownMenuItem(
              value: m,
              child: Text(m.isEmpty ? '(없음)' : m, style: const TextStyle(fontSize: 13)),
            )).toList(),
            onChanged: (v) => setState(() => _fallbackModel = v ?? ''),
          ),

          const SizedBox(height: 24),

          // Temperature
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Temperature', style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary)),
              Text(_temperature.toStringAsFixed(2),
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
            ],
          ),
          Slider(
            value: _temperature,
            min: 0.0,
            max: 1.0,
            divisions: 20,
            label: _temperature.toStringAsFixed(2),
            onChanged: (v) => setState(() => _temperature = v),
          ),
          Text('낮을수록 일관성 높음(0.0), 높을수록 창의적(1.0)',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.outline)),

          const SizedBox(height: 24),

          // Max Tokens
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Max Tokens', style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary)),
              Text('${_maxTokens.round()}',
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
            ],
          ),
          Slider(
            value: _maxTokens,
            min: 256,
            max: 32768,
            divisions: 127,
            label: '${_maxTokens.round()}',
            onChanged: (v) => setState(() => _maxTokens = v),
          ),
          Text('응답 1회당 최대 토큰 수',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.outline)),

          const SizedBox(height: 32),
        ],
      ),
    );
  }
}
