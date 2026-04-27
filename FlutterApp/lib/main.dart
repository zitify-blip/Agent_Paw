import 'package:flutter/material.dart';
import 'config.dart';
import 'screens/login_screen.dart';
import 'screens/projects_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Config.load();
  runApp(const AgentPawApp());
}

class AgentPawApp extends StatelessWidget {
  const AgentPawApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Agent Paw',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF6750A4),
          brightness: Brightness.light,
        ),
        useMaterial3: true,
      ),
      darkTheme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF6750A4),
          brightness: Brightness.dark,
        ),
        useMaterial3: true,
      ),
      home: Config.isConfigured
          ? const ProjectsScreen()
          : const LoginScreen(),
    );
  }
}
