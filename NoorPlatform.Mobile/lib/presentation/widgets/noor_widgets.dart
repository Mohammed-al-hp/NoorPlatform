import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../../config/theme.dart';

/// شعار منصة نور: العلامة (SVG) + الاسم بخط Amiri — نفس شعار منصة الويب.
class NoorLogo extends StatelessWidget {
  final double size;
  final bool showName;
  final bool onDark;
  final bool vertical;

  const NoorLogo({
    super.key,
    this.size = 56,
    this.showName = true,
    this.onDark = false,
    this.vertical = false,
  });

  @override
  Widget build(BuildContext context) {
    final mark = Container(
      width: size,
      height: size,
      padding: EdgeInsets.all(size * 0.09),
      decoration: BoxDecoration(
        color: NoorTheme.cream,
        borderRadius: BorderRadius.circular(size * 0.28),
        border: Border.all(color: NoorTheme.accentGold.withValues(alpha: 0.35)),
        boxShadow: onDark ? null : NoorTheme.cardShadow,
      ),
      child: SvgPicture.asset('assets/images/logo_mark.svg', semanticsLabel: 'شعار منصة نور'),
    );
    if (!showName) return mark;

    final name = Column(
      crossAxisAlignment: vertical ? CrossAxisAlignment.center : CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          'منصة نور',
          style: NoorTheme.quran(
            size: size * 0.46,
            color: onDark ? NoorTheme.goldSoft : NoorTheme.primaryGreen,
          ).copyWith(height: 1.25),
        ),
        Text(
          'لتحفيظ القرآن الكريم',
          style: TextStyle(
            fontSize: (size * 0.2).clamp(11, 16).toDouble(),
            fontWeight: FontWeight.w600,
            color: onDark ? NoorTheme.accentGoldLight : NoorTheme.accentGold,
          ),
        ),
      ],
    );

    return vertical
        ? Column(mainAxisSize: MainAxisSize.min, children: [mark, SizedBox(height: size * 0.22), name])
        : Row(mainAxisSize: MainAxisSize.min, children: [mark, SizedBox(width: size * 0.22), name]);
  }
}

/// رأس متدرّج للصفحات الرئيسية مع نجمة زخرفية خفيفة.
class GradientHeader extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry padding;

  const GradientHeader({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(NoorTheme.spacingLg - 4),
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: NoorTheme.spacingMd),
      decoration: BoxDecoration(
        gradient: NoorTheme.headerGradient,
        borderRadius: BorderRadius.circular(NoorTheme.radiusXl),
        boxShadow: NoorTheme.elevatedShadow,
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        children: [
          PositionedDirectional(
            end: -28,
            top: -28,
            child: Opacity(
              opacity: 0.13,
              child: SvgPicture.asset('assets/images/logo_mark.svg', width: 150, height: 150),
            ),
          ),
          Padding(padding: padding, child: child),
        ],
      ),
    );
  }
}

/// عنوان قسم بخط عريض وعلامة ذهبية.
class SectionTitle extends StatelessWidget {
  final String title;
  final Widget? trailing;

  const SectionTitle(this.title, {super.key, this.trailing});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 8, 4, 10),
      child: Row(
        children: [
          Container(
            width: 4,
            height: 18,
            decoration: BoxDecoration(gradient: NoorTheme.goldGradient, borderRadius: BorderRadius.circular(4)),
          ),
          const SizedBox(width: 10),
          Expanded(child: Text(title, style: Theme.of(context).textTheme.titleMedium)),
          if (trailing != null) trailing!,
        ],
      ),
    );
  }
}

/// صورة رمزية بالحرف الأول من الاسم.
class InitialsAvatar extends StatelessWidget {
  final String name;
  final double radius;
  final bool gold;

  const InitialsAvatar({super.key, required this.name, this.radius = 22, this.gold = false});

  @override
  Widget build(BuildContext context) {
    final trimmed = name.trim();
    final letter = trimmed.isEmpty ? '؟' : trimmed.characters.first;
    return Container(
      width: radius * 2,
      height: radius * 2,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: gold ? NoorTheme.goldGradient : NoorTheme.primaryGradient,
      ),
      child: Text(
        letter,
        style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: radius * 0.85),
      ),
    );
  }
}

/// رقاقة صغيرة على خلفية داكنة (داخل الرأس المتدرّج).
class HeaderPill extends StatelessWidget {
  final IconData icon;
  final String text;

  const HeaderPill({super.key, required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(NoorTheme.radiusFull),
        border: Border.all(color: Colors.white.withValues(alpha: 0.18)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: NoorTheme.accentGoldLight),
          const SizedBox(width: 5),
          Text(text, style: const TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}
