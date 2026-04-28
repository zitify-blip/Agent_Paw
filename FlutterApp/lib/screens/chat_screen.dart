import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/models.dart';
import '../widgets/message_bubble.dart';
import 'personas_screen.dart';
import 'project_settings_screen.dart';
import 'wiki_list_screen.dart';
import 'timeline_screen.dart';

class ChatScreen extends StatefulWidget {
  final Project project;

  const ChatScreen({super.key, required this.project});

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> {
  final _messageCtrl = TextEditingController();
  final _scrollCtrl = ScrollController();
  List<_DisplayMessage> _messages = [];
  bool _loadingHistory = true;
  bool _sending = false;
  String? _error;
  String? _oldestEventId;
  List<Persona> _personas = [];
  Persona? _selectedPersona;

  @override
  void initState() {
    super.initState();
    _loadHistory();
    _loadPersonas();
  }

  Future<void> _loadPersonas() async {
    try {
      final personas = await ApiClient.getPersonas(widget.project.projectId);
      if (mounted) setState(() => _personas = personas);
    } catch (_) {}
  }

  Future<void> _showPersonaPicker() async {
    await showModalBottomSheet<void>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Text(
                '에이전트 선택',
                style: Theme.of(ctx).textTheme.titleMedium,
              ),
            ),
            ListTile(
              leading: const CircleAvatar(child: Icon(Icons.auto_awesome)),
              title: const Text('자동 (기본)'),
              selected: _selectedPersona == null,
              onTap: () {
                setState(() => _selectedPersona = null);
                Navigator.pop(ctx);
              },
            ),
            ..._personas.map((p) => ListTile(
                  leading: CircleAvatar(
                    child: Text(
                      (p.label ?? p.name).substring(0, 1).toUpperCase(),
                    ),
                  ),
                  title: Text(p.label ?? p.name),
                  subtitle: p.description != null ? Text(p.description!) : null,
                  selected: _selectedPersona?.personaId == p.personaId,
                  onTap: () {
                    setState(() => _selectedPersona = p);
                    Navigator.pop(ctx);
                  },
                )),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
  }

  @override
  void dispose() {
    _messageCtrl.dispose();
    _scrollCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadHistory({bool prepend = false}) async {
    try {
      final msgs = await ApiClient.getMessages(
        widget.project.projectId,
        limit: 50,
        before: prepend ? _oldestEventId : null,
      );
      final display = msgs.map(_toDisplay).toList();
      if (mounted) {
        setState(() {
          if (prepend) {
            _messages = [...display, ..._messages];
          } else {
            _messages = display;
            if (display.isNotEmpty) _oldestEventId = display.first.eventId;
          }
          _loadingHistory = false;
        });
        if (!prepend) _scrollToBottom();
      }
    } catch (e) {
      if (mounted) setState(() {
        _loadingHistory = false;
        _error = e.toString();
      });
    }
  }

  _DisplayMessage _toDisplay(ChatMessage m) {
    return _DisplayMessage(
      eventId: m.eventId,
      content: m.displayContent,
      senderLabel: m.senderLabel,
      isUser: m.isUser,
      isPm: m.eventType == 'PM_REPORT' || m.eventType == 'PM_INTERVENTION',
      time: m.createdAt,
    );
  }

  Future<void> _send() async {
    final text = _messageCtrl.text.trim();
    if (text.isEmpty || _sending) return;

    _messageCtrl.clear();
    setState(() {
      _sending = true;
      _messages.add(_DisplayMessage(
        eventId: 'tmp-${DateTime.now().millisecondsSinceEpoch}',
        content: text,
        senderLabel: '나',
        isUser: true,
        isPm: false,
        time: DateTime.now(),
      ));
    });
    _scrollToBottom();

    try {
      final response = await ApiClient.sendMessage(
          widget.project.projectId, text,
          forcePersonaId: _selectedPersona?.personaId);

      if (!mounted) return;

      final newMsgs = response.turns.isNotEmpty
          ? response.turns
              .map((t) => _DisplayMessage(
                    eventId: '${response.eventId}-${t.turnIndex}',
                    content: t.content,
                    senderLabel: t.personaLabel ?? 'Agent',
                    isUser: false,
                    isPm: t.isPm,
                    time: DateTime.now(),
                  ))
              .toList()
          : [
              _DisplayMessage(
                eventId: response.eventId,
                content: response.content,
                senderLabel: response.personaLabel ?? 'Agent',
                isUser: false,
                isPm: false,
                time: DateTime.now(),
              )
            ];

      setState(() {
        _messages.addAll(newMsgs);
        _sending = false;
      });
      _scrollToBottom();
    } catch (e) {
      if (mounted) {
        setState(() => _sending = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('전송 실패: $e')),
        );
      }
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollCtrl.hasClients) {
        _scrollCtrl.animateTo(
          _scrollCtrl.position.maxScrollExtent,
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOut,
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(widget.project.projectName,
                style: const TextStyle(fontSize: 16)),
          ],
        ),
        actions: [
          PopupMenuButton<String>(
            onSelected: (v) {
              switch (v) {
                case 'personas':
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                        builder: (_) => PersonasScreen(
                            projectId: widget.project.projectId)),
                  );
                case 'wiki':
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                        builder: (_) => WikiListScreen(
                            projectId: widget.project.projectId)),
                  );
                case 'timeline':
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                        builder: (_) => TimelineScreen(
                            projectId: widget.project.projectId)),
                  );
                case 'settings':
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                        builder: (_) => ProjectSettingsScreen(
                            project: widget.project)),
                  );
              }
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'personas', child: Text('페르소나')),
              PopupMenuItem(value: 'wiki', child: Text('위키')),
              PopupMenuItem(value: 'timeline', child: Text('타임라인')),
              PopupMenuDivider(),
              PopupMenuItem(value: 'settings', child: Text('프로젝트 설정')),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          if (_error != null)
            Container(
              color: Colors.red.shade100,
              padding: const EdgeInsets.all(8),
              child: Text(_error!, style: const TextStyle(color: Colors.red)),
            ),
          Expanded(
            child: _loadingHistory
                ? const Center(child: CircularProgressIndicator())
                : NotificationListener<ScrollNotification>(
                    onNotification: (n) {
                      if (n is ScrollStartNotification &&
                          _scrollCtrl.position.pixels == 0) {
                        _loadHistory(prepend: true);
                      }
                      return false;
                    },
                    child: ListView.builder(
                      controller: _scrollCtrl,
                      padding: const EdgeInsets.symmetric(vertical: 8),
                      itemCount: _messages.length,
                      itemBuilder: (ctx, i) {
                        final m = _messages[i];
                        return MessageBubble(
                          content: m.content,
                          senderLabel: m.senderLabel,
                          isUser: m.isUser,
                          isPm: m.isPm,
                          time: m.time,
                        );
                      },
                    ),
                  ),
          ),
          _buildInput(),
        ],
      ),
    );
  }

  Widget _buildInput() {
    return Container(
      padding: EdgeInsets.fromLTRB(
          12, 8, 12, 8 + MediaQuery.of(context).viewInsets.bottom),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.08),
            blurRadius: 4,
            offset: const Offset(0, -2),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (_selectedPersona != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Chip(
                avatar: const Icon(Icons.person, size: 16),
                label: Text(
                  _selectedPersona!.label ?? _selectedPersona!.name,
                  style: const TextStyle(fontSize: 12),
                ),
                deleteIcon: const Icon(Icons.close, size: 14),
                onDeleted: () => setState(() => _selectedPersona = null),
                visualDensity: VisualDensity.compact,
                padding: EdgeInsets.zero,
              ),
            ),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              IconButton(
                icon: Icon(
                  Icons.person_search,
                  color: _selectedPersona != null
                      ? Theme.of(context).colorScheme.primary
                      : null,
                ),
                tooltip: '에이전트 선택',
                onPressed: _personas.isEmpty ? null : _showPersonaPicker,
              ),
              Expanded(
                child: TextField(
                  controller: _messageCtrl,
                  minLines: 1,
                  maxLines: 5,
                  textInputAction: TextInputAction.newline,
                  decoration: const InputDecoration(
                    hintText: '메시지 입력...',
                    border: OutlineInputBorder(),
                    contentPadding:
                        EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                  ),
                  onSubmitted: (_) => _send(),
                ),
              ),
              const SizedBox(width: 8),
              FilledButton(
                onPressed: _sending ? null : _send,
                style: FilledButton.styleFrom(
                  minimumSize: const Size(48, 48),
                  padding: EdgeInsets.zero,
                ),
                child: _sending
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.send),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _DisplayMessage {
  final String eventId;
  final String content;
  final String senderLabel;
  final bool isUser;
  final bool isPm;
  final DateTime time;

  _DisplayMessage({
    required this.eventId,
    required this.content,
    required this.senderLabel,
    required this.isUser,
    required this.isPm,
    required this.time,
  });
}
