import 'package:dartz/dartz.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../core/errors/failures.dart';

enum LoadStatus { initial, loading, loaded, error }

class LoadState<T> {
  final LoadStatus status;
  final T? data;
  final String? error;

  const LoadState({this.status = LoadStatus.initial, this.data, this.error});

  bool get isLoading => status == LoadStatus.initial || status == LoadStatus.loading;
}

/// Cubit عام لشاشات العرض في واجهة المحفّظ: يحمّل بيانات من مستودع (Either)
/// ويحتفظ بالبيانات السابقة أثناء إعادة التحميل.
class LoadCubit<T> extends Cubit<LoadState<T>> {
  final Future<Either<Failure, T>> Function() loader;

  LoadCubit(this.loader) : super(LoadState<T>());

  Future<void> load() async {
    emit(LoadState<T>(
      status: state.data == null ? LoadStatus.loading : LoadStatus.loaded,
      data: state.data,
    ));
    final result = await loader();
    if (isClosed) return;
    result.fold(
      (f) => emit(LoadState<T>(status: LoadStatus.error, error: f.message, data: state.data)),
      (v) => emit(LoadState<T>(status: LoadStatus.loaded, data: v)),
    );
  }
}
