import 'package:dartz/dartz.dart' hide State;
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../config/theme.dart';
import '../../../core/errors/failures.dart';
import '../../../data/models/teacher_models.dart';
import '../../../di/injection_container.dart';
import '../../../domain/repositories/teacher_repository.dart';
import '../../blocs/teacher/load_cubit.dart';
import '../../widgets/async_body.dart';
export '../../widgets/monthly_rating_card.dart';

TeacherRepository get teacherRepo => sl<TeacherRepository>();

/// قيم التقييم بنفس صيغة واجهة الويب (hifz.js evalMap).
const kEvaluationOptions = ['ممتاز', 'جيد جدًا', 'جيد', 'ضعيف', 'يحتاج مراجعة'];

const kLevels = ['مبتدئ', 'متوسط', 'متقدم'];

const kAttendanceStatuses = <String, String>{
  'Present': 'حاضر',
  'Late': 'متأخر',
  'ExcusedAbsence': 'غائب بعذر',
  'UnexcusedAbsence': 'غائب',
};

Color attendanceColor(String status) => switch (status) {
      'Present' => NoorTheme.success,
      'Late' => NoorTheme.warning,
      'ExcusedAbsence' => NoorTheme.info,
      'UnexcusedAbsence' => NoorTheme.error,
      _ => NoorTheme.textSecondary,
    };

void showSnack(BuildContext context, String message, {bool error = false}) {
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text(message),
      backgroundColor: error ? NoorTheme.error : null,
    ),
  );
}

/// يعرض نتيجة عملية (نجاح/فشل) ويعيد true عند النجاح.
Future<bool> runAction(
  BuildContext context,
  Future<Either<Failure, String>> Function() action,
) async {
  final result = await action();
  if (!context.mounted) return result.isRight();
  return result.fold(
    (f) {
      showSnack(context, f.message, error: true);
      return false;
    },
    (msg) {
      showSnack(context, msg);
      return true;
    },
  );
}

Future<bool> confirmDialog(BuildContext context, String message,
    {String confirmText = 'تأكيد'}) async {
  final ok = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      content: Text(message),
      actions: [
        TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
        FilledButton(onPressed: () => Navigator.pop(ctx, true), child: Text(confirmText)),
      ],
    ),
  );
  return ok ?? false;
}

/// شاشة/جزء يحمّل بيانات عبر [LoadCubit] ويعرضها مع حالات التحميل والخطأ والفراغ.
class LoadView<T> extends StatelessWidget {
  final Future<Either<Failure, T>> Function() loader;
  final Widget Function(BuildContext context, T data, Future<void> Function() reload) builder;
  final bool Function(T data)? isEmpty;
  final String emptyTitle;
  final IconData emptyIcon;

  const LoadView({
    super.key,
    required this.loader,
    required this.builder,
    this.isEmpty,
    this.emptyTitle = 'لا توجد بيانات',
    this.emptyIcon = Icons.inbox_outlined,
  });

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => LoadCubit<T>(loader)..load(),
      child: BlocBuilder<LoadCubit<T>, LoadState<T>>(
        builder: (context, state) {
          final cubit = context.read<LoadCubit<T>>();
          final data = state.data;
          return AsyncBody(
              pullToRefresh: false,
            isLoading: state.isLoading && data == null,
            errorMessage: data == null ? state.error : null,
            onRetry: cubit.load,
            isEmpty: data != null && (isEmpty?.call(data) ?? false),
            emptyTitle: emptyTitle,
            emptyIcon: emptyIcon,
            child: data == null
                ? const SizedBox.shrink()
                : RefreshIndicator(
                    onRefresh: cubit.load,
                    child: builder(context, data, cubit.load),
                  ),
          );
        },
      ),
    );
  }
}

/// اختيار طالب من طلاب المحفّظ (GET /api/students) — يُحمّل مرة واحدة.
class StudentDropdown extends StatefulWidget {
  final int? value;
  final ValueChanged<TeacherStudentModel?> onChanged;
  final String label;

  const StudentDropdown({
    super.key,
    required this.value,
    required this.onChanged,
    this.label = 'الطالب',
  });

  @override
  State<StudentDropdown> createState() => _StudentDropdownState();
}

class _StudentDropdownState extends State<StudentDropdown> {
  late final Future<Either<Failure, List<TeacherStudentModel>>> _future = teacherRepo.getStudents();

  @override
  Widget build(BuildContext context) {
    final value = widget.value;
    final onChanged = widget.onChanged;
    final label = widget.label;
    return FutureBuilder<Either<Failure, List<TeacherStudentModel>>>(
      future: _future,
      builder: (context, snap) {
        if (!snap.hasData) return const LinearProgressIndicator();
        return snap.data!.fold(
          (f) => Text(f.message, style: const TextStyle(color: NoorTheme.error)),
          (students) => DropdownButtonFormField<int>(
            initialValue: students.any((s) => s.id == value) ? value : null,
            isExpanded: true,
            decoration: InputDecoration(labelText: label, prefixIcon: const Icon(Icons.person)),
            items: students
                .map((s) => DropdownMenuItem(
                      value: s.id,
                      child: Text('${s.fullName} — ${s.circleName}', overflow: TextOverflow.ellipsis),
                    ))
                .toList(),
            onChanged: (id) => onChanged(
              id == null ? null : students.firstWhere((s) => s.id == id),
            ),
          ),
        );
      },
    );
  }
}

/// اختيار حلقة من حلقات المحفّظ — يُحمّل مرة واحدة.
class CircleDropdown extends StatefulWidget {
  final int? value;
  final ValueChanged<CircleModel?> onChanged;
  final bool allowNone;
  final String noneLabel;

  const CircleDropdown({
    super.key,
    required this.value,
    required this.onChanged,
    this.allowNone = false,
    this.noneLabel = 'بدون حلقة',
  });

  @override
  State<CircleDropdown> createState() => _CircleDropdownState();
}

class _CircleDropdownState extends State<CircleDropdown> {
  late final Future<Either<Failure, List<CircleModel>>> _future = teacherRepo.getCircles();

  @override
  Widget build(BuildContext context) {
    final value = widget.value;
    final onChanged = widget.onChanged;
    final allowNone = widget.allowNone;
    final noneLabel = widget.noneLabel;
    return FutureBuilder<Either<Failure, List<CircleModel>>>(
      future: _future,
      builder: (context, snap) {
        if (!snap.hasData) return const LinearProgressIndicator();
        return snap.data!.fold(
          (f) => Text(f.message, style: const TextStyle(color: NoorTheme.error)),
          (circles) => DropdownButtonFormField<int?>(
            initialValue: circles.any((c) => c.id == value) ? value : null,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'الحلقة', prefixIcon: Icon(Icons.groups)),
            items: [
              if (allowNone) DropdownMenuItem<int?>(value: null, child: Text(noneLabel)),
              ...circles.map((c) => DropdownMenuItem<int?>(
                    value: c.id,
                    child: Text(c.isExtra ? '${c.name} (إضافية)' : c.name),
                  )),
            ],
            onChanged: (id) => onChanged(
              id == null ? null : circles.firstWhere((c) => c.id == id),
            ),
          ),
        );
      },
    );
  }
}

