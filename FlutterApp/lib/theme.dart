import 'package:flutter/material.dart';

// ── 컬러 토큰 (디자인시스템 컬러시스템 v1.0.1) ────────────────────────────
class AppColors {
  // Primary
  static const primary50  = Color(0xFFE8F1FB);
  static const primary100 = Color(0xFFC5DCF5);
  static const primary300 = Color(0xFF6BA8E0);
  static const primary500 = Color(0xFF2E75B6); // action/primary (light)
  static const primary700 = Color(0xFF1F4E79);
  static const primary900 = Color(0xFF0F2E4A);

  // Secondary
  static const secondary300 = Color(0xFFF4B183);
  static const secondary500 = Color(0xFFED7D31);
  static const secondary700 = Color(0xFFC55A11);

  // Semantic
  static const success = Color(0xFF2E8B57);
  static const warning = Color(0xFFD99E0B);
  static const error   = Color(0xFFC0392B);

  // Neutral
  static const neutral0   = Color(0xFFFFFFFF);
  static const neutral50  = Color(0xFFF7F8FA);
  static const neutral100 = Color(0xFFEEF0F3);
  static const neutral300 = Color(0xFFC9CED5);
  static const neutral500 = Color(0xFF8A929B);
  static const neutral700 = Color(0xFF3F464E);
  static const neutral900 = Color(0xFF1A1D21);

  // Dark semantic tokens
  static const darkBase          = Color(0xFF1A1D21);
  static const darkSurface       = Color(0xFF262A30);
  static const darkElevated      = Color(0xFF2E333A);
  static const darkBorder        = Color(0xFF363B42);
  static const darkBorderStrong  = Color(0xFF4A5058);
  static const darkTextPrimary   = Color(0xFFF0F2F5);
  static const darkTextSecondary = Color(0xFFB8BEC6);
}

// ── 타이포그래피 (타이포그래피 v1.0.1, 8pt grid) ──────────────────────────
const _text = TextTheme(
  displayLarge:   TextStyle(fontSize: 40, fontWeight: FontWeight.w700, letterSpacing: -0.8, height: 1.2),
  headlineLarge:  TextStyle(fontSize: 32, fontWeight: FontWeight.w700, letterSpacing: -0.5, height: 1.25),
  headlineMedium: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, letterSpacing: -0.25, height: 1.33),
  headlineSmall:  TextStyle(fontSize: 20, fontWeight: FontWeight.w600, letterSpacing: -0.1,  height: 1.4),
  titleLarge:     TextStyle(fontSize: 18, fontWeight: FontWeight.w600, height: 1.44),
  titleMedium:    TextStyle(fontSize: 16, fontWeight: FontWeight.w600, height: 1.5),
  titleSmall:     TextStyle(fontSize: 14, fontWeight: FontWeight.w600, height: 1.57),
  bodyLarge:      TextStyle(fontSize: 16, fontWeight: FontWeight.w400, height: 1.5),
  bodyMedium:     TextStyle(fontSize: 14, fontWeight: FontWeight.w400, height: 1.5),
  bodySmall:      TextStyle(fontSize: 13, fontWeight: FontWeight.w400, height: 1.54),
  labelLarge:     TextStyle(fontSize: 14, fontWeight: FontWeight.w500),
  labelMedium:    TextStyle(fontSize: 12, fontWeight: FontWeight.w400, letterSpacing: 0.16),
  labelSmall:     TextStyle(fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1.28),
);

// ── Light 테마 ────────────────────────────────────────────────────────────
ThemeData buildLightTheme() {
  final cs = ColorScheme(
    brightness: Brightness.light,
    primary:              AppColors.primary500,
    onPrimary:            AppColors.neutral0,
    primaryContainer:     AppColors.primary100,
    onPrimaryContainer:   AppColors.primary900,
    secondary:            AppColors.secondary500,
    onSecondary:          AppColors.neutral0,
    secondaryContainer:   AppColors.secondary300,
    onSecondaryContainer: AppColors.neutral900,
    tertiary:             AppColors.success,
    onTertiary:           AppColors.neutral0,
    error:                AppColors.error,
    onError:              AppColors.neutral0,
    surface:              AppColors.neutral0,
    onSurface:            AppColors.neutral900,
    surfaceContainerHighest: AppColors.neutral100,
    surfaceContainer:     AppColors.neutral50,
    outline:              AppColors.neutral300,
    outlineVariant:       AppColors.neutral100,
    onSurfaceVariant:     AppColors.neutral700,
    shadow:               Colors.black,
    scrim:                Colors.black,
    inverseSurface:       AppColors.neutral900,
    onInverseSurface:     AppColors.neutral50,
    inversePrimary:       AppColors.primary300,
  );

  return _base(cs, _text);
}

// ── Dark 테마 ─────────────────────────────────────────────────────────────
ThemeData buildDarkTheme() {
  final cs = ColorScheme(
    brightness: Brightness.dark,
    primary:              AppColors.primary300,
    onPrimary:            AppColors.primary900,
    primaryContainer:     AppColors.primary700,
    onPrimaryContainer:   AppColors.primary50,
    secondary:            AppColors.secondary300,
    onSecondary:          AppColors.neutral900,
    secondaryContainer:   AppColors.secondary700,
    onSecondaryContainer: AppColors.neutral50,
    tertiary:             AppColors.success,
    onTertiary:           AppColors.neutral0,
    error:                const Color(0xFFCF6679),
    onError:              AppColors.neutral900,
    surface:              AppColors.darkBase,
    onSurface:            AppColors.darkTextPrimary,
    surfaceContainerHighest: AppColors.darkElevated,
    surfaceContainer:     AppColors.darkSurface,
    outline:              AppColors.darkBorderStrong,
    outlineVariant:       AppColors.darkBorder,
    onSurfaceVariant:     AppColors.darkTextSecondary,
    shadow:               Colors.black,
    scrim:                Colors.black,
    inverseSurface:       AppColors.neutral100,
    onInverseSurface:     AppColors.neutral900,
    inversePrimary:       AppColors.primary500,
  );

  return _base(cs, _text);
}

ThemeData _base(ColorScheme cs, TextTheme tt) => ThemeData(
  colorScheme: cs,
  useMaterial3: true,
  textTheme: tt.apply(
    bodyColor: cs.onSurface,
    displayColor: cs.onSurface,
  ),
  appBarTheme: AppBarTheme(
    backgroundColor: cs.surface,
    foregroundColor: cs.onSurface,
    elevation: 0,
    scrolledUnderElevation: 1,
    surfaceTintColor: cs.primary.withOpacity(0.08),
    titleTextStyle: tt.titleMedium?.copyWith(color: cs.onSurface),
  ),
  cardTheme: CardTheme(
    elevation: 0,
    color: cs.surface,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(12),
      side: BorderSide(color: cs.outlineVariant),
    ),
    margin: EdgeInsets.zero,
  ),
  inputDecorationTheme: InputDecorationTheme(
    border: OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: BorderSide(color: cs.outline),
    ),
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: BorderSide(color: cs.outline),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: BorderSide(color: cs.primary, width: 2),
    ),
    errorBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: BorderSide(color: cs.error),
    ),
    filled: true,
    fillColor: cs.surfaceContainer,
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
  ),
  filledButtonTheme: FilledButtonThemeData(
    style: FilledButton.styleFrom(
      backgroundColor: cs.primary,
      foregroundColor: cs.onPrimary,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
      textStyle: tt.labelLarge,
    ),
  ),
  textButtonTheme: TextButtonThemeData(
    style: TextButton.styleFrom(
      foregroundColor: cs.primary,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
    ),
  ),
  chipTheme: ChipThemeData(
    backgroundColor: cs.surfaceContainer,
    labelStyle: tt.labelSmall?.copyWith(color: cs.onSurfaceVariant),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(6)),
    side: BorderSide(color: cs.outlineVariant),
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
  ),
  listTileTheme: ListTileThemeData(
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
  ),
  dividerTheme: DividerThemeData(
    color: cs.outlineVariant,
    thickness: 1,
    space: 0,
  ),
  snackBarTheme: SnackBarThemeData(
    behavior: SnackBarBehavior.floating,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
  ),
  floatingActionButtonTheme: FloatingActionButtonThemeData(
    backgroundColor: cs.primary,
    foregroundColor: cs.onPrimary,
    elevation: 2,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
  ),
  popupMenuTheme: PopupMenuThemeData(
    color: cs.surfaceContainerHighest,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    elevation: 2,
  ),
);
