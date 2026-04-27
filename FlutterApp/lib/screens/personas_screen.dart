import 'package:flutter/material.dart';
import '../api/client.dart';
import '../api/models.dart';

class PersonasScreen extends StatefulWidget {
  final String projectId;
  const PersonasScreen({super.key, required this.projectId});

  @override
  State<PersonasScreen> createState() => _PersonasScreenState();
}

class _PersonasScreenState extends State<PersonasScreen> {
  List<Persona>? _personas;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final personas = await ApiClient.getPersonas(widget.projectId);
      if (mounted) setState(() => _personas = personas);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('페르소나')),
      body: _error != null
          ? Center(
              child: Text(_error!, style: const TextStyle(color: Colors.red)))
          : _personas == null
              ? const Center(child: CircularProgressIndicator())
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: _personas!.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemBuilder: (ctx, i) {
                    final p = _personas![i];
                    return Card(
                      child: ListTile(
                        leading: CircleAvatar(
                          child: Text(
                            (p.label ?? p.name).characters.first.toUpperCase(),
                          ),
                        ),
                        title: Text(p.label ?? p.name),
                        subtitle: p.description != null
                            ? Text(
                                p.description!,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              )
                            : null,
                        trailing: p.isPm
                            ? Chip(
                                label: const Text('PM'),
                                backgroundColor: Theme.of(context)
                                    .colorScheme
                                    .tertiary
                                    .withOpacity(0.2),
                              )
                            : null,
                      ),
                    );
                  },
                ),
    );
  }
}
