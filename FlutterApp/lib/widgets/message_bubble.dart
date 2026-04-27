import 'package:flutter/material.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import '../theme.dart';

class MessageBubble extends StatelessWidget {
  final String content;
  final String senderLabel;
  final bool isUser;
  final bool isPm;
  final DateTime time;

  const MessageBubble({
    super.key,
    required this.content,
    required this.senderLabel,
    required this.isUser,
    required this.isPm,
    required this.time,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cs    = theme.colorScheme;
    final tt    = theme.textTheme;
    final isDark = cs.brightness == Brightness.dark;

    // 컬러 시스템 토큰 적용
    Color bubbleBg;
    Color textColor;
    Color? borderColor;

    if (isUser) {
      bubbleBg    = cs.primary;            // primary/500 (light) / primary/300 (dark)
      textColor   = cs.onPrimary;
      borderColor = null;
    } else if (isPm) {
      bubbleBg    = (isDark ? AppColors.warning : AppColors.warning).withOpacity(0.12);
      textColor   = cs.onSurface;
      borderColor = AppColors.warning.withOpacity(0.4);
    } else {
      bubbleBg    = cs.surfaceContainer;   // neutral/50 (light) / dark/surface (dark)
      textColor   = cs.onSurface;
      borderColor = cs.outlineVariant;
    }

    return Padding(
      // 간격 시스템: vertical space/xs(8), horizontal space/md(16)
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Column(
        crossAxisAlignment:
            isUser ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          if (!isUser)
            Padding(
              padding: const EdgeInsets.only(left: 4, bottom: 4),
              child: Text(
                senderLabel,
                style: tt.labelSmall?.copyWith(
                  color: isPm ? AppColors.warning : cs.onSurfaceVariant,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          Container(
            constraints: BoxConstraints(
              maxWidth: MediaQuery.of(context).size.width * 0.80,
            ),
            // Card padding: space/sm(12) vertical, space/md(16) horizontal
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: BoxDecoration(
              color: bubbleBg,
              borderRadius: BorderRadius.only(
                topLeft:     const Radius.circular(16),
                topRight:    const Radius.circular(16),
                bottomLeft:  Radius.circular(isUser ? 16 : 4),
                bottomRight: Radius.circular(isUser ? 4  : 16),
              ),
              border: borderColor != null
                  ? Border.all(color: borderColor, width: 1)
                  : null,
            ),
            child: isUser
                ? Text(
                    content,
                    style: tt.bodyMedium?.copyWith(color: textColor),
                  )
                : MarkdownBody(
                    data: content,
                    styleSheet: MarkdownStyleSheet.fromTheme(theme).copyWith(
                      p:    tt.bodyMedium?.copyWith(color: textColor),
                      code: tt.bodySmall?.copyWith(
                        fontFamily: 'monospace',
                        color: cs.primary,
                        backgroundColor: cs.primaryContainer.withOpacity(0.4),
                      ),
                    ),
                  ),
          ),
          Padding(
            // space/2xs(4) top
            padding: EdgeInsets.only(top: 4, left: isUser ? 0 : 4, right: isUser ? 4 : 0),
            child: Text(
              _formatTime(time),
              style: tt.labelSmall?.copyWith(color: cs.onSurfaceVariant),
            ),
          ),
        ],
      ),
    );
  }

  String _formatTime(DateTime dt) {
    final l = dt.toLocal();
    return '${l.hour.toString().padLeft(2, '0')}:${l.minute.toString().padLeft(2, '0')}';
  }
}
