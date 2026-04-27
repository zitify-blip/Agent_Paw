import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/models.dart';

class TimelineScreen extends StatefulWidget {
  final String projectId;
  const TimelineScreen({super.key, required this.projectId});

  @override
  State<TimelineScreen> createState() => _TimelineScreenState();
}

class _TimelineScreenState extends State<TimelineScreen> {
  List<TimelineEvent>? _events;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final events = await ApiClient.getTimeline(widget.projectId);
      if (mounted) setState(() => _events = events);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  Color _typeColor(BuildContext context, String type) {
    final cs = Theme.of(context).colorScheme;
    return switch (type) {
      'USER_MESSAGE' => cs.primary,
      'AI_RESPONSE' => cs.secondary,
      'PM_REPORT' || 'PM_INTERVENTION' => cs.tertiary,
      _ => cs.outline,
    };
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('타임라인')),
      body: _error != null
          ? Center(
              child: Text(_error!, style: const TextStyle(color: Colors.red)))
          : _events == null
              ? const Center(child: CircularProgressIndicator())
              : _events!.isEmpty
                  ? const Center(child: Text('이벤트가 없습니다.'))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.builder(
                        padding: const EdgeInsets.all(16),
                        itemCount: _events!.length,
                        itemBuilder: (ctx, i) {
                          final e = _events![i];
                          final color = _typeColor(ctx, e.eventType);
                          return Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Column(
                                children: [
                                  Container(
                                    width: 12,
                                    height: 12,
                                    decoration: BoxDecoration(
                                      color: color,
                                      shape: BoxShape.circle,
                                    ),
                                  ),
                                  if (i < _events!.length - 1)
                                    Container(
                                      width: 2,
                                      height: 48,
                                      color: color.withOpacity(0.3),
                                    ),
                                ],
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Padding(
                                  padding: const EdgeInsets.only(bottom: 16),
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        e.eventType,
                                        style: TextStyle(
                                          fontWeight: FontWeight.bold,
                                          color: color,
                                          fontSize: 13,
                                        ),
                                      ),
                                      if (e.triggeredBy != null)
                                        Text(e.triggeredBy!,
                                            style: const TextStyle(
                                                fontSize: 12)),
                                      if (e.modelUsed != null)
                                        Text(
                                          e.modelUsed!,
                                          style: TextStyle(
                                            fontSize: 11,
                                            color: Theme.of(ctx)
                                                .colorScheme
                                                .outline,
                                          ),
                                        ),
                                      Text(
                                        _formatDateTime(e.createdAt),
                                        style: TextStyle(
                                          fontSize: 11,
                                          color: Theme.of(ctx)
                                              .colorScheme
                                              .outline,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                            ],
                          );
                        },
                      ),
                    ),
    );
  }

  String _formatDateTime(DateTime dt) {
    final local = dt.toLocal();
    return '${local.month}/${local.day} '
        '${local.hour.toString().padLeft(2, '0')}:'
        '${local.minute.toString().padLeft(2, '0')}';
  }
}
