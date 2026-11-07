import 'dart:async';

/// UI debouncer to prevent redundant API polling.
class Debouncer {
  final Duration delay;
  Timer? _timer;

  Debouncer({required this.delay});

  /// Run [action] after [delay], cancelling any previous pending call.
  void run(void Function() action) {
    _timer?.cancel();
    _timer = Timer(delay, action);
  }

  /// Cancel any pending debounced action.
  void cancel() {
    _timer?.cancel();
  }

  /// Whether a debounced action is currently pending.
  bool get isActive => _timer?.isActive ?? false;

  void dispose() {
    _timer?.cancel();
    _timer = null;
  }
}
